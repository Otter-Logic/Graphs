namespace OtterLogic.Graphs.Spatial;

/// <summary>
/// Triangulated surfaces in space that nothing may pass through, and the two
/// questions a graph builder asks of them: is this point inside one, and does this
/// straight step pass through one.
/// <para>
/// The three-dimensional counterpart of <see cref="Planar.PlanarObstacles"/>, on
/// the same terms. Plain coordinates, not geometry types: an adaptor turns its
/// meshes into vertex and face arrays, and everything here is arithmetic a test
/// can run with no Rhino present. The surface is not inside, so a step may run
/// along a wall and touch a corner, because the shortest way round a solid does
/// exactly that. Clearance is the caller's to add, by offsetting first.
/// </para>
/// <para>
/// A closed mesh is a solid: a point can be inside it, and a step between two of
/// its corners that cuts through the middle is blocked. An open mesh — a wall, a
/// floor slab — has no inside, so it blocks exactly the steps that pass through a
/// face. Both are answered by the same test: a step is cut wherever it meets any
/// face, a piece whose middle lies inside a solid is blocked, and so is any
/// crossing that goes cleanly through a face rather than grazing an edge.
/// </para>
/// </summary>
public sealed class SolidObstacles
{
    private readonly Triangle[][] _obstacles;
    private readonly (double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)[] _box;
    private readonly double _tolerance;

    // Ray directions for the inside test: skewed so a ray almost never runs
    // exactly along an edge or a face, with two more to fall back on when one does.
    private static readonly (double X, double Y, double Z)[] RayDirections =
    {
        (0.7430863, 0.4217634, 0.5197221),
        (-0.3894103, 0.8142371, 0.4306857),
        (0.1948572, -0.5583211, 0.8064128),
    };

    /// <param name="meshes">
    /// One (vertices, faces) pair per obstacle: vertices n x 3, x, y, z; faces m x 3,
    /// three vertex indices per triangle, either way round. Quads must be split before
    /// they arrive. A closed mesh is a solid with an inside; an open one only blocks
    /// what crosses it.
    /// </param>
    /// <param name="tolerance">
    /// How close counts as touching, in the units of the coordinates. It decides
    /// when a point is on a surface rather than inside it, so it should be the
    /// tolerance the meshes were made to, not a machine epsilon.
    /// </param>
    public SolidObstacles(IEnumerable<(double[,] Vertices, int[,] Faces)> meshes, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        if (!(tolerance > 0.0) || !double.IsFinite(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be above zero.");

        _tolerance = tolerance;

        var obstacles = new List<Triangle[]>();
        int index = 0;
        foreach (var (vertices, faces) in meshes)
        {
            if (vertices is null || vertices.GetLength(1) != 3)
                throw new ArgumentException($"Obstacle {index} must have n x 3 vertices, x, y, z.", nameof(meshes));
            if (faces is null || faces.GetLength(1) != 3)
                throw new ArgumentException($"Obstacle {index} must have m x 3 faces, three vertex indices each.", nameof(meshes));

            int n = vertices.GetLength(0);
            var triangles = new List<Triangle>();
            for (int f = 0; f < faces.GetLength(0); f++)
            {
                var corners = new Vec3[3];
                for (int k = 0; k < 3; k++)
                {
                    int v = faces[f, k];
                    if (v < 0 || v >= n)
                        throw new ArgumentException($"Face {f} of obstacle {index} points at vertex {v}, but vertices run from 0 to {n - 1}.", nameof(meshes));

                    corners[k] = new Vec3(vertices[v, 0], vertices[v, 1], vertices[v, 2]);
                    if (!corners[k].IsFinite)
                        throw new ArgumentException($"Obstacle {index} has a vertex that is not finite.", nameof(meshes));
                }

                var triangle = new Triangle(corners[0], corners[1], corners[2]);
                if (triangle.Normal.Length > tolerance * tolerance)
                    triangles.Add(triangle);
            }

            if (triangles.Count == 0)
                throw new ArgumentException($"Obstacle {index} has no face with any area.", nameof(meshes));

            obstacles.Add(triangles.ToArray());
            index++;
        }

        _obstacles = obstacles.ToArray();
        _box = new (double, double, double, double, double, double)[_obstacles.Length];
        for (int o = 0; o < _obstacles.Length; o++)
        {
            var all = _obstacles[o].SelectMany(t => new[] { t.A, t.B, t.C }).ToArray();
            _box[o] = (all.Min(p => p.X) - tolerance, all.Min(p => p.Y) - tolerance, all.Min(p => p.Z) - tolerance,
                       all.Max(p => p.X) + tolerance, all.Max(p => p.Y) + tolerance, all.Max(p => p.Z) + tolerance);
        }
    }

    /// <summary>Number of obstacles.</summary>
    public int Count => _obstacles.Length;

    /// <summary>
    /// Whether the point lies strictly inside any closed obstacle. On a surface is
    /// not inside, and an open mesh has no inside.
    /// </summary>
    public bool Contains(double x, double y, double z)
    {
        var p = new Vec3(x, y, z);
        for (int o = 0; o < _obstacles.Length; o++)
            if (Inside(o, p))
                return true;

        return false;
    }

    /// <summary>
    /// Whether the straight step from a to b passes through any obstacle: cleanly
    /// through a face, or through the inside of a solid.
    /// </summary>
    public bool Blocks(double ax, double ay, double az, double bx, double by, double bz)
    {
        var a = new Vec3(ax, ay, az);
        var b = new Vec3(bx, by, bz);
        var d = b - a;
        double length = d.Length;
        if (length <= _tolerance)
            return Contains(ax, ay, az);

        // Every parameter at which the step meets a face, and whether any of those
        // meetings goes cleanly through rather than grazing an edge or a corner.
        var cuts = new List<double> { 0.0, 1.0 };
        for (int o = 0; o < _obstacles.Length; o++)
        {
            if (!SegmentMeetsBox(o, a, b))
                continue;

            foreach (var triangle in _obstacles[o])
            {
                if (!TryMeet(triangle, a, d, length, out double t, out bool clean))
                    continue;

                if (!clean)
                    clean = OnFlatInterior(_obstacles[o], a + d * t);

                if (clean && t > _tolerance / length && t < 1.0 - _tolerance / length)
                    return true;

                cuts.Add(t);
            }
        }

        // Between two cuts the step is wholly inside a solid or wholly outside every
        // one, so the middle of each piece settles it.
        cuts.Sort();
        for (int k = 1; k < cuts.Count; k++)
        {
            if (cuts[k] - cuts[k - 1] <= _tolerance / length)
                continue;

            var mid = a + d * (0.5 * (cuts[k] + cuts[k - 1]));
            if (Contains(mid.X, mid.Y, mid.Z))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ray parity, after ruling the surface out: within tolerance of any face is not
    /// inside. Retried in another direction when the ray grazes an edge, where a
    /// count could go either way.
    /// </summary>
    private bool Inside(int obstacle, Vec3 p)
    {
        var box = _box[obstacle];
        if (p.X < box.MinX || p.Y < box.MinY || p.Z < box.MinZ || p.X > box.MaxX || p.Y > box.MaxY || p.Z > box.MaxZ)
            return false;

        var triangles = _obstacles[obstacle];
        foreach (var triangle in triangles)
            if (triangle.DistanceTo(p) <= _tolerance)
                return false;

        foreach (var (dx, dy, dz) in RayDirections)
        {
            var d = new Vec3(dx, dy, dz);
            var crossings = new List<double>();
            bool grazed = false;

            foreach (var triangle in triangles)
            {
                if (!TryMeetRay(triangle, p, d, out double t, out bool clean))
                    continue;

                if (!clean && !OnFlatInterior(triangles, p + d * t))
                {
                    grazed = true;
                    break;
                }

                crossings.Add(t);
            }

            if (grazed)
                continue;

            // A seam between two triangles is met once by each; one crossing, not two.
            crossings.Sort();
            int distinct = 0;
            for (int k = 0; k < crossings.Count; k++)
                if (k == 0 || crossings[k] - crossings[k - 1] > _tolerance)
                    distinct++;

            return distinct % 2 == 1;
        }

        // Three grazes in a row is a mesh built to defeat the test; call the point outside.
        return false;
    }

    private bool SegmentMeetsBox(int obstacle, Vec3 a, Vec3 b)
    {
        var box = _box[obstacle];
        return Math.Max(a.X, b.X) >= box.MinX && Math.Min(a.X, b.X) <= box.MaxX
            && Math.Max(a.Y, b.Y) >= box.MinY && Math.Min(a.Y, b.Y) <= box.MaxY
            && Math.Max(a.Z, b.Z) >= box.MinZ && Math.Min(a.Z, b.Z) <= box.MaxZ;
    }

    /// <summary>
    /// Where the segment a + t d, t in [0, 1], meets the triangle, if it does.
    /// <paramref name="clean"/> says the meeting point is further than the tolerance
    /// from every edge of the triangle — a pass through the face, not a graze.
    /// </summary>
    private bool TryMeet(Triangle triangle, Vec3 a, Vec3 d, double length, out double t, out bool clean)
    {
        t = double.NaN;
        clean = false;

        double denominator = Vec3.Dot(triangle.Normal, d);
        if (Math.Abs(denominator) <= 1e-12 * triangle.Normal.Length * length)
            return false; // Parallel: lying in the face is running along a wall, not through it.

        t = Vec3.Dot(triangle.Normal, triangle.A - a) / denominator;
        double slack = _tolerance / length;
        if (t < -slack || t > 1.0 + slack)
            return false;

        var x = a + d * t;
        if (triangle.DistanceTo(x) > _tolerance)
            return false;

        clean = triangle.DistanceToEdges(x) > _tolerance;
        return true;
    }

    /// <summary>
    /// A meeting point on an edge counts as a pass through the face when the edge is
    /// only a seam of the triangulation: every triangle touching the point lies in one
    /// plane, and the point is clear of every edge of theirs that is not shared with
    /// another of them. A real edge — a fold, or the rim of a wall — fails one of those.
    /// </summary>
    private bool OnFlatInterior(Triangle[] triangles, Vec3 x)
    {
        var touching = new List<int>();
        for (int i = 0; i < triangles.Length; i++)
            if (triangles[i].DistanceTo(x) <= _tolerance)
                touching.Add(i);

        if (touching.Count < 2)
            return false;

        var first = triangles[touching[0]].Normal;
        foreach (int i in touching)
        {
            var normal = triangles[i].Normal;
            if (Vec3.Cross(first, normal).Length > 1e-9 * first.Length * normal.Length)
                return false;
        }

        foreach (int i in touching)
        {
            foreach (var (a, b) in triangles[i].Edges())
            {
                if (Triangle.SegmentDistance(x, a, b) > _tolerance)
                    continue;

                bool shared = false;
                foreach (int j in touching)
                    if (j != i && triangles[j].HasEdge(a, b, _tolerance))
                    {
                        shared = true;
                        break;
                    }

                if (!shared)
                    return false;
            }
        }

        return true;
    }

    private bool TryMeetRay(Triangle triangle, Vec3 origin, Vec3 d, out double t, out bool clean)
    {
        t = double.NaN;
        clean = false;
        double denominator = Vec3.Dot(triangle.Normal, d);
        if (Math.Abs(denominator) <= 1e-12 * triangle.Normal.Length)
            return false;

        t = Vec3.Dot(triangle.Normal, triangle.A - origin) / denominator;
        if (t <= 0.0)
            return false;

        var x = origin + d * t;
        if (triangle.DistanceTo(x) > _tolerance)
            return false;

        clean = triangle.DistanceToEdges(x) > _tolerance;
        return true;
    }

    private readonly record struct Vec3(double X, double Y, double Z)
    {
        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    private readonly struct Triangle
    {
        public readonly Vec3 A, B, C, Normal;

        public Triangle(Vec3 a, Vec3 b, Vec3 c)
        {
            A = a; B = b; C = c;
            Normal = Vec3.Cross(b - a, c - a);
        }

        /// <summary>Distance from a point to the nearest point of the triangle (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        public double DistanceTo(Vec3 p) => (p - ClosestPoint(p)).Length;

        /// <summary>Distance from a point to the nearest of the three edges.</summary>
        public double DistanceToEdges(Vec3 p)
            => Math.Min(SegmentDistance(p, A, B), Math.Min(SegmentDistance(p, B, C), SegmentDistance(p, C, A)));

        public IEnumerable<(Vec3 A, Vec3 B)> Edges()
        {
            yield return (A, B);
            yield return (B, C);
            yield return (C, A);
        }

        /// <summary>Whether this triangle has the edge between the two points, either way round, to within the tolerance.</summary>
        public bool HasEdge(Vec3 a, Vec3 b, double tolerance)
        {
            foreach (var (p, q) in Edges())
            {
                bool same = ((p - a).Length <= tolerance && (q - b).Length <= tolerance)
                         || ((p - b).Length <= tolerance && (q - a).Length <= tolerance);
                if (same)
                    return true;
            }

            return false;
        }

        private Vec3 ClosestPoint(Vec3 p)
        {
            var ab = B - A; var ac = C - A; var ap = p - A;
            double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
            if (d1 <= 0.0 && d2 <= 0.0) return A;

            var bp = p - B;
            double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
            if (d3 >= 0.0 && d4 <= d3) return B;

            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
                return A + ab * (d1 / (d1 - d3));

            var cp = p - C;
            double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
            if (d6 >= 0.0 && d5 <= d6) return C;

            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
                return A + ac * (d2 / (d2 - d6));

            double va = d3 * d6 - d5 * d4;
            if (va <= 0.0 && d4 - d3 >= 0.0 && d5 - d6 >= 0.0)
                return B + (C - B) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));

            double denominator = 1.0 / (va + vb + vc);
            return A + ab * (vb * denominator) + ac * (vc * denominator);
        }

        public static double SegmentDistance(Vec3 p, Vec3 a, Vec3 b)
        {
            var ab = b - a;
            double squared = Vec3.Dot(ab, ab);
            double t = squared <= 0.0 ? 0.0 : Math.Clamp(Vec3.Dot(p - a, ab) / squared, 0.0, 1.0);
            return (p - (a + ab * t)).Length;
        }
    }
}

namespace OtterLogic.Graphs.Planar;

/// <summary>
/// Closed polygons in the plane that nothing may pass through, and the two
/// questions a graph builder asks of them: is this point inside one, and does this
/// straight step cross one.
/// <para>
/// Plain coordinates, not geometry types. Graphs references nothing, and this is
/// what keeps that true while still letting a graph be prepared from a drawing:
/// an adaptor turns its curves into arrays of x and y, and everything here is
/// arithmetic a test can run with no Rhino present.
/// </para>
/// <para>
/// The boundary is not inside. A step may run along a wall and may touch a
/// corner, because the shortest way round an obstacle does exactly that — it
/// goes corner to corner — and a rule that refused contact would refuse every
/// shortest path. Clearance is the caller's to add, by offsetting the outlines
/// before they arrive.
/// </para>
/// </summary>
public sealed class PlanarObstacles
{
    private readonly double[][] _x;
    private readonly double[][] _y;
    private readonly (double MinX, double MinY, double MaxX, double MaxY)[] _box;
    private readonly double _tolerance;

    /// <param name="polygons">
    /// One n x 2 array per obstacle, x then y, in order round the outline either
    /// way. Closed implicitly; a repeated first vertex at the end is dropped.
    /// Outlines may be concave and may overlap each other, but one that crosses
    /// itself has no inside to speak of and gives whatever even-odd makes of it.
    /// </param>
    /// <param name="tolerance">
    /// How close counts as touching, in the units of the coordinates. It decides
    /// when a point is on a boundary rather than inside it, so it should be the
    /// tolerance the outlines were drawn to, not a machine epsilon.
    /// </param>
    public PlanarObstacles(IEnumerable<double[,]> polygons, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(polygons);
        if (!(tolerance > 0.0) || !double.IsFinite(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be above zero.");

        _tolerance = tolerance;

        var xs = new List<double[]>();
        var ys = new List<double[]>();
        int index = 0;

        foreach (var polygon in polygons)
        {
            if (polygon is null || polygon.GetLength(1) != 2)
                throw new ArgumentException($"Obstacle {index} must be an n x 2 array of x and y.", nameof(polygons));

            int n = polygon.GetLength(0);
            if (n > 1 && Math.Abs(polygon[0, 0] - polygon[n - 1, 0]) <= tolerance
                      && Math.Abs(polygon[0, 1] - polygon[n - 1, 1]) <= tolerance)
                n--;

            if (n < 3)
                throw new ArgumentException($"Obstacle {index} has {n} distinct vertices; an outline needs at least three.", nameof(polygons));

            var x = new double[n];
            var y = new double[n];
            for (int v = 0; v < n; v++)
            {
                x[v] = polygon[v, 0];
                y[v] = polygon[v, 1];
                if (!double.IsFinite(x[v]) || !double.IsFinite(y[v]))
                    throw new ArgumentException($"Obstacle {index} has a vertex that is not finite.", nameof(polygons));
            }

            xs.Add(x);
            ys.Add(y);
            index++;
        }

        _x = xs.ToArray();
        _y = ys.ToArray();
        _box = new (double, double, double, double)[_x.Length];
        for (int p = 0; p < _x.Length; p++)
            _box[p] = (_x[p].Min() - tolerance, _y[p].Min() - tolerance, _x[p].Max() + tolerance, _y[p].Max() + tolerance);
    }

    /// <summary>Number of obstacles.</summary>
    public int Count => _x.Length;

    /// <summary>
    /// Every vertex of every obstacle, obstacle by obstacle in the order given —
    /// the only places a shortest path round them ever turns.
    /// </summary>
    public double[,] Corners()
    {
        int total = _x.Sum(x => x.Length);
        var corners = new double[total, 2];
        int row = 0;

        for (int p = 0; p < _x.Length; p++)
        {
            for (int v = 0; v < _x[p].Length; v++, row++)
            {
                corners[row, 0] = _x[p][v];
                corners[row, 1] = _y[p][v];
            }
        }

        return corners;
    }

    /// <summary>Whether the point lies strictly inside any obstacle. On a boundary is not inside.</summary>
    public bool Contains(double x, double y)
    {
        for (int p = 0; p < _x.Length; p++)
            if (Inside(p, x, y))
                return true;

        return false;
    }

    /// <summary>
    /// Whether the straight step from a to b passes through the inside of any
    /// obstacle.
    /// <para>
    /// Not a test for crossing an edge, which gets the two hard cases wrong in
    /// opposite directions: a step between two corners of one obstacle crosses no
    /// edge and may still cut straight through it, and a step that grazes a corner
    /// meets two edges and passes through nothing. Instead the step is cut
    /// wherever it meets any boundary at all, and each piece between cuts is
    /// either wholly inside an obstacle or wholly outside every one — so the
    /// midpoint of each piece settles it.
    /// </para>
    /// </summary>
    public bool Blocks(double ax, double ay, double bx, double by)
    {
        double rx = bx - ax, ry = by - ay;
        double length = Math.Sqrt(rx * rx + ry * ry);
        if (length <= _tolerance)
            return Contains(ax, ay);

        double minX = Math.Min(ax, bx), maxX = Math.Max(ax, bx);
        double minY = Math.Min(ay, by), maxY = Math.Max(ay, by);
        double slack = _tolerance / length;

        List<double>? cuts = null;

        for (int p = 0; p < _x.Length; p++)
        {
            var box = _box[p];
            if (maxX < box.MinX || minX > box.MaxX || maxY < box.MinY || minY > box.MaxY)
                continue;

            cuts ??= new List<double> { 0.0, 1.0 };

            var x = _x[p];
            var y = _y[p];
            for (int v = 0, w = x.Length - 1; v < x.Length; w = v++)
            {
                double px = x[w], py = y[w];
                double sx = x[v] - px, sy = y[v] - py;
                double side = Math.Sqrt(sx * sx + sy * sy);
                if (side <= _tolerance)
                    continue;

                double denominator = rx * sy - ry * sx;
                double offsetX = px - ax, offsetY = py - ay;

                // The cross product is length x side x the sine between them, so this reads:
                // over the shorter of the two, they drift apart by less than the tolerance.
                // That is parallel for every purpose here, and dividing by it would not be safe.
                if (Math.Abs(denominator) <= _tolerance * Math.Max(length, side))
                {
                    // Collinear: the edge's ends cut the step where they fall on it.
                    if (Math.Abs(offsetX * ry - offsetY * rx) / length > _tolerance)
                        continue;

                    AddCut(cuts, (offsetX * rx + offsetY * ry) / (length * length), slack);
                    AddCut(cuts, ((x[v] - ax) * rx + (y[v] - ay) * ry) / (length * length), slack);
                    continue;
                }

                double t = (offsetX * sy - offsetY * sx) / denominator;
                double u = (offsetX * ry - offsetY * rx) / denominator;
                double edgeSlack = _tolerance / side;

                if (t >= -slack && t <= 1.0 + slack && u >= -edgeSlack && u <= 1.0 + edgeSlack)
                    AddCut(cuts, t, slack);
            }
        }

        if (cuts is null)
            return false;

        cuts.Sort();
        for (int k = 1; k < cuts.Count; k++)
        {
            // A piece shorter than the tolerance is a touch, not a passage.
            if ((cuts[k] - cuts[k - 1]) * length <= 2.0 * _tolerance)
                continue;

            double m = 0.5 * (cuts[k] + cuts[k - 1]);
            if (Contains(ax + m * rx, ay + m * ry))
                return true;
        }

        return false;
    }

    private static void AddCut(List<double> cuts, double t, double slack)
    {
        if (t > slack && t < 1.0 - slack)
            cuts.Add(t);
    }

    /// <summary>Even-odd ray cast, after ruling the boundary out: within tolerance of any edge is not inside.</summary>
    private bool Inside(int p, double px, double py)
    {
        var box = _box[p];
        if (px < box.MinX || px > box.MaxX || py < box.MinY || py > box.MaxY)
            return false;

        var x = _x[p];
        var y = _y[p];
        bool inside = false;

        for (int v = 0, w = x.Length - 1; v < x.Length; w = v++)
        {
            double ex = x[v] - x[w], ey = y[v] - y[w];
            double squared = ex * ex + ey * ey;
            double along = squared > 0.0 ? Math.Clamp(((px - x[w]) * ex + (py - y[w]) * ey) / squared, 0.0, 1.0) : 0.0;
            double dx = px - (x[w] + along * ex), dy = py - (y[w] + along * ey);
            if (dx * dx + dy * dy <= _tolerance * _tolerance)
                return false;

            if ((y[v] > py) != (y[w] > py) && px < x[w] + (py - y[w]) / ey * ex)
                inside = !inside;
        }

        return inside;
    }
}

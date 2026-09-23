namespace OtterLogic.Graphs.Spatial;

/// <summary>
/// A drawn network read as a graph: which node each line end became, and where
/// the nodes are.
/// </summary>
/// <param name="Nodes">n x 3 node positions, x, y, z: each node sits where the first line end that made it was.</param>
/// <param name="From">Per line, the node its start welded to.</param>
/// <param name="To">Per line, the node its end welded to.</param>
/// <param name="Collapsed">Lines whose two ends welded to the same node — shorter than the tolerance — and so make no connection.</param>
public sealed record LineNetworkResult(double[,] Nodes, int[] From, int[] To, int[] Collapsed)
{
    public int NodeCount => Nodes.GetLength(0);

    public int LineCount => From.Length;

    /// <summary>
    /// The graph, one connection per line that made one, weighted by the given
    /// weights or by the straight-line distance between its nodes when none are
    /// given. Two lines between the same two nodes are one connection, keeping the
    /// larger weight, which is what <see cref="WeightedGraph"/> does with any edge
    /// it is told twice.
    /// </summary>
    /// <param name="weights">One per line, or null for the length between the welded nodes.</param>
    public WeightedGraph Graph(IReadOnlyList<double>? weights = null)
    {
        if (weights is not null && weights.Count != LineCount)
            throw new ArgumentException($"{weights.Count} weights for {LineCount} lines; give one per line or none.", nameof(weights));

        var edges = new List<(int, int, double)>(LineCount);
        for (int i = 0; i < LineCount; i++)
        {
            if (From[i] == To[i])
                continue;

            double weight = weights is null ? Distance(From[i], To[i]) : weights[i];
            edges.Add((From[i], To[i], weight));
        }

        return WeightedGraph.FromEdges(NodeCount, edges);
    }

    private double Distance(int a, int b)
    {
        double dx = Nodes[a, 0] - Nodes[b, 0], dy = Nodes[a, 1] - Nodes[b, 1], dz = Nodes[a, 2] - Nodes[b, 2];
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}

/// <summary>
/// Welds the ends of drawn lines into the nodes of a graph, in three dimensions.
/// <para>
/// The most natural way a person draws a network is as lines — centrelines,
/// members, a street map — and nothing about turning those into nodes and
/// connections knows what the lines are. Two ends within the tolerance of each
/// other are one node; a point joins the nearest existing node within reach,
/// the earliest on a tie, so the numbering is a function of the input order
/// alone and never of a hash. A node sits where the first end that made it was,
/// not at an average, so a node's position is always one the user drew.
/// </para>
/// <para>
/// Plain coordinates, not geometry types, for the reason everything here is:
/// an adaptor fills the arrays from its lines, and the tests run with no Rhino.
/// </para>
/// </summary>
public static class LineNetwork
{
    /// <param name="starts">m x 3, the start of each line.</param>
    /// <param name="ends">m x 3, the end of each line.</param>
    /// <param name="tolerance">How far apart two ends may be and still be one node. The tolerance the lines were drawn to, not a machine epsilon.</param>
    public static LineNetworkResult Weld(double[,] starts, double[,] ends, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(starts);
        ArgumentNullException.ThrowIfNull(ends);
        if (!(tolerance > 0.0) || !double.IsFinite(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be above zero.");
        if (starts.GetLength(1) != 3 || ends.GetLength(1) != 3)
            throw new ArgumentException("Line ends must be m x 3, x, y, z.");
        if (starts.GetLength(0) != ends.GetLength(0))
            throw new ArgumentException($"{starts.GetLength(0)} starts for {ends.GetLength(0)} ends; every line needs both.");

        int m = starts.GetLength(0);
        var welder = new Welder(tolerance);
        var from = new int[m];
        var to = new int[m];
        var collapsed = new List<int>();

        for (int i = 0; i < m; i++)
        {
            from[i] = welder.Weld(starts[i, 0], starts[i, 1], starts[i, 2], i);
            to[i] = welder.Weld(ends[i, 0], ends[i, 1], ends[i, 2], i);
            if (from[i] == to[i])
                collapsed.Add(i);
        }

        return new LineNetworkResult(welder.Nodes(), from, to, collapsed.ToArray());
    }

    /// <summary>
    /// Points bucketed on a grid of cells one tolerance wide, so a new point checks
    /// the 27 cells around it rather than every node so far.
    /// </summary>
    private sealed class Welder
    {
        private readonly double _tolerance;
        private readonly List<(double X, double Y, double Z)> _nodes = new();
        private readonly Dictionary<(long, long, long), List<int>> _grid = new();

        public Welder(double tolerance) => _tolerance = tolerance;

        public int Weld(double x, double y, double z, int line)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                throw new ArgumentException($"Line {line} has an end that is not finite.");

            int existing = Find(x, y, z);
            if (existing >= 0)
                return existing;

            int node = _nodes.Count;
            _nodes.Add((x, y, z));

            var cell = Cell(x, y, z);
            if (!_grid.TryGetValue(cell, out var here))
                _grid[cell] = here = new List<int>();
            here.Add(node);

            return node;
        }

        public double[,] Nodes()
        {
            var nodes = new double[_nodes.Count, 3];
            for (int i = 0; i < _nodes.Count; i++)
            {
                nodes[i, 0] = _nodes[i].X;
                nodes[i, 1] = _nodes[i].Y;
                nodes[i, 2] = _nodes[i].Z;
            }

            return nodes;
        }

        private int Find(double x, double y, double z)
        {
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            var (cx, cy, cz) = Cell(x, y, z);

            for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
            for (long dz = -1; dz <= 1; dz++)
            {
                if (!_grid.TryGetValue((cx + dx, cy + dy, cz + dz), out var candidates))
                    continue;

                foreach (int node in candidates)
                {
                    var (nx, ny, nz) = _nodes[node];
                    double distance = Math.Sqrt((x - nx) * (x - nx) + (y - ny) * (y - ny) + (z - nz) * (z - nz));
                    if (distance <= _tolerance && (distance < bestDistance || (distance == bestDistance && node < best)))
                    {
                        best = node;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private (long, long, long) Cell(double x, double y, double z)
            => ((long)Math.Floor(x / _tolerance), (long)Math.Floor(y / _tolerance), (long)Math.Floor(z / _tolerance));
    }
}

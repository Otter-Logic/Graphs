namespace OtterLogic.Graphs.Planar;

/// <summary>A visibility graph and what its nodes are.</summary>
/// <param name="Graph">Every pair of nodes that can see each other, weighted by the distance between them.</param>
/// <param name="Nodes">
/// Position of every node, x then y: the caller's points first, in the order
/// given, then every obstacle corner. So node i is point i for as many points as
/// were given, and a caller never has to look its own points up.
/// </param>
/// <param name="PointCount">How many of the nodes are the caller's points; the rest are corners.</param>
/// <param name="Enclosed">Per node, whether it lies inside an obstacle. Such a node sees nothing and is left isolated.</param>
public sealed record VisibilityGraphResult(WeightedGraph Graph, double[,] Nodes, int PointCount, bool[] Enclosed);

/// <summary>
/// The graph whose shortest routes are the shortest ways through the open plane.
/// <para>
/// A shortest path among polygonal obstacles is straight wherever it can be and
/// turns only at obstacle corners. So the places worth being are the corners and
/// the caller's own points, the steps worth taking are the straight ones between
/// any two of those that nothing blocks, and <see cref="Dijkstra"/> over that graph
/// returns the true shortest path, not the best of whatever a grid or a scatter of
/// points happened to offer. It is also small: tens of nodes where a grid fine
/// enough to look smooth needs thousands.
/// </para>
/// <para>
/// Every corner is kept and every pair is tested, O(n² e) for n nodes and e
/// obstacle edges. The reductions in the literature — reflex corners only, tangent
/// steps only — make the graph smaller without changing a single shortest path,
/// and are left out until a drawing is large enough to need them: at a few hundred
/// corners this is already well inside a live Grasshopper solve.
/// </para>
/// </summary>
public static class VisibilityGraph
{
    /// <summary>Builds the graph over <paramref name="points"/> and every corner of <paramref name="obstacles"/>.</summary>
    /// <param name="points">n x 2, x then y: where routes start, end or must be able to pass. May be empty.</param>
    /// <param name="obstacles">What blocks the view.</param>
    public static VisibilityGraphResult Of(double[,] points, PlanarObstacles obstacles)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(obstacles);
        if (points.GetLength(1) != 2)
            throw new ArgumentException("Points must be an n x 2 array of x and y.", nameof(points));

        var corners = obstacles.Corners();
        int pointCount = points.GetLength(0);
        int n = pointCount + corners.GetLength(0);
        if (n < 1)
            throw new ArgumentException("There is nothing to build a graph from: no points and no obstacles.", nameof(points));

        var nodes = new double[n, 2];
        for (int i = 0; i < n; i++)
        {
            var from = i < pointCount ? points : corners;
            int row = i < pointCount ? i : i - pointCount;
            nodes[i, 0] = from[row, 0];
            nodes[i, 1] = from[row, 1];
            if (!double.IsFinite(nodes[i, 0]) || !double.IsFinite(nodes[i, 1]))
                throw new ArgumentException($"Point {row} is not finite.", nameof(points));
        }

        // A corner can be enclosed too, where two obstacles overlap.
        var enclosed = new bool[n];
        for (int i = 0; i < n; i++)
            enclosed[i] = obstacles.Contains(nodes[i, 0], nodes[i, 1]);

        var edges = new List<(int, int, double)>();
        for (int a = 0; a < n; a++)
        {
            if (enclosed[a])
                continue;

            for (int b = a + 1; b < n; b++)
            {
                if (enclosed[b] || obstacles.Blocks(nodes[a, 0], nodes[a, 1], nodes[b, 0], nodes[b, 1]))
                    continue;

                double dx = nodes[b, 0] - nodes[a, 0], dy = nodes[b, 1] - nodes[a, 1];
                edges.Add((a, b, Math.Sqrt(dx * dx + dy * dy)));
            }
        }

        return new VisibilityGraphResult(WeightedGraph.FromEdges(n, edges), nodes, pointCount, enclosed);
    }
}

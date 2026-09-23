namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Everything a graph method is handed: the graph, where its nodes are when that
/// is known, and the nodes the question is about.
/// <para>
/// The data side of one wire. A user builds a graph, names the nodes a route
/// should start from and end at, and wires in whichever method they have looked
/// up; the method reads what it needs from here and ignores the rest. Sources and
/// Targets are the only two questions a person asks of a network — from where,
/// to where — and each method says what it makes of them: Dijkstra routes from
/// the sources to the targets, Potential Flow drains everything at the targets,
/// Betweenness wants neither.
/// </para>
/// <para>
/// Positions are three columns, x, y, z, or absent. Graphs knows nothing of
/// geometry, so they arrive as plain numbers an adaptor filled in; what they buy
/// is the straight-line estimate A* steers by, and the choice of running it in
/// three dimensions or in plan, which is decided here rather than in an adaptor
/// because it is arithmetic on the graph's own weights.
/// </para>
/// <para>
/// One type for both kinds of graph, so a method's signature does not force a
/// user to know which they have. Reading an undirected graph as directed loses
/// nothing; reading a directed one as undirected throws direction away, and
/// <see cref="Undirected"/> says whether it had to so the method can say so too.
/// </para>
/// </summary>
public sealed class PathQuery
{
    private readonly WeightedGraph? _undirected;
    private readonly DirectedGraph? _directed;

    /// <param name="graph">The graph, connections travelled either way.</param>
    /// <param name="positions">n x 3 node positions, or null when the graph has none.</param>
    /// <param name="sources">Nodes the question starts from. Null or empty for none.</param>
    /// <param name="targets">Nodes the question ends at. Null or empty for none.</param>
    public PathQuery(WeightedGraph graph, double[,]? positions = null, IEnumerable<int>? sources = null, IEnumerable<int>? targets = null)
    {
        _undirected = graph ?? throw new ArgumentNullException(nameof(graph));
        Positions = CheckPositions(positions, graph.NodeCount);
        Sources = CheckNodes(sources, graph.NodeCount, nameof(sources), "Source");
        Targets = CheckNodes(targets, graph.NodeCount, nameof(targets), "Target");
    }

    /// <param name="graph">The graph, each arc travelled from its tail to its head only.</param>
    /// <param name="positions">n x 3 node positions, or null when the graph has none.</param>
    /// <param name="sources">Nodes the question starts from. Null or empty for none.</param>
    /// <param name="targets">Nodes the question ends at. Null or empty for none.</param>
    public PathQuery(DirectedGraph graph, double[,]? positions = null, IEnumerable<int>? sources = null, IEnumerable<int>? targets = null)
    {
        _directed = graph ?? throw new ArgumentNullException(nameof(graph));
        Positions = CheckPositions(positions, graph.NodeCount);
        Sources = CheckNodes(sources, graph.NodeCount, nameof(sources), "Source");
        Targets = CheckNodes(targets, graph.NodeCount, nameof(targets), "Target");
    }

    /// <summary>One row per node, x, y, z; null for a graph with no positions.</summary>
    public double[,]? Positions { get; }

    /// <summary>Where the question starts, each node once, ascending. Empty for none.</summary>
    public int[] Sources { get; }

    /// <summary>Where the question ends, each node once, ascending. Empty for none.</summary>
    public int[] Targets { get; }

    public bool IsDirected => _directed is not null;

    public bool HasPositions => Positions is not null;

    public int NodeCount => _directed?.NodeCount ?? _undirected!.NodeCount;

    /// <summary>Edges of an undirected graph; arcs of a directed one, an arc and its reverse counted separately.</summary>
    public int ConnectionCount => _directed?.ArcCount ?? _undirected!.EdgeCount;

    /// <summary>The directed graph, or null when this holds an undirected one.</summary>
    public DirectedGraph? DirectedOrNull => _directed;

    /// <summary>The undirected graph, or null when this holds a directed one.</summary>
    public WeightedGraph? UndirectedOrNull => _undirected;

    /// <summary>The graph as arcs. Lossless: an undirected edge becomes an arc each way.</summary>
    public DirectedGraph Directed() => _directed ?? _undirected!.ToDirected();

    /// <summary>
    /// The graph with direction forgotten, for a method only defined on an
    /// undirected one. Where an arc and its reverse disagreed the larger weight is
    /// kept, which is what <see cref="WeightedGraph"/> does with any edge it is told twice.
    /// </summary>
    /// <param name="lostDirection">Whether direction was thrown away to answer.</param>
    public WeightedGraph Undirected(out bool lostDirection)
    {
        lostDirection = _directed is not null;
        return _undirected ?? _directed!.ToUndirected(DuplicateArcs.KeepLargest);
    }

    /// <summary>
    /// Every connection in the order every per-connection answer follows: edges once
    /// each, lower node first; arcs by tail then head, which is arc-id order.
    /// </summary>
    public IEnumerable<(int A, int B, double Weight)> Connections()
        => _directed is not null ? _directed.Arcs() : _undirected!.Edges();

    /// <summary>Straight-line distance between two nodes, or NaN without positions.</summary>
    public double Distance(int a, int b)
    {
        if (Positions is null)
            return double.NaN;

        double dx = Positions[a, 0] - Positions[b, 0];
        double dy = Positions[a, 1] - Positions[b, 1];
        double dz = Positions[a, 2] - Positions[b, 2];
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Straight-line distance between two nodes in plan, ignoring z, or NaN without positions.</summary>
    public double DistanceInPlan(int a, int b)
    {
        if (Positions is null)
            return double.NaN;

        double dx = Positions[a, 0] - Positions[b, 0];
        double dy = Positions[a, 1] - Positions[b, 1];
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The same question with different ends, over the same graph and positions.</summary>
    public PathQuery With(IEnumerable<int>? sources, IEnumerable<int>? targets)
        => _directed is not null
            ? new PathQuery(_directed, Positions, sources, targets)
            : new PathQuery(_undirected!, Positions, sources, targets);

    // ---- what a method asks for, in the words a user reads when it is missing ----

    /// <summary>Throws unless at least one source was given.</summary>
    public void RequireSources(string method)
    {
        if (Sources.Length == 0)
            throw new ArgumentException($"{method} needs at least one source: the node the routes start from.");
    }

    /// <summary>Throws unless at least one target was given.</summary>
    public void RequireTargets(string method, string role)
    {
        if (Targets.Length == 0)
            throw new ArgumentException($"{method} needs at least one target: {role}.");
    }

    /// <summary>Throws unless exactly one source and one target were given.</summary>
    public void RequireOneToOne(string method)
    {
        if (Sources.Length != 1 || Targets.Length != 1)
            throw new ArgumentException(
                $"{method} routes from one source to one target; it was given {Sources.Length} source(s) and "
                + $"{Targets.Length} target(s). For several, or for the cost to every node, use Dijkstra.");
    }

    /// <summary>Throws unless the graph has positions.</summary>
    public void RequirePositions(string method)
    {
        if (Positions is null)
            throw new ArgumentException(
                $"{method} needs to know where the nodes are, and this graph has no positions. Build it from "
                + "points or lines, or wire Points into Graph From Connectivity.");
    }

    /// <summary>Throws unless the graph is directed.</summary>
    public void RequireDirected(string method, string why)
    {
        if (_directed is null)
            throw new ArgumentException($"{method} needs a directed graph: {why}");
    }

    private static double[,]? CheckPositions(double[,]? positions, int nodeCount)
    {
        if (positions is null)
            return null;

        if (positions.GetLength(0) != nodeCount || positions.GetLength(1) != 3)
            throw new ArgumentException(
                $"Positions must be {nodeCount} x 3 — one row per node, x, y, z; got "
                + $"{positions.GetLength(0)} x {positions.GetLength(1)}.", nameof(positions));

        for (int i = 0; i < nodeCount; i++)
            for (int k = 0; k < 3; k++)
                if (!double.IsFinite(positions[i, k]))
                    throw new ArgumentException($"Node {i} has a position that is not finite.", nameof(positions));

        return positions;
    }

    private static int[] CheckNodes(IEnumerable<int>? nodes, int nodeCount, string parameter, string role)
    {
        if (nodes is null)
            return Array.Empty<int>();

        var distinct = nodes.Distinct().OrderBy(i => i).ToArray();
        foreach (int i in distinct)
            if (i < 0 || i >= nodeCount)
                throw new ArgumentOutOfRangeException(parameter, i, $"{role} {i} is outside 0..{nodeCount - 1}.");

        return distinct;
    }
}

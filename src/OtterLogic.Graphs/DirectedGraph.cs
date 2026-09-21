namespace OtterLogic.Graphs;

/// <summary>What <see cref="DirectedGraph"/> makes of the same arc given twice with two different weights.</summary>
/// <remarks>
/// There is no default, deliberately. <see cref="WeightedGraph"/> can keep the larger
/// without asking because a repeat there is nearly always one edge reported from both
/// ends. A repeated arc is a genuine second statement, and what it should mean
/// depends on what the weight is: the cheaper of two roads is the one a route takes,
/// while two pipes side by side carry the sum.
/// </remarks>
public enum DuplicateArcs
{
    /// <summary>The smaller weight — right when the weight is a cost.</summary>
    KeepSmallest,

    /// <summary>The larger weight — what <see cref="WeightedGraph"/> does with a repeated edge.</summary>
    KeepLargest,

    /// <summary>The total — right when the weight is a capacity.</summary>
    Sum,
}

/// <summary>
/// A graph whose connections run one way: an arc from a tail to a head, with a
/// weight on it.
/// <para>
/// A type of its own rather than a switch on <see cref="WeightedGraph"/>, because
/// half of what runs on that graph is only true of an undirected one — the
/// Laplacian <see cref="PotentialFlow"/> solves is symmetric, a cut vertex is
/// defined by edges that run both ways, spectral clustering wants a symmetric
/// operator. With a switch each of those would have to check it and throw. With two
/// types a signature says which it needs, and the mistake does not compile.
/// </para>
/// <para>
/// Three things differ from <see cref="WeightedGraph"/>, all on purpose. An arc and
/// its reverse are two arcs, which is the point. A zero weight is kept: a free
/// transfer, a dependency that takes no time and a dummy arc in a schedule are all
/// real arcs, where a zero similarity is no relationship at all. And any finite
/// weight is allowed, negative included — a longest path over a schedule wants
/// them, and <see cref="Dijkstra"/>, which does not, already refuses them itself.
/// </para>
/// <para>
/// Stored as sorted sparse rows twice over, out-arcs and in-arcs. The second copy
/// is what makes "what depends on this" as cheap as "what does this depend on",
/// and it is built once because the graph never changes. Every arc has an id — its
/// position in tail-then-head order — so that anything else known per arc, a
/// capacity, a flow, a duration, lives in a plain array beside the graph rather
/// than in it.
/// </para>
/// </summary>
public sealed class DirectedGraph : IAdjacency
{
    private readonly int[] _offsets;
    private readonly int[] _heads;
    private readonly double[] _weights;

    private readonly int[] _inOffsets;
    private readonly int[] _tails;
    private readonly int[] _inArcs;

    private DirectedGraph(int nodeCount, int[] offsets, int[] heads, double[] weights)
    {
        NodeCount = nodeCount;
        _offsets = offsets;
        _heads = heads;
        _weights = weights;

        // The reverse rows. Walking arcs in id order fills each row in ascending
        // tail order, so they come out sorted without a second pass.
        _inOffsets = new int[nodeCount + 1];
        foreach (int head in heads)
            _inOffsets[head + 1]++;
        for (int i = 0; i < nodeCount; i++)
            _inOffsets[i + 1] += _inOffsets[i];

        _tails = new int[heads.Length];
        _inArcs = new int[heads.Length];
        var cursor = (int[])_inOffsets.Clone();
        for (int tail = 0; tail < nodeCount; tail++)
        {
            for (int arc = offsets[tail]; arc < offsets[tail + 1]; arc++)
            {
                int slot = cursor[heads[arc]]++;
                _tails[slot] = tail;
                _inArcs[slot] = arc;
            }
        }
    }

    /// <summary>Number of nodes.</summary>
    public int NodeCount { get; }

    /// <summary>Number of arcs. An arc and its reverse count separately.</summary>
    public int ArcCount => _heads.Length;

    /// <summary>Builds a graph from unweighted arcs; every arc gets weight one and a repeat is one arc.</summary>
    /// <param name="nodeCount">Number of nodes. Nodes on no arc are allowed.</param>
    /// <param name="arcs">Tail and head of each arc.</param>
    public static DirectedGraph FromArcs(int nodeCount, IEnumerable<(int From, int To)> arcs)
    {
        ArgumentNullException.ThrowIfNull(arcs);
        return FromArcs(nodeCount, arcs.Select(a => (a.From, a.To, 1.0)), DuplicateArcs.KeepLargest);
    }

    /// <summary>
    /// Builds a graph from weighted arcs.
    /// <para>
    /// A self-loop is dropped, as it is in <see cref="WeightedGraph"/>: nothing here
    /// routes, ranks or flows round one. A repeated arc is folded by
    /// <paramref name="duplicates"/>. Nothing else is normalised away — zero and
    /// negative weights are kept.
    /// </para>
    /// </summary>
    /// <param name="nodeCount">Number of nodes. Nodes on no arc are allowed.</param>
    /// <param name="arcs">Tail, head and finite weight of each arc.</param>
    /// <param name="duplicates">What a repeated arc means.</param>
    public static DirectedGraph FromArcs(
        int nodeCount, IEnumerable<(int From, int To, double Weight)> arcs, DuplicateArcs duplicates)
    {
        ArgumentNullException.ThrowIfNull(arcs);
        if (nodeCount < 1)
            throw new ArgumentOutOfRangeException(nameof(nodeCount), nodeCount, "A graph needs at least one node.");

        var unique = new Dictionary<long, double>();

        foreach (var (from, to, weight) in arcs)
        {
            if (from < 0 || from >= nodeCount || to < 0 || to >= nodeCount)
                throw new ArgumentOutOfRangeException(nameof(arcs),
                    $"Arc ({from}, {to}) refers to a node outside 0..{nodeCount - 1}.");
            if (!double.IsFinite(weight))
                throw new ArgumentOutOfRangeException(nameof(arcs),
                    $"Arc ({from}, {to}) has weight {weight}; weights must be finite.");

            if (from == to)
                continue;

            long key = (long)from * nodeCount + to;
            unique[key] = !unique.TryGetValue(key, out double existing)
                ? weight
                : duplicates switch
                {
                    DuplicateArcs.KeepSmallest => Math.Min(existing, weight),
                    DuplicateArcs.KeepLargest => Math.Max(existing, weight),
                    DuplicateArcs.Sum => existing + weight,
                    _ => throw new ArgumentOutOfRangeException(nameof(duplicates), duplicates, null),
                };
        }

        var offsets = new int[nodeCount + 1];
        foreach (long key in unique.Keys)
            offsets[(int)(key / nodeCount) + 1]++;
        for (int i = 0; i < nodeCount; i++)
            offsets[i + 1] += offsets[i];

        var heads = new int[unique.Count];
        var weights = new double[unique.Count];

        // Ascending keys are tail-then-head order, which is arc-id order.
        int arc = 0;
        foreach (var (key, weight) in unique.OrderBy(pair => pair.Key))
        {
            heads[arc] = (int)(key % nodeCount);
            weights[arc++] = weight;
        }

        return new DirectedGraph(nodeCount, offsets, heads, weights);
    }

    /// <summary>Heads of the arcs leaving <paramref name="node"/>, ascending.</summary>
    public ReadOnlySpan<int> Successors(int node)
        => _heads.AsSpan(_offsets[node], _offsets[node + 1] - _offsets[node]);

    /// <summary>Weights of the arcs to <see cref="Successors"/>, in the same order.</summary>
    public ReadOnlySpan<double> ArcWeights(int node)
        => _weights.AsSpan(_offsets[node], _offsets[node + 1] - _offsets[node]);

    /// <summary>
    /// Id of the first arc leaving <paramref name="node"/>. The arc to
    /// <c>Successors(node)[k]</c> has id <c>FirstArc(node) + k</c>, which is where
    /// anything else known about that arc sits in the caller's own arrays.
    /// </summary>
    public int FirstArc(int node) => _offsets[node];

    /// <summary>Tails of the arcs arriving at <paramref name="node"/>, ascending.</summary>
    public ReadOnlySpan<int> Predecessors(int node)
        => _tails.AsSpan(_inOffsets[node], _inOffsets[node + 1] - _inOffsets[node]);

    /// <summary>Ids of the arcs from <see cref="Predecessors"/>, in the same order.</summary>
    public ReadOnlySpan<int> PredecessorArcs(int node)
        => _inArcs.AsSpan(_inOffsets[node], _inOffsets[node + 1] - _inOffsets[node]);

    /// <summary>Weight of the arc with id <paramref name="arc"/>.</summary>
    public double Weight(int arc) => _weights[arc];

    /// <summary>Every arc, in id order: by tail, then by head.</summary>
    public IEnumerable<(int From, int To, double Weight)> Arcs()
    {
        for (int tail = 0; tail < NodeCount; tail++)
            for (int arc = _offsets[tail]; arc < _offsets[tail + 1]; arc++)
                yield return (tail, _heads[arc], _weights[arc]);
    }

    /// <summary>
    /// The same connections with their direction forgotten.
    /// <para>
    /// Lossy, which is why it asks: an arc and its reverse become one edge, and
    /// <paramref name="both"/> says what that edge weighs when they disagreed. It
    /// also inherits <see cref="WeightedGraph"/>'s rules on the way in, so the
    /// result must have weights that are not negative, and an edge that comes out
    /// at zero is dropped.
    /// </para>
    /// </summary>
    public WeightedGraph ToUndirected(DuplicateArcs both)
    {
        var merged = new Dictionary<long, double>(ArcCount);
        foreach (var (from, to, weight) in Arcs())
        {
            long key = (long)Math.Min(from, to) * NodeCount + Math.Max(from, to);
            merged[key] = !merged.TryGetValue(key, out double existing)
                ? weight
                : both switch
                {
                    DuplicateArcs.KeepSmallest => Math.Min(existing, weight),
                    DuplicateArcs.KeepLargest => Math.Max(existing, weight),
                    DuplicateArcs.Sum => existing + weight,
                    _ => throw new ArgumentOutOfRangeException(nameof(both), both, null),
                };
        }

        return WeightedGraph.FromEdges(NodeCount,
            merged.Select(edge => ((int)(edge.Key / NodeCount), (int)(edge.Key % NodeCount), edge.Value)));
    }

    ReadOnlySpan<int> IAdjacency.Next(int node) => Successors(node);
    ReadOnlySpan<double> IAdjacency.NextWeights(int node) => ArcWeights(node);
}

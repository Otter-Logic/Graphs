namespace OtterLogic.Graphs;

/// <summary>
/// Dijkstra's algorithm over a <see cref="WeightedGraph"/>, from one source or
/// several at once.
/// <para>
/// Named for the algorithm rather than for the question, because several
/// algorithms answer this question and a caller chooses between them:
/// <see cref="BreadthFirst"/> when every step costs the same, this when steps have
/// costs. They share <see cref="RouteTree"/>, so swapping one for another changes a
/// class name and nothing downstream.
/// </para>
/// <para>
/// Several sources cost nothing extra and are not a special case: every source
/// starts at zero and each node goes to whichever reaches it first. One source is
/// a list of one. It is also what makes "the nearest exit" a single solve rather
/// than one per exit.
/// </para>
/// <para>
/// The graph's weights say how strongly two nodes are related; a route's cost is
/// a different question, so the caller supplies it per edge rather than having it
/// read off the weight. One graph then serves both: a clustering reads its
/// similarities, and a route search reads whatever cost the caller's domain
/// gives the same edge — length, a penalty for climbing, anything non-negative.
/// </para>
/// <para>
/// Ties go to the lower-numbered source, then the lower-numbered previous node,
/// so the routes are a property of the graph and the costs alone, never of the
/// order the queue happened to pop equal costs in. That matters for the same
/// reason every other graph algorithm here sorts its neighbours: an adaptor
/// re-solves on every upstream change, and a route that flips between two equal
/// choices from one solve to the next is unusable.
/// </para>
/// </summary>
public static class Dijkstra
{
    /// <summary>
    /// Cheapest routes from every node in <paramref name="sources"/> to every node.
    /// </summary>
    /// <param name="graph">The graph to route over. Edges are traversable both ways.</param>
    /// <param name="sources">Nodes a route may start from, each at cost zero. Duplicates are ignored.</param>
    /// <param name="cost">
    /// Cost of moving from <c>from</c> to <c>to</c> along an edge of weight <c>weight</c>:
    /// finite and not negative, or positive infinity for an edge that may not be used in
    /// that direction. Null for the weight itself.
    /// </param>
    /// <param name="targets">
    /// Optional. The only nodes the caller wants a route to. The search stops once
    /// every one that can be reached has been, and the result then reports
    /// <em>only</em> what was settled by that point — the targets and whatever was
    /// nearer than the furthest of them. Anything else reads as unreached, because
    /// its cost at the moment of stopping was a bound and not an answer, and a
    /// bound that looks like an answer is worse than none. Null or empty searches
    /// the whole graph.
    /// </param>
    public static RouteTree From(
        WeightedGraph graph, IEnumerable<int> sources,
        Func<int, int, double, double>? cost = null, IEnumerable<int>? targets = null)
        => Search(graph, sources, cost, targets);

    /// <summary>
    /// The same search along arcs, each travelled from its tail to its head only.
    /// <para>
    /// One loop serves both graphs, so a one-way street changes which steps exist
    /// and nothing about how ties are broken or what an early stop reports. A
    /// negative arc weight is refused here as it is for an edge: Dijkstra's
    /// argument for stopping early does not survive one.
    /// </para>
    /// </summary>
    public static RouteTree From(
        DirectedGraph graph, IEnumerable<int> sources,
        Func<int, int, double, double>? cost = null, IEnumerable<int>? targets = null)
        => Search(graph, sources, cost, targets);

    private static RouteTree Search<TGraph>(
        TGraph graph, IEnumerable<int> sources,
        Func<int, int, double, double>? cost, IEnumerable<int>? targets)
        where TGraph : class, IAdjacency
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(sources);

        int n = graph.NodeCount;

        bool[]? wanted = null;
        int outstanding = 0;
        if (targets is not null)
        {
            foreach (int t in targets)
            {
                if (t < 0 || t >= n)
                    throw new ArgumentOutOfRangeException(nameof(targets), t, $"Target {t} is outside 0..{n - 1}.");

                wanted ??= new bool[n];
                if (!wanted[t])
                {
                    wanted[t] = true;
                    outstanding++;
                }
            }
        }

        var best = new double[n];
        var source = new int[n];
        var previous = new int[n];
        Array.Fill(best, double.PositiveInfinity);
        Array.Fill(source, -1);
        Array.Fill(previous, -1);

        var queue = new PriorityQueue<int, (double Cost, int Source, int Node)>();

        foreach (int s in sources.Distinct().OrderBy(s => s))
        {
            if (s < 0 || s >= n)
                throw new ArgumentOutOfRangeException(nameof(sources), s, $"Source {s} is outside 0..{n - 1}.");

            best[s] = 0.0;
            source[s] = s;
            queue.Enqueue(s, (0.0, s, s));
        }

        var settled = new bool[n];
        while (queue.TryDequeue(out int node, out var key))
        {
            if (settled[node] || key.Cost > best[node] || key.Source != source[node])
                continue;

            settled[node] = true;
            if (wanted is not null && wanted[node] && --outstanding == 0)
                break;

            var neighbours = graph.Next(node);
            var weights = graph.NextWeights(node);

            for (int e = 0; e < neighbours.Length; e++)
            {
                int next = neighbours[e];
                if (settled[next])
                    continue;

                double step = cost is null ? weights[e] : cost(node, next, weights[e]);
                if (double.IsNaN(step) || step < 0.0 || double.IsNegativeInfinity(step))
                    throw new InvalidOperationException(
                        $"The cost of edge ({node}, {next}) is {step}; costs must be non-negative, or positive infinity to forbid it.");
                if (double.IsPositiveInfinity(step))
                    continue;

                double through = best[node] + step;
                bool better = through < best[next]
                    || (through == best[next] && (source[node] < source[next]
                        || (source[node] == source[next] && node < previous[next])));

                if (!better)
                    continue;

                best[next] = through;
                source[next] = source[node];
                previous[next] = node;
                queue.Enqueue(next, (through, source[node], next));
            }
        }

        // Stopped early: whatever was still in the queue holds a bound, not an answer.
        if (wanted is not null && outstanding == 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (settled[i])
                    continue;

                best[i] = double.PositiveInfinity;
                source[i] = -1;
                previous[i] = -1;
            }
        }

        return new RouteTree(best, source, previous);
    }
}

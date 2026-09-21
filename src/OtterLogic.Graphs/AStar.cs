namespace OtterLogic.Graphs;

/// <summary>
/// A* search: the cheapest route between two nodes, found by looking towards the
/// target instead of evenly in every direction.
/// <para>
/// <see cref="Dijkstra"/> settles nodes in order of what they cost to reach, and so
/// explores a disc around the source until the disc happens to touch the target.
/// This orders them by that cost <em>plus an estimate of what is left</em>, so nodes
/// in the wrong direction wait at the back of the queue and mostly never get looked
/// at. The route is the same one. What changes is how much of the graph is
/// examined on the way to it, which on a street network or a fine grid is the
/// difference between a few hundred nodes and all of them.
/// </para>
/// <para>
/// The estimate is the caller's, because this layer has no idea where a node is:
/// a function from a node to a lower bound on its remaining cost, straight-line
/// distance being the usual one. <b>It must never overestimate.</b> One that does
/// still returns a route, quickly, and it may not be the cheapest. One that is zero
/// everywhere turns this into Dijkstra. Between those, the closer it sits under the
/// truth the less of the graph is opened.
/// </para>
/// <para>
/// A node is looked at again if a cheaper way to it turns up after it was first
/// expanded. With an estimate that is <em>consistent</em> — never dropping by more
/// than the step it is dropping across, as straight-line distance cannot when
/// steps weigh at least their own length — that never happens and costs nothing.
/// With one that is merely never too high it can, and re-opening is what keeps the
/// answer right in that case rather than only in the textbook one.
/// </para>
/// <para>
/// One source and one target, unlike <see cref="Dijkstra"/>. The estimate points
/// at a target, so there is no "to everywhere" and no "from the nearest of
/// several"; both of those are Dijkstra's. Only the route found is reported — what
/// was opened along the way holds bounds, not answers.
/// </para>
/// </summary>
public static class AStar
{
    /// <summary>The cheapest route from <paramref name="source"/> to <paramref name="target"/>.</summary>
    /// <param name="graph">The graph to route over. Edges are traversable both ways.</param>
    /// <param name="source">Where the route starts.</param>
    /// <param name="target">Where it ends.</param>
    /// <param name="estimate">
    /// A lower bound on the cost from a node to <paramref name="target"/>: finite,
    /// not negative, never more than the true remaining cost. Asked once per node.
    /// </param>
    /// <param name="cost">
    /// Cost of moving from <c>from</c> to <c>to</c> along a step of weight <c>weight</c>,
    /// exactly as <see cref="Dijkstra"/> takes it. Null for the weight itself.
    /// </param>
    /// <returns>
    /// Routes reporting the nodes on the route found and nothing else. Every other
    /// node reads as unreached, the target too when no route exists.
    /// </returns>
    public static RouteTree Route(
        WeightedGraph graph, int source, int target,
        Func<int, double> estimate, Func<int, int, double, double>? cost = null)
        => Search(graph, source, target, estimate, cost, out _);

    /// <summary>The same, also saying which nodes were expanded, in order — what the search actually looked at.</summary>
    public static RouteTree Route(
        WeightedGraph graph, int source, int target,
        Func<int, double> estimate, Func<int, int, double, double>? cost, out int[] expanded)
        => Search(graph, source, target, estimate, cost, out expanded);

    /// <summary>The same search along arcs, each travelled from its tail to its head only.</summary>
    public static RouteTree Route(
        DirectedGraph graph, int source, int target,
        Func<int, double> estimate, Func<int, int, double, double>? cost = null)
        => Search(graph, source, target, estimate, cost, out _);

    /// <summary>The same along arcs, also saying which nodes were expanded, in order.</summary>
    public static RouteTree Route(
        DirectedGraph graph, int source, int target,
        Func<int, double> estimate, Func<int, int, double, double>? cost, out int[] expanded)
        => Search(graph, source, target, estimate, cost, out expanded);

    private static RouteTree Search<TGraph>(
        TGraph graph, int source, int target,
        Func<int, double> estimate, Func<int, int, double, double>? cost, out int[] expanded)
        where TGraph : class, IAdjacency
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(estimate);

        int n = graph.NodeCount;
        if (source < 0 || source >= n)
            throw new ArgumentOutOfRangeException(nameof(source), source, $"Source {source} is outside 0..{n - 1}.");
        if (target < 0 || target >= n)
            throw new ArgumentOutOfRangeException(nameof(target), target, $"Target {target} is outside 0..{n - 1}.");

        var best = new double[n];
        var previous = new int[n];
        var remaining = new double[n];
        var closed = new bool[n];
        Array.Fill(best, double.PositiveInfinity);
        Array.Fill(previous, -1);
        Array.Fill(remaining, double.NaN);

        double Remaining(int node)
        {
            if (!double.IsNaN(remaining[node]))
                return remaining[node];

            double value = estimate(node);
            if (!double.IsFinite(value) || value < 0.0)
                throw new InvalidOperationException(
                    $"The estimate for node {node} is {value}; estimates must be finite and not negative.");

            return remaining[node] = value;
        }

        // Ordered by cost so far plus estimate, then by node, so that which of two
        // equally promising nodes goes first is the graph's doing and not the queue's.
        var queue = new PriorityQueue<int, (double Total, int Node)>();
        var order = new List<int>();

        best[source] = 0.0;
        queue.Enqueue(source, (Remaining(source), source));

        bool found = false;
        while (queue.TryDequeue(out int node, out var key))
        {
            // A stale entry: the node was reached more cheaply after this was queued.
            if (key.Total > best[node] + remaining[node])
                continue;

            // The same cost queued twice, from two ways in that tied.
            if (closed[node])
                continue;

            closed[node] = true;
            order.Add(node);
            if (node == target)
            {
                found = true;
                break;
            }

            var next = graph.Next(node);
            var weights = graph.NextWeights(node);

            for (int e = 0; e < next.Length; e++)
            {
                int to = next[e];
                double step = cost is null ? weights[e] : cost(node, to, weights[e]);
                if (double.IsNaN(step) || step < 0.0)
                    throw new InvalidOperationException(
                        $"The cost of step ({node}, {to}) is {step}; costs must be non-negative, or positive infinity to forbid it.");
                if (double.IsPositiveInfinity(step))
                    continue;

                double through = best[node] + step;
                if (through < best[to])
                {
                    best[to] = through;
                    previous[to] = node;
                    closed[to] = false;
                    queue.Enqueue(to, (through + Remaining(to), to));
                }
                else if (through == best[to] && node < previous[to] && !closed[to])
                {
                    // An equal way in through a lower-numbered node: same cost, so
                    // nothing to re-queue, but the route should not depend on which
                    // of the two was met first. Never once the node has been expanded:
                    // across free steps that is how two nodes end up each other's
                    // previous, and a route that goes round for ever.
                    previous[to] = node;
                }
            }
        }

        expanded = order.ToArray();

        var routeCost = new double[n];
        var from = new int[n];
        var before = new int[n];
        Array.Fill(routeCost, double.PositiveInfinity);
        Array.Fill(from, -1);
        Array.Fill(before, -1);

        if (found)
        {
            for (int at = target; at >= 0; at = previous[at])
            {
                routeCost[at] = best[at];
                from[at] = source;
                before[at] = previous[at];
            }
        }

        return new RouteTree(routeCost, from, before);
    }
}

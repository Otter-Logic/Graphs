namespace OtterLogic.Graphs;

/// <summary>
/// Breadth-first search over a <see cref="WeightedGraph"/>: routes with the fewest
/// steps, from one source or several at once. Weights are ignored.
/// <para>
/// The answer is exactly <see cref="Dijkstra"/>'s with every step costing one, and
/// the tests hold it to that node for node. It exists separately because it needs
/// no priority queue — O(n + e) against O(e log n) — and because counting steps is
/// a different question from summing costs that callers ask for by name: rings
/// around a node, how many joints from a support, the generation of each face
/// outward from a seed.
/// </para>
/// <para>
/// Ties go the way Dijkstra's do, to the lower-numbered source and then the
/// lower-numbered previous node, so the routes are a property of the graph and
/// never of the order a queue happened to be filled in.
/// </para>
/// </summary>
public static class BreadthFirst
{
    /// <summary>Fewest-step routes from every node in <paramref name="sources"/> to every node.</summary>
    /// <param name="graph">The graph to search. Edges are traversable both ways.</param>
    /// <param name="sources">Nodes a route may start from, each at zero steps. Duplicates are ignored.</param>
    /// <returns>Routes whose <see cref="RouteTree.Cost"/> is a whole number of steps.</returns>
    public static RouteTree From(WeightedGraph graph, IEnumerable<int> sources)
        => Search(graph, sources);

    /// <summary>The same search along arcs, each stepped from its tail to its head only.</summary>
    public static RouteTree From(DirectedGraph graph, IEnumerable<int> sources)
        => Search(graph, sources);

    private static RouteTree Search<TGraph>(TGraph graph, IEnumerable<int> sources)
        where TGraph : class, IAdjacency
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(sources);

        int n = graph.NodeCount;
        var steps = new double[n];
        var source = new int[n];
        var previous = new int[n];
        Array.Fill(steps, double.PositiveInfinity);
        Array.Fill(source, -1);
        Array.Fill(previous, -1);

        var level = new List<int>();
        foreach (int s in sources.Distinct().OrderBy(s => s))
        {
            if (s < 0 || s >= n)
                throw new ArgumentOutOfRangeException(nameof(sources), s, $"Source {s} is outside 0..{n - 1}.");

            steps[s] = 0.0;
            source[s] = s;
            level.Add(s);
        }

        // A level at a time, so that every node of one level has its source settled
        // before any of them is used to decide a tie in the next.
        var next = new List<int>();
        for (double depth = 1.0; level.Count > 0; depth++)
        {
            next.Clear();

            foreach (int node in level)
            {
                foreach (int neighbour in graph.Next(node))
                {
                    if (double.IsPositiveInfinity(steps[neighbour]))
                    {
                        steps[neighbour] = depth;
                        source[neighbour] = source[node];
                        previous[neighbour] = node;
                        next.Add(neighbour);
                    }
                    else if (steps[neighbour] == depth
                        && (source[node] < source[neighbour]
                            || (source[node] == source[neighbour] && node < previous[neighbour])))
                    {
                        source[neighbour] = source[node];
                        previous[neighbour] = node;
                    }
                }
            }

            (level, next) = (next, level);
        }

        return new RouteTree(steps, source, previous);
    }
}

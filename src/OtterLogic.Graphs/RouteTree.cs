namespace OtterLogic.Graphs;

/// <summary>
/// Cheapest routes from a set of source nodes: what each node's route costs, which
/// source it starts from, and the node before this one on it.
/// <para>
/// One result for every search that answers this question —
/// <see cref="Dijkstra"/> over costs, <see cref="BreadthFirst"/> over hops — so a
/// caller that reads a route does not care which found it. It is a tree, or one
/// tree per source: following <see cref="Previous"/> from any node leads back to
/// its source and never round a loop.
/// </para>
/// </summary>
/// <param name="Cost">Cost of the cheapest route to each node; positive infinity where no route exists.</param>
/// <param name="Source">The source the cheapest route to each node starts from; -1 where no route exists.</param>
/// <param name="Previous">The node before each node on its cheapest route; -1 at a source and where no route exists.</param>
public sealed record RouteTree(double[] Cost, int[] Source, int[] Previous)
{
    /// <summary>Whether any source reaches <paramref name="node"/>.</summary>
    public bool Reaches(int node) => Source[node] >= 0;

    /// <summary>
    /// The cheapest route to <paramref name="node"/>, from its source to the node
    /// itself. Empty where no route exists.
    /// </summary>
    public int[] RouteTo(int node)
    {
        if (!Reaches(node))
            return Array.Empty<int>();

        var route = new List<int>();
        for (int at = node; at >= 0; at = Previous[at])
            route.Add(at);

        route.Reverse();
        return route.ToArray();
    }
}

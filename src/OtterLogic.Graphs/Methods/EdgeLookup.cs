namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Reads an undirected answer back onto the connections the query listed.
/// <para>
/// A method only defined on an undirected graph answers per edge. When the query
/// was directed, the user's connections are arcs — an arc and its reverse listed
/// separately, in a different order — and a per-connection answer has to line up
/// with those, because that is the order every other per-connection output
/// follows. So the edge answer is looked up for each arc, in arc order.
/// </para>
/// </summary>
internal static class EdgeLookup
{
    /// <summary>The weight of the edge between two nodes, or NaN when there is none. Neighbours are sorted, so a binary search.</summary>
    public static double WeightOf(WeightedGraph graph, int a, int b)
    {
        var neighbours = graph.Neighbours(a);
        int at = neighbours.BinarySearch(b);
        return at < 0 ? double.NaN : graph.EdgeWeights(a)[at];
    }

    /// <summary>
    /// A per-edge answer, given as a function of the two ends, read onto every
    /// connection of the query in <see cref="PathQuery.Connections"/> order.
    /// </summary>
    public static double[] OntoConnections(PathQuery query, Func<int, int, double> perEdge)
    {
        var values = new double[query.ConnectionCount];
        int k = 0;
        foreach (var (a, b, _) in query.Connections())
            values[k++] = perEdge(a, b);

        return values;
    }
}

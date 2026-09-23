namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Cut vertices as a method on a wire: the nodes a graph depends on to stay in
/// one piece, how many others each would strand, and the bridges — the
/// connections that are the only link between what lies either side. No settings.
/// </summary>
public sealed record CutVerticesMethod : GraphMethod
{
    public override string Name => "Cut Vertices";

    public override string Describe() => $"{Name} and bridges";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var graph = query.Undirected(out bool lostDirection);
        var result = CutVertices.Of(graph);
        var stranded = result.Stranded;

        var cuts = result.ArticulationPoints()
            .OrderByDescending(i => stranded[i]).ThenBy(i => i)
            .ToArray();

        var bridges = new HashSet<(int, int)>(result.Bridges.Select(b => (Math.Min(b.A, b.B), Math.Max(b.A, b.B))));
        var isBridge = EdgeLookup.OntoConnections(query, (a, b) => bridges.Contains((Math.Min(a, b), Math.Max(a, b))) ? 1.0 : 0.0);

        var remarks = new List<string>();
        if (lostDirection)
            remarks.Add(DirectionIgnored);
        if (query.Sources.Length > 0 || query.Targets.Length > 0)
            remarks.Add(EndsIgnored);

        return new PathOutcome
        {
            Method = Name,
            Values = stranded.Select(s => (double)s).ToArray(),
            ValuesName = "Stranded: how many nodes lose the rest of the graph if this one goes",
            ConnectionValues = isBridge,
            ConnectionValuesName = "Bridge, 1 where losing the connection splits the graph",
            Groups = result.Bridges.Select(b => new[] { Math.Min(b.A, b.B), Math.Max(b.A, b.B) }).ToArray(),
            GroupsName = "Bridge",
            Marked = cuts,
            MarkedName = "Cut Vertex, most stranding first",
            Remarks = remarks,
            Details = new[]
            {
                $"{cuts.Length} cut {(cuts.Length == 1 ? "vertex" : "vertices")} and {Count(result.Bridges.Length, "bridge")}.",
                cuts.Length == 0
                    ? "No single node holds the graph together: lose any one and the rest stays in one piece."
                    : $"Node {cuts[0]} strands the most, {stranded[cuts[0]]} node(s), if it goes. Every joint of a "
                      + "simple chain is a cut vertex, so Stranded is what says whether one matters.",
            },
        };
    }
}

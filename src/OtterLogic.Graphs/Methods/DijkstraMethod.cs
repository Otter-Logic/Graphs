namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Dijkstra as a method on a wire: cheapest routes from the sources to the
/// targets, or to every node when no target is named. No settings, because the
/// algorithm has none a person should have to read: the costs are the graph's
/// weights, and the question is the sources and targets on the query.
/// </summary>
public sealed record DijkstraMethod : GraphMethod
{
    public override string Name => "Dijkstra";

    public override string Describe() => $"{Name}, cheapest routes by weight";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.RequireSources(Name);

        var wanted = query.Targets.Length > 0 ? query.Targets : null;
        var tree = query.DirectedOrNull is { } directed
            ? Dijkstra.From(directed, query.Sources, targets: wanted)
            : Dijkstra.From(query.UndirectedOrNull!, query.Sources, targets: wanted);

        var ends = Routing.Ends(query);
        var (routes, unreached) = Routing.Trace(tree, ends);

        var remarks = new List<string>();
        if (Routing.UnreachedRemark(query, unreached.Length) is { } remark)
            remarks.Add(remark);

        return new PathOutcome
        {
            Method = Name,
            Routes = routes,
            RouteEnds = ends,
            Values = Routing.Values(tree),
            ValuesName = "Cost",
            Marked = unreached,
            MarkedName = "Unreached",
            Remarks = remarks,
            Details = new[]
            {
                $"Cheapest routes {Routing.Span(query)}: {ends.Length - unreached.Length} of {ends.Length} reached.",
                "Each connection's weight is read as the cost of travelling it — a length, a time, a penalty.",
            },
        };
    }
}

namespace OtterLogic.Graphs.Methods;

/// <summary>
/// No method chosen: the question is read off what was wired, and the method
/// that answers it is run with the reason in words.
/// <para>
/// What the graph component runs when nothing is wired to its Method input, so a
/// user gets a first answer before they have chosen anything. Sources and
/// targets are the only two things they can wire, and each combination has one
/// sensible reading: nothing at all asks what the graph is made of; sources ask
/// for the cheapest routes from them; one source and one target on a placed
/// graph is exactly what A* is for, and it is used whenever the weights let it be.
/// </para>
/// </summary>
public sealed record AutoMethod : GraphMethod
{
    public override string Name => "Auto";

    public override string Describe()
        => $"{Name}: routes when there are sources, pieces when there are none";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Sources.Length == 0 && query.Targets.Length == 0)
            return Chosen(new ConnectedPiecesMethod().Run(query),
                "nothing was wired to Sources or Targets, so the first question is what the graph is made of. "
                + "Wire Sources for routes from them.");

        if (query.Sources.Length == 0)
        {
            var fromTargets = query.With(query.Targets, null);
            return Chosen(new DijkstraMethod().Run(fromTargets),
                "only Targets were wired, so routes were traced outward from them to every node"
                + (query.IsDirected ? ", following the arcs forward from the targets." : "."));
        }

        if (query.Sources.Length == 1 && query.Targets.Length == 1 && query.HasPositions
            && AStarMethod.ChooseMetric(query, 1.0, out _) != EstimateMetric.Unsafe)
            return Chosen(new AStarMethod().Run(query),
                "one source, one target and node positions: A* finds Dijkstra's route while looking at far less of the graph.");

        return Chosen(new DijkstraMethod().Run(query),
            query.Targets.Length == 0
                ? "Sources were wired and no Targets, so Dijkstra gives the cheapest route to every node."
                : "Dijkstra gives the cheapest route from the nearest source to each target in one solve.");
    }

    private static PathOutcome Chosen(PathOutcome outcome, string rationale) => outcome with { Rationale = rationale };
}

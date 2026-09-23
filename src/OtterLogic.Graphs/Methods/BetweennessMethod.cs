namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Betweenness as a method on a wire: how much of the graph's traffic passes
/// through each node. The one setting is where exactness gives way to estimation
/// on a large graph, and its default is one a user never needs to move.
/// </summary>
public sealed record BetweennessMethod : GraphMethod
{
    /// <summary>
    /// Past this many nodes the score is estimated from this many evenly spread
    /// starting points rather than all of them, which keeps a large graph
    /// interactive. Evenly spread, not random, so the answer is the same every solve.
    /// </summary>
    public int MaximumSources { get; init; } = 2000;

    /// <summary>How many of the busiest nodes to single out.</summary>
    public const int BusiestCount = 10;

    public override string Name => "Betweenness";

    public override string Describe() => $"{Name}, exact up to {MaximumSources} nodes";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (MaximumSources < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumSources), MaximumSources, "At least one source is needed to score from.");

        var graph = query.Undirected(out bool lostDirection);
        int n = graph.NodeCount;
        var betweenness = Centrality.Betweenness(graph, MaximumSources);

        // The busiest few, busiest first, so a person sees where the graph leans
        // without sorting anything on the canvas. Ties by index, for a steady answer.
        var busiest = Enumerable.Range(0, n)
            .Where(i => betweenness[i] > 0.0)
            .OrderByDescending(i => betweenness[i]).ThenBy(i => i)
            .Take(BusiestCount)
            .ToArray();

        var remarks = new List<string>();
        if (lostDirection)
            remarks.Add(DirectionIgnored);
        if (query.Sources.Length > 0 || query.Targets.Length > 0)
            remarks.Add(EndsIgnored);
        if (n > MaximumSources)
            remarks.Add($"Estimated from {MaximumSources} of {n} nodes. Raise Maximum Sources for the exact value, at the cost of solve time.");

        return new PathOutcome
        {
            Method = Name,
            Values = betweenness,
            ValuesName = "Betweenness, 0 to 1",
            Marked = busiest,
            MarkedName = "Busiest, busiest first",
            Remarks = remarks,
            Details = new[]
            {
                $"Every node scored by the share of shortest routes between all other pairs that pass through it"
                + (n > MaximumSources ? ", estimated." : ", exact."),
                "High scores are the bottlenecks and bridges — the nodes the rest of the graph leans on to reach itself. "
                + "Routes are counted in steps; weights play no part.",
            },
        };
    }
}

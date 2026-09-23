namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Potential flow as a method on a wire: how much passes along every connection
/// when something enters at the sources and drains at the targets.
/// <para>
/// The targets are the drains — the exits, the supports, the plant room — and
/// are the one thing it must be given. The sources are where things enter; with
/// none named, the same amount enters at every node, which is how "everything
/// drains from everywhere" is asked and is the usual first question. The one
/// setting is how much enters at each source.
/// </para>
/// </summary>
public sealed record PotentialFlowMethod : GraphMethod
{
    /// <summary>What enters at each source, or at every node when no source is named.</summary>
    public double Injection { get; init; } = 1.0;

    public override string Name => "Potential Flow";

    public override string Describe() => $"{Name}, {Injection:0.###} entering per source";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!double.IsFinite(Injection))
            throw new ArgumentOutOfRangeException(nameof(Injection), Injection, "Injection must be finite.");

        query.RequireTargets(Name, "the nodes everything drains to — the exits, the supports, the plant room");

        var graph = query.Undirected(out bool lostDirection);
        int n = graph.NodeCount;

        var injection = new double[n];
        if (query.Sources.Length == 0)
            Array.Fill(injection, Injection);
        else
            foreach (int s in query.Sources)
                injection[s] = Injection;

        var result = PotentialFlow.Solve(graph, injection, query.Targets);

        var potential = new double[n];
        var unreached = new List<int>();
        for (int i = 0; i < n; i++)
        {
            potential[i] = result.Reached[i] ? result.Potential[i] : double.NaN;
            if (!result.Reached[i])
                unreached.Add(i);
        }

        // Read per connection of the query: on a directed query the weight is the
        // merged edge's, so an arc and its reverse report equal and opposite flows.
        var flow = EdgeLookup.OntoConnections(query, (a, b) => result.Flow(a, b, EdgeLookup.WeightOf(graph, a, b)));

        var warnings = new List<string>();
        var remarks = new List<string>();
        if (lostDirection)
            remarks.Add(DirectionIgnored);
        if (unreached.Count > 0)
            warnings.Add($"{unreached.Count} node(s) are in a piece of the graph with no target, so nothing entering "
                         + "there has anywhere to go. Marked lists them.");
        if (!result.Converged)
            warnings.Add($"The solve stopped after {result.Iterations} iterations short of its tolerance, so the flows "
                         + "are approximate. Weights spanning many orders of magnitude are the usual cause.");

        return new PathOutcome
        {
            Method = Name,
            Values = potential,
            ValuesName = "Potential",
            ConnectionValues = flow,
            ConnectionValuesName = "Flow, from the connection's first node to its second; negative runs the other way",
            Marked = unreached.ToArray(),
            MarkedName = "Unreached",
            Warnings = warnings,
            Remarks = remarks,
            Details = new[]
            {
                query.Sources.Length == 0
                    ? $"{Injection:0.###} enters at every node and drains at {Count(query.Targets.Length, "target")}."
                    : $"{Injection:0.###} enters at each of {Count(query.Sources.Length, "source")} and drains at {Count(query.Targets.Length, "target")}.",
                "Each connection's weight is how readily it carries: a heavier connection carries more for the same "
                + "drop. For a connection that gets harder with length, build the graph with one over its length.",
            },
        };
    }
}

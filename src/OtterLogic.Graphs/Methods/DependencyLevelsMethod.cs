namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Dependency levels as a method on a wire: rank every node of a directed graph
/// by the longest chain of things it depends on, folding every cycle into one
/// group first. No settings. Needs a directed graph, since an order needs a
/// direction: read as arcs each way, an undirected graph is nothing but cycles.
/// </summary>
public sealed record DependencyLevelsMethod : GraphMethod
{
    public override string Name => "Dependency Levels";

    public override string Describe() => Name;

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.RequireDirected(Name,
            "an order needs to know which way each dependence runs. Build the graph with Graph From "
            + "Connectivity and Directed set, each branch listing what that node depends on.");

        var graph = query.DirectedOrNull!;
        int n = graph.NodeCount;
        var result = Condensation.Of(graph);

        var level = Enumerable.Range(0, n).Select(result.HeightOf).ToArray();
        int levels = n == 0 ? 0 : level.Max() + 1;

        var sequence = Enumerable.Range(0, levels)
            .Select(l => Enumerable.Range(0, n).Where(i => level[i] == l).ToArray())
            .ToArray();

        var cycles = Enumerable.Range(0, n)
            .GroupBy(i => result.Component[i])
            .Where(g => g.Count() > 1)
            .Select(g => g.ToArray())
            .ToArray();
        var inCycle = cycles.SelectMany(c => c).OrderBy(i => i).ToArray();

        var details = new List<string>
        {
            $"{Count(levels, "level")}: level 0 depends on nothing, each level only on those below it. "
            + "Everything on one level can go ahead together once the levels below are done.",
        };
        foreach (var cycle in cycles)
            details.Add($"Circular dependence between nodes {string.Join(", ", cycle)}, folded into one group on level {level[cycle[0]]}.");

        var remarks = new List<string>();
        if (cycles.Length > 0)
            remarks.Add($"{cycles.Length} circular dependenc{(cycles.Length == 1 ? "y" : "ies")} folded into one group each. "
                        + "Marked lists the nodes on a cycle.");
        if (query.Sources.Length > 0 || query.Targets.Length > 0)
            remarks.Add(EndsIgnored);

        return new PathOutcome
        {
            Method = Name,
            Values = level.Select(l => (double)l).ToArray(),
            ValuesName = "Level, the longest chain of dependence below the node",
            Groups = sequence,
            GroupsName = "Level",
            Marked = inCycle,
            MarkedName = "In A Cycle",
            Remarks = remarks,
            Details = details,
        };
    }
}

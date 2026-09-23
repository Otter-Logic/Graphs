namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Breadth-first search as a method on a wire: the fewest steps from the sources
/// to every node, and the rings that makes. No settings; weights play no part.
/// </summary>
public sealed record BreadthFirstMethod : GraphMethod
{
    public override string Name => "Breadth-First";

    public override string Describe() => $"{Name}, fewest steps";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.RequireSources(Name);

        var tree = query.DirectedOrNull is { } directed
            ? BreadthFirst.From(directed, query.Sources)
            : BreadthFirst.From(query.UndirectedOrNull!, query.Sources);

        int n = query.NodeCount;
        var ends = Routing.Ends(query);
        var (routes, unreached) = Routing.Trace(tree, ends);

        // One ring per step count: branch 0 the sources, branch 1 their neighbours,
        // and so on — usually what a person wanted when they reached for this.
        var reached = Enumerable.Range(0, n).Where(tree.Reaches).ToArray();
        int depth = reached.Length == 0 ? 0 : (int)reached.Max(i => tree.Cost[i]) + 1;
        var rings = Enumerable.Range(0, depth).Select(_ => new List<int>()).ToArray();
        foreach (int i in reached)
            rings[(int)tree.Cost[i]].Add(i);

        var remarks = new List<string>();
        int strandedNodes = n - reached.Length;
        if (strandedNodes > 0)
            remarks.Add($"{strandedNodes} node(s) are in a part of the graph no source reaches."
                        + (query.Targets.Length > 0 ? $" {unreached.Length} of the targets are among them; Marked lists those." : " Marked lists them."));

        return new PathOutcome
        {
            Method = Name,
            Routes = routes,
            RouteEnds = ends,
            Values = Routing.Values(tree),
            ValuesName = "Steps",
            Groups = rings.Select(r => r.ToArray()).ToArray(),
            GroupsName = "Ring",
            Marked = unreached,
            MarkedName = "Unreached",
            Remarks = remarks,
            Details = new[]
            {
                $"Fewest steps {Routing.Span(query)}: {Math.Max(depth - 1, 0)} ring(s) out from the sources.",
                "Every step counts one whatever the connection weighs; for weighted costs use Dijkstra.",
            },
        };
    }
}

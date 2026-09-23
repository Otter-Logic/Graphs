namespace OtterLogic.Graphs.Methods;

/// <summary>
/// What every graph method hands back, whatever it is: the answer in the few shapes
/// an answer about a graph can take, and what the method can honestly say about it.
/// <para>
/// The common ground between eight results that are otherwise unalike. A route
/// search has a tree of routes, potential flow has a number per connection, cut
/// vertices has a count per node and a list of pairs, dependency levels has
/// groups — and the component that holds the graph cannot know which it will get.
/// So a method reduces its result to routes, a value per node, a value per
/// connection, groups of nodes and nodes singled out, each named so a report can
/// say what it is; anything else it wants seen goes into <see cref="Details"/>
/// as lines. Every field is optional, and a method fills only the ones its
/// answer has — the name beside each one is how a user tells Cost from Steps
/// from Betweenness without reading a manual.
/// </para>
/// </summary>
public sealed record PathOutcome
{
    /// <summary>The method's name, or for an automatic choice the name of the one chosen.</summary>
    public required string Method { get; init; }

    /// <summary>One route per end in <see cref="RouteEnds"/>, source first. Empty where none reaches it. Null for a method that finds no routes.</summary>
    public int[][]? Routes { get; init; }

    /// <summary>The node each route in <see cref="Routes"/> leads to.</summary>
    public int[]? RouteEnds { get; init; }

    /// <summary>Per node, the method's number for it — a cost, a step count, a score, a level. NaN where it has none. Null for a method with no per-node answer.</summary>
    public double[]? Values { get; init; }

    /// <summary>What <see cref="Values"/> measures: "Cost", "Steps", "Betweenness".</summary>
    public string? ValuesName { get; init; }

    /// <summary>Per connection, in <see cref="PathQuery.Connections"/> order — a flow, a flag. Null for a method with no per-connection answer.</summary>
    public double[]? ConnectionValues { get; init; }

    /// <summary>What <see cref="ConnectionValues"/> measures: "Flow", "Bridge".</summary>
    public string? ConnectionValuesName { get; init; }

    /// <summary>Sets of nodes, one array each, in whatever order the method has — rings out from a source, the pieces of a graph, its levels. Null for none.</summary>
    public int[][]? Groups { get; init; }

    /// <summary>What one group is: "Ring", "Piece", "Level".</summary>
    public string? GroupsName { get; init; }

    /// <summary>Nodes the method singles out — unreached, cut vertices, the busiest. Null for none.</summary>
    public int[]? Marked { get; init; }

    /// <summary>What being marked means: "Unreached", "Cut Vertex", "Explored".</summary>
    public string? MarkedName { get; init; }

    /// <summary>The answer stands but is weaker than it looks. Check before acting on it.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Worth knowing; nothing to fix. Direction was ignored, a count was estimated.</summary>
    public IReadOnlyList<string> Remarks { get; init; } = Array.Empty<string>();

    /// <summary>Lines for the report: what ran, what was reached, what the numbers mean. Informational only.</summary>
    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();

    /// <summary>Why this method, when it was chosen rather than asked for. Null otherwise.</summary>
    public string? Rationale { get; init; }

    /// <summary>How many routes reach their end.</summary>
    public int ReachedCount => Routes?.Count(r => r.Length > 0) ?? 0;

    /// <summary>
    /// The run in words, one line per fact, for a report a person reads without
    /// wiring anything else: what ran and why, then what the method had to say.
    /// </summary>
    public IReadOnlyList<string> Report()
    {
        var lines = new List<string>();

        string headline = Method;
        if (Rationale is { } rationale)
            headline += " — chosen because " + rationale;
        lines.Add(headline.EndsWith('.') ? headline : headline + ".");

        lines.AddRange(Details);

        if (ValuesName is not null)
            lines.Add($"Values is {ValuesName}, one per node.");
        if (ConnectionValuesName is not null)
            lines.Add($"Connection Values is {ConnectionValuesName}, one per connection in Deconstruct Graph's order.");
        if (GroupsName is not null && Groups is not null)
            lines.Add($"Groups holds {Groups.Length} {Plural(GroupsName, Groups.Length)}, one branch each.");
        if (MarkedName is not null && Marked is not null)
            lines.Add($"Marked lists {Marked.Length} node(s): {MarkedName}.");

        foreach (string warning in Warnings)
            lines.Add("Warning: " + warning);
        foreach (string remark in Remarks)
            lines.Add(remark);

        return lines;
    }

    private static string Plural(string name, int count)
        => count == 1 ? name.ToLowerInvariant() : name.ToLowerInvariant() + "s";

    /// <summary>
    /// Complains about an answer whose shape does not fit the graph it is about, so
    /// a method that miscounts is caught here and not in an adaptor's data tree.
    /// </summary>
    internal void Check(int nodeCount, int connectionCount)
    {
        if ((Routes is null) != (RouteEnds is null))
            throw new InvalidOperationException($"{Method} returned routes without their ends, or ends without routes.");
        if (Routes is not null && Routes.Length != RouteEnds!.Length)
            throw new InvalidOperationException($"{Method} returned {Routes.Length} routes for {RouteEnds.Length} ends.");
        if (Values is not null && Values.Length != nodeCount)
            throw new InvalidOperationException($"{Method} returned {Values.Length} values for {nodeCount} nodes.");
        if (ConnectionValues is not null && ConnectionValues.Length != connectionCount)
            throw new InvalidOperationException($"{Method} returned {ConnectionValues.Length} connection values for {connectionCount} connections.");
        if ((Values is null) != (ValuesName is null))
            throw new InvalidOperationException($"{Method} returned values without a name, or a name without values.");
        if ((ConnectionValues is null) != (ConnectionValuesName is null))
            throw new InvalidOperationException($"{Method} returned connection values without a name, or a name without values.");
        if ((Groups is null) != (GroupsName is null))
            throw new InvalidOperationException($"{Method} returned groups without a name, or a name without groups.");
        if ((Marked is null) != (MarkedName is null))
            throw new InvalidOperationException($"{Method} returned marked nodes without a name, or a name without nodes.");

        foreach (int node in (Marked ?? Array.Empty<int>()).Concat(RouteEnds ?? Array.Empty<int>())
                     .Concat((Groups ?? Array.Empty<int[]>()).SelectMany(g => g))
                     .Concat((Routes ?? Array.Empty<int[]>()).SelectMany(r => r)))
            if (node < 0 || node >= nodeCount)
                throw new InvalidOperationException($"{Method} named node {node}, but nodes run from 0 to {nodeCount - 1}.");
    }
}

namespace OtterLogic.Graphs.Methods;

/// <summary>
/// Connected pieces as a method on a wire: the separate pieces a graph is in.
/// No settings. Called pieces rather than components because in Grasshopper a
/// component is the thing the user just dropped.
/// <para>
/// Worth running before anything else: no route or flow crosses a gap, so a
/// graph meant to be one piece and not explains most surprising answers
/// downstream. Given sources and targets it says whether they share a piece,
/// which is the question behind "why is there no route".
/// </para>
/// </summary>
public sealed record ConnectedPiecesMethod : GraphMethod
{
    public override string Name => "Connected Pieces";

    public override string Describe() => Name;

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var graph = query.Undirected(out bool lostDirection);
        int n = graph.NodeCount;
        var piece = graph.ConnectedComponents(out int count);

        // Pieces are numbered by their lowest node, so grouping the nodes in order
        // meets the pieces in order too.
        var groups = Enumerable.Range(0, n).GroupBy(i => piece[i]).Select(g => g.ToArray()).ToArray();
        var isolated = groups.Where(g => g.Length == 1).Select(g => g[0]).ToArray();

        var details = new List<string>
        {
            count == 1
                ? "The graph is one piece: every node can reach every other."
                : $"The graph is in {count} separate pieces; no route, flow or message crosses between them.",
        };
        if (isolated.Length > 0)
            details.Add($"{Count(isolated.Length, "node")} connected to nothing. Marked lists them.");

        var ends = query.Sources.Concat(query.Targets).Distinct().ToArray();
        if (ends.Length > 1)
        {
            var piecesOfEnds = ends.Select(i => piece[i]).Distinct().Count();
            details.Add(piecesOfEnds == 1
                ? "The sources and targets all lie in one piece, so routes between them exist."
                : $"The sources and targets lie in {piecesOfEnds} different pieces, so no route joins those in different ones.");
        }

        var remarks = new List<string>();
        if (lostDirection)
            remarks.Add(DirectionIgnored);

        return new PathOutcome
        {
            Method = Name,
            Values = piece.Select(p => (double)p).ToArray(),
            ValuesName = "Piece, numbered by its lowest node",
            Groups = groups,
            GroupsName = "Piece",
            Marked = isolated,
            MarkedName = "Isolated, connected to nothing",
            Remarks = remarks,
            Details = details,
        };
    }
}

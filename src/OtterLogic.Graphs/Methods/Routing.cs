namespace OtterLogic.Graphs.Methods;

/// <summary>
/// What the route searches share once they have a <see cref="RouteTree"/>: which
/// ends were asked for, the route to each, and which were never reached.
/// </summary>
internal static class Routing
{
    /// <summary>The targets, or every node when none were named.</summary>
    public static int[] Ends(PathQuery query)
        => query.Targets.Length > 0 ? query.Targets : Enumerable.Range(0, query.NodeCount).ToArray();

    /// <summary>One route per end, and the ends no route reaches.</summary>
    public static (int[][] Routes, int[] Unreached) Trace(RouteTree tree, int[] ends)
    {
        var routes = new int[ends.Length][];
        var unreached = new List<int>();

        for (int k = 0; k < ends.Length; k++)
        {
            routes[k] = tree.RouteTo(ends[k]);
            if (routes[k].Length == 0)
                unreached.Add(ends[k]);
        }

        return (routes, unreached.ToArray());
    }

    /// <summary>The tree's cost per node, NaN where nothing reaches it.</summary>
    public static double[] Values(RouteTree tree)
    {
        var values = new double[tree.Cost.Length];
        for (int i = 0; i < values.Length; i++)
            values[i] = tree.Reaches(i) ? tree.Cost[i] : double.NaN;

        return values;
    }

    /// <summary>"from 2 sources to every node" / "from 1 source to 3 targets".</summary>
    public static string Span(PathQuery query)
    {
        string from = $"from {query.Sources.Length} source{(query.Sources.Length == 1 ? "" : "s")}";
        string to = query.Targets.Length == 0
            ? "to every node"
            : $"to {query.Targets.Length} target{(query.Targets.Length == 1 ? "" : "s")}";
        return from + " " + to;
    }

    /// <summary>The remark a route search makes about ends it could not reach, or null when it reached them all.</summary>
    public static string? UnreachedRemark(PathQuery query, int unreached)
    {
        if (unreached == 0)
            return null;

        string what = query.Targets.Length == 0 ? "node" : "target";
        return $"{unreached} {what}(s) are in a part of the graph no source reaches. Marked lists them"
               + (query.IsDirected ? "; in a directed graph the arcs may simply not lead there." : ". Connected Pieces shows why.");
    }
}

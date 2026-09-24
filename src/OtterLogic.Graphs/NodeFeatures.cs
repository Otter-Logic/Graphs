namespace OtterLogic.Graphs;

/// <summary>
/// One row of numbers per node, describing its place in the graph: what a
/// clustering or a trained model reads when the samples are the nodes of a network.
/// </summary>
/// <param name="Names">What each column holds, in order.</param>
/// <param name="Rows">n x <see cref="Names"/>.Length, one row per node.</param>
/// <param name="Remarks">Worth knowing; nothing to fix. No sources were given, a score was estimated.</param>
public sealed record NodeFeatureTable(string[] Names, double[,] Rows, IReadOnlyList<string> Remarks)
{
    /// <summary>Number of nodes described.</summary>
    public int NodeCount => Rows.GetLength(0);

    /// <summary>Number of columns.</summary>
    public int ColumnCount => Rows.GetLength(1);
}

/// <summary>
/// Describes every node of a <see cref="WeightedGraph"/> by the same row of numbers:
/// how many connections it has and how strong, how tightly its neighbours are knit,
/// how much of the graph it can reach in two steps, how much traffic passes
/// through it, how near it is to everything, how much it holds on, and how far it
/// stands from the nearest source.
/// <para>
/// Here beside <see cref="Centrality"/> because it is a reading of a graph with no
/// learning in it, and because the columns are the ones a clustering and a trained
/// graph model must both compute the same way. It answers the question the
/// per-node outputs of the individual methods cannot: not "how busy is this node",
/// but "what kind of node is this" — a hub, a bridge, a leaf on a spur, a room off
/// a corridor — which is a question of several readings side by side.
/// </para>
/// <para>
/// Every column is a property of the graph alone, never of the order its nodes or
/// edges were listed in, so the same graph gives the same rows on every solve.
/// Closeness and betweenness are exact up to <c>maximumSources</c> nodes and
/// estimated from that many evenly spread starting points past it, for the reason
/// <see cref="Centrality.Betweenness"/> gives: a few thousand searches is where a
/// re-solve stops feeling live.
/// </para>
/// </summary>
public static class NodeFeatures
{
    /// <summary>How many nodes this one is connected to.</summary>
    public const string Connections = "Connections";

    /// <summary>The weights of its connections summed: its weighted degree.</summary>
    public const string Strength = "Strength";

    /// <summary>
    /// The share of its neighbours that are connected to each other, 0 to 1 — the
    /// local clustering coefficient. One in a clique; zero at the hub of a star.
    /// </summary>
    public const string Triangles = "Triangles";

    /// <summary>How many nodes lie within two steps, itself not counted.</summary>
    public const string WithinTwoSteps = "Within Two Steps";

    /// <summary>The share of shortest routes between other pairs that pass through it, 0 to 1.</summary>
    public const string Betweenness = "Betweenness";

    /// <summary>
    /// One over its mean distance in steps to the nodes it can reach, scaled by the
    /// share of the graph it reaches — the Wasserman and Faust closeness, so a
    /// node in a small piece is not read as central for being near its few neighbours.
    /// </summary>
    public const string Closeness = "Closeness";

    /// <summary>How many nodes lose their connection to the rest of its piece when it is removed.</summary>
    public const string Stranded = "Stranded";

    /// <summary>How many nodes are in its connected piece, itself included.</summary>
    public const string PieceSize = "Piece Size";

    /// <summary>Fewest steps to any source; -1 with no sources, or with no route to one.</summary>
    public const string StepsToSource = "Steps To Source";

    /// <summary>Cheapest route cost to any source, over the weights; -1 with no sources, or with no route to one.</summary>
    public const string CostToSource = "Cost To Source";

    /// <summary>The columns of every table, in order.</summary>
    public static readonly string[] Names =
    {
        Connections, Strength, Triangles, WithinTwoSteps, Betweenness, Closeness,
        Stranded, PieceSize, StepsToSource, CostToSource,
    };

    /// <summary>
    /// Describes every node.
    /// </summary>
    /// <param name="graph">The graph. Weights are read as costs for <see cref="CostToSource"/> and summed for <see cref="Strength"/>; every other column counts steps.</param>
    /// <param name="sources">Nodes the last two columns measure distance from. Null or empty gives -1 in both.</param>
    /// <param name="maximumSources">Past this many nodes, betweenness and closeness are estimated from this many evenly spread starting points.</param>
    public static NodeFeatureTable Of(WeightedGraph graph, IEnumerable<int>? sources = null, int maximumSources = 2000)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (maximumSources < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumSources), maximumSources, "Need at least one source to score from.");

        int n = graph.NodeCount;
        var from = (sources ?? Array.Empty<int>()).Distinct().OrderBy(s => s).ToArray();
        foreach (int s in from)
            if (s < 0 || s >= n)
                throw new ArgumentOutOfRangeException(nameof(sources), s, $"Source {s} is outside 0..{n - 1}.");

        var rows = new double[n, Names.Length];
        var remarks = new List<string>();

        var betweenness = Centrality.Betweenness(graph, maximumSources);
        var closeness = ClosenessOf(graph, maximumSources);
        var stranded = CutVertices.Stranded(graph);
        var component = graph.ConnectedComponents(out int pieces);
        var pieceSize = new int[pieces];
        foreach (int c in component)
            pieceSize[c]++;

        RouteTree? steps = from.Length > 0 ? BreadthFirst.From(graph, from) : null;
        RouteTree? costs = from.Length > 0 ? Dijkstra.From(graph, from) : null;

        var seen = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            var neighbours = graph.Neighbours(i);

            rows[i, 0] = neighbours.Length;
            rows[i, 1] = graph.Degree(i);
            rows[i, 2] = TrianglesAt(graph, i);

            // Everything one or two steps away, each node once, itself left out.
            seen.Clear();
            foreach (int a in neighbours)
            {
                seen.Add(a);
                foreach (int b in graph.Neighbours(a))
                    seen.Add(b);
            }

            seen.Remove(i);
            rows[i, 3] = seen.Count;

            rows[i, 4] = betweenness[i];
            rows[i, 5] = closeness[i];
            rows[i, 6] = stranded[i];
            rows[i, 7] = pieceSize[component[i]];
            rows[i, 8] = steps is not null && steps.Reaches(i) ? steps.Cost[i] : -1.0;
            rows[i, 9] = costs is not null && costs.Reaches(i) ? costs.Cost[i] : -1.0;
        }

        if (from.Length == 0)
            remarks.Add($"No sources were given, so {StepsToSource} and {CostToSource} are -1 for every node. "
                + "Wire the nodes distances should be measured from into Sources.");
        else if (steps is not null && Enumerable.Range(0, n).Any(i => !steps.Reaches(i)))
            remarks.Add($"Some nodes have no route to a source and read -1 in {StepsToSource} and {CostToSource}.");

        if (n > maximumSources)
            remarks.Add($"{Betweenness} and {Closeness} were estimated from {maximumSources} of {n} nodes. "
                + "Raise Maximum Sources for the exact values, at the cost of solve time.");

        return new NodeFeatureTable((string[])Names.Clone(), rows, remarks);
    }

    /// <summary>
    /// The share of a node's neighbour pairs that are themselves connected. A node
    /// with fewer than two neighbours has no pairs and scores zero.
    /// </summary>
    private static double TrianglesAt(WeightedGraph graph, int node)
    {
        var neighbours = graph.Neighbours(node);
        int degree = neighbours.Length;
        if (degree < 2)
            return 0.0;

        int closed = 0;
        for (int p = 0; p < degree; p++)
        {
            // Neighbour lists are sorted, so each pair is one binary search.
            var around = graph.Neighbours(neighbours[p]);
            for (int q = p + 1; q < degree; q++)
                if (around.BinarySearch(neighbours[q]) >= 0)
                    closed++;
        }

        return 2.0 * closed / (degree * (degree - 1));
    }

    /// <summary>
    /// Closeness by hop count, from every node or from an even sample of them.
    /// <para>
    /// Distance is symmetric on an undirected graph, so a node's mean distance to
    /// everything can be estimated from the distances of a few sources to it: each
    /// source's search reaches every node, and every node collects what the sources
    /// found — Eppstein and Wang's estimate, spread evenly rather than drawn at
    /// random so the answer is the same on every solve.
    /// </para>
    /// </summary>
    private static double[] ClosenessOf(WeightedGraph graph, int maximumSources)
    {
        int n = graph.NodeCount;
        var closeness = new double[n];
        if (n < 2)
            return closeness;

        int sources = Math.Min(n, maximumSources);
        var total = new double[n];
        var reached = new int[n];
        var sampled = new bool[n];
        var distance = new int[n];
        var queue = new int[n];

        for (int s = 0; s < sources; s++)
        {
            int source = sources == n ? s : (int)((long)s * n / sources);
            sampled[source] = true;

            Array.Fill(distance, -1);
            distance[source] = 0;
            int head = 0, tail = 0;
            queue[tail++] = source;

            while (head < tail)
            {
                int node = queue[head++];
                foreach (int next in graph.Neighbours(node))
                {
                    if (distance[next] >= 0)
                        continue;

                    distance[next] = distance[node] + 1;
                    queue[tail++] = next;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (i == source || distance[i] < 0)
                    continue;

                total[i] += distance[i];
                reached[i]++;
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (reached[i] == 0)
                continue;

            // The sources a node could have been reached from: all of them but itself.
            int others = sources - (sampled[i] ? 1 : 0);
            if (others <= 0)
                continue;

            double share = (double)reached[i] / others;
            double mean = total[i] / reached[i];
            closeness[i] = share / mean;
        }

        return closeness;
    }
}

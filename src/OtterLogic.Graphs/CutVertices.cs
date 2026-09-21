namespace OtterLogic.Graphs;

/// <summary>
/// What a <see cref="WeightedGraph"/> depends on to stay in one piece, from one
/// low-link search: the nodes, the edges, and how much each node holds on.
/// </summary>
/// <param name="Stranded">
/// For every node, how many other nodes lose their connection to the largest
/// remaining piece of its component when it is removed. Zero for a node whose
/// removal leaves the rest of its component in one piece.
/// </param>
/// <param name="Bridges">
/// Edges whose removal splits their component — the only connection between what
/// lies on either side. Lower node first, in ascending order.
/// </param>
public sealed record CutStructure(int[] Stranded, (int A, int B)[] Bridges)
{
    /// <summary>
    /// The cut vertices — articulation points — in ascending order: exactly the
    /// nodes that strand something. A node whose removal splits its component
    /// always leaves at least two pieces, and everything outside the largest of
    /// them is stranded, so the two definitions cannot disagree.
    /// </summary>
    public int[] ArticulationPoints()
        => Enumerable.Range(0, Stranded.Length).Where(i => Stranded[i] > 0).ToArray();
}

/// <summary>
/// The nodes a <see cref="WeightedGraph"/> depends on to stay in one piece, and
/// how much of it each one holds on.
/// <para>
/// A cut vertex — an articulation point — is a node whose removal splits its
/// connected component. That alone is a poor thing to flag: every interior node of
/// a chain is one, and so is the node at the root of every short spur. What says
/// whether a cut matters is how much it strands, so that is what this reports.
/// </para>
/// </summary>
public static class CutVertices
{
    /// <summary>
    /// For every node, how many other nodes lose their connection to the largest
    /// remaining piece of its component when it is removed. Zero for a node whose
    /// removal leaves the rest of its component in one piece.
    /// <para>
    /// Tarjan's low-link search, written iteratively so a long chain cannot
    /// overflow the stack, with subtree sizes kept alongside: removing a node
    /// leaves one piece per child whose subtree cannot reach above it, plus
    /// whatever sits above it in the search tree, and everything outside the
    /// largest of those pieces is stranded.
    /// </para>
    /// </summary>
    public static int[] Stranded(WeightedGraph graph) => Of(graph).Stranded;

    /// <summary>
    /// The same search, keeping the bridges it passes on the way.
    /// <para>
    /// A tree edge is a bridge when nothing below it reaches as high as its upper
    /// end — the test for a cut vertex with the equality taken out. It falls out of
    /// the pass that is already being made, which is why both are reported from
    /// here rather than by a second search that would have to agree with this one.
    /// A <see cref="WeightedGraph"/> holds each edge once, so stepping back along
    /// the edge just descended is the only thing that has to be ruled out.
    /// </para>
    /// </summary>
    public static CutStructure Of(WeightedGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        int n = graph.NodeCount;
        var stranded = new int[n];
        var discovered = new int[n];
        var low = new int[n];
        var size = new int[n];
        var parent = new int[n];
        var cursor = new int[n];
        Array.Fill(discovered, -1);

        // Per node, the pieces its removal leaves: the largest found so far among
        // its separable children, and how many nodes those children hold in total.
        var largestPiece = new int[n];
        var separated = new int[n];

        var bridges = new List<(int A, int B)>();
        var stack = new Stack<int>();
        var members = new List<int>();
        int clock = 0;

        for (int root = 0; root < n; root++)
        {
            if (discovered[root] >= 0)
                continue;

            members.Clear();
            discovered[root] = low[root] = clock++;
            parent[root] = -1;
            size[root] = 1;
            stack.Push(root);
            members.Add(root);

            while (stack.Count > 0)
            {
                int node = stack.Peek();
                var neighbours = graph.Neighbours(node);

                if (cursor[node] < neighbours.Length)
                {
                    int next = neighbours[cursor[node]++];
                    if (discovered[next] < 0)
                    {
                        discovered[next] = low[next] = clock++;
                        parent[next] = node;
                        size[next] = 1;
                        stack.Push(next);
                        members.Add(next);
                    }
                    else if (next != parent[node])
                    {
                        low[node] = Math.Min(low[node], discovered[next]);
                    }

                    continue;
                }

                stack.Pop();
                int above = parent[node];
                if (above < 0)
                    continue;

                size[above] += size[node];
                low[above] = Math.Min(low[above], low[node]);

                if (low[node] > discovered[above])
                    bridges.Add((Math.Min(above, node), Math.Max(above, node)));

                if (low[node] >= discovered[above])
                {
                    largestPiece[above] = Math.Max(largestPiece[above], size[node]);
                    separated[above] += size[node];
                }
            }

            int componentSize = members.Count;
            foreach (int node in members)
            {
                if (separated[node] == 0)
                    continue;

                // Whatever sits above the node in the search tree is one more
                // piece. The root has nothing above it, and every child of the
                // root is separable, so a root with one child leaves one piece and
                // strands nothing without a case of its own.
                int rest = componentSize - 1 - separated[node];
                int largest = Math.Max(largestPiece[node], rest);
                stranded[node] = componentSize - 1 - largest;
            }
        }

        bridges.Sort();
        return new CutStructure(stranded, bridges.ToArray());
    }
}

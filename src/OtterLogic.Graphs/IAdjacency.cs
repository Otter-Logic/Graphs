namespace OtterLogic.Graphs;

/// <summary>
/// The one thing a search needs from a graph: from this node, where can a step go
/// and what does it weigh.
/// <para>
/// Both graph types answer it, which is what lets <see cref="Dijkstra"/>,
/// <see cref="BreadthFirst"/> and <see cref="AStar"/> each be written once. The
/// searches are generic over the implementing type rather than taking the
/// interface, so the calls stay direct — both graphs are sealed. Internal because
/// it is plumbing: a caller picks a graph type, and an algorithm that is only true
/// of one of them says so in its signature rather than accepting this.
/// </para>
/// </summary>
internal interface IAdjacency
{
    int NodeCount { get; }

    /// <summary>Nodes one step on from <paramref name="node"/>, ascending.</summary>
    ReadOnlySpan<int> Next(int node);

    /// <summary>Weights of those steps, in the same order.</summary>
    ReadOnlySpan<double> NextWeights(int node);
}

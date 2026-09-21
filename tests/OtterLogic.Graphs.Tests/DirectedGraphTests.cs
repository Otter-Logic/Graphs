using OtterLogic.Graphs;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class DirectedGraphTests
{
    /// <summary>
    /// An arc and its reverse are two arcs. Rows come out sorted whatever order the
    /// arcs arrived in, and an arc's id is its place in tail-then-head order.
    /// </summary>
    [Fact]
    public void KeepsDirectionAndNumbersArcsByTailThenHead()
    {
        var graph = DirectedGraph.FromArcs(4, new[] { (2, 0), (0, 3), (0, 1), (1, 0), (3, 3) });

        Assert.Equal(4, graph.ArcCount);                                   // the self-loop is dropped
        Assert.Equal(new[] { 1, 3 }, graph.Successors(0).ToArray());
        Assert.Equal(new[] { 0 }, graph.Successors(1).ToArray());
        Assert.Equal(new[] { (0, 1, 1.0), (0, 3, 1.0), (1, 0, 1.0), (2, 0, 1.0) }, graph.Arcs().ToArray());

        Assert.Equal(0, graph.FirstArc(0));
        Assert.Equal(2, graph.FirstArc(1));
        Assert.Equal(3, graph.FirstArc(2));
        Assert.Equal(4, graph.FirstArc(3));                                // no arcs leave 3
    }

    /// <summary>
    /// The reverse rows answer "what arrives here", and hand back arc ids so that a
    /// caller walking backwards can still find whatever it keeps per arc.
    /// </summary>
    [Fact]
    public void PredecessorsMirrorSuccessorsAndCarryArcIds()
    {
        var graph = DirectedGraph.FromArcs(4, new[] { (2, 0, 5.0), (1, 0, 7.0), (0, 3, 9.0) }, DuplicateArcs.KeepSmallest);

        Assert.Equal(new[] { 1, 2 }, graph.Predecessors(0).ToArray());
        Assert.Equal(new[] { 7.0, 5.0 }, graph.PredecessorArcs(0).ToArray().Select(graph.Weight));
        Assert.Empty(graph.Predecessors(1).ToArray());
        Assert.Equal(new[] { 0 }, graph.Predecessors(3).ToArray());
    }

    /// <summary>
    /// Where WeightedGraph drops a zero and refuses a negative, this keeps both: a
    /// free transfer and a dummy arc in a schedule are real arcs.
    /// </summary>
    [Fact]
    public void KeepsZeroAndNegativeWeights()
    {
        var graph = DirectedGraph.FromArcs(3, new[] { (0, 1, 0.0), (1, 2, -4.0) }, DuplicateArcs.Sum);

        Assert.Equal(new[] { (0, 1, 0.0), (1, 2, -4.0) }, graph.Arcs().ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DirectedGraph.FromArcs(3, new[] { (0, 1, double.NaN) }, DuplicateArcs.Sum));
        Assert.Throws<ArgumentOutOfRangeException>(() => DirectedGraph.FromArcs(3, new[] { (0, 3) }));
    }

    /// <summary>What a repeated arc means depends on what the weight is, so the caller says.</summary>
    [Theory]
    [InlineData(DuplicateArcs.KeepSmallest, 2.0)]
    [InlineData(DuplicateArcs.KeepLargest, 5.0)]
    [InlineData(DuplicateArcs.Sum, 7.0)]
    public void ARepeatedArcIsFoldedAsAsked(DuplicateArcs policy, double expected)
    {
        var graph = DirectedGraph.FromArcs(2, new[] { (0, 1, 5.0), (0, 1, 2.0), (1, 0, 3.0) }, policy);

        Assert.Equal(new[] { (0, 1, expected), (1, 0, 3.0) }, graph.Arcs().ToArray());
    }

    /// <summary>Undirected to directed loses nothing; back again loses the direction and says how the two ways merge.</summary>
    [Fact]
    public void ConvertsBothWays()
    {
        var undirected = WeightedGraph.FromEdges(3, new[] { (0, 1, 2.0), (1, 2, 3.0) });
        var directed = undirected.ToDirected();

        Assert.Equal(new[] { (0, 1, 2.0), (1, 0, 2.0), (1, 2, 3.0), (2, 1, 3.0) }, directed.Arcs().ToArray());
        Assert.Equal(undirected.Edges().ToArray(), directed.ToUndirected(DuplicateArcs.KeepLargest).Edges().ToArray());

        var oneWay = DirectedGraph.FromArcs(3, new[] { (0, 1, 2.0), (1, 0, 6.0), (2, 1, 3.0) }, DuplicateArcs.KeepSmallest);
        Assert.Equal(new[] { (0, 1, 2.0), (1, 2, 3.0) }, oneWay.ToUndirected(DuplicateArcs.KeepSmallest).Edges().ToArray());
        Assert.Equal(new[] { (0, 1, 8.0), (1, 2, 3.0) }, oneWay.ToUndirected(DuplicateArcs.Sum).Edges().ToArray());
    }

    /// <summary>
    /// A ring of one-way streets: the way there is one step and the way back is all the
    /// way round. The same search, the same ties; only the steps that exist differ.
    /// </summary>
    [Fact]
    public void SearchesFollowArcsOneWayOnly()
    {
        var ring = DirectedGraph.FromArcs(5, Enumerable.Range(0, 5).Select(i => (i, (i + 1) % 5)));

        var costs = Dijkstra.From(ring, new[] { 0 });
        Assert.Equal(new[] { 0.0, 1.0, 2.0, 3.0, 4.0 }, costs.Cost);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, costs.RouteTo(4));

        var steps = BreadthFirst.From(ring, new[] { 0 });
        Assert.Equal(costs.Cost, steps.Cost);
        Assert.Equal(costs.Previous, steps.Previous);

        // Nothing leads into a node that only has arcs out.
        var source = DirectedGraph.FromArcs(3, new[] { (0, 1), (0, 2) });
        Assert.False(Dijkstra.From(source, new[] { 1 }).Reaches(0));

        Assert.Throws<InvalidOperationException>(() =>
            Dijkstra.From(DirectedGraph.FromArcs(2, new[] { (0, 1, -1.0) }, DuplicateArcs.Sum), new[] { 0 }));
    }

    [Fact]
    public void CondensationReadsAGraphAsItReadsArcs()
    {
        var arcs = new[] { (0, 1), (1, 2), (2, 1), (3, 0) };
        var fromGraph = Condensation.Of(DirectedGraph.FromArcs(4, arcs));
        var fromArcs = Condensation.Of(4, arcs);

        Assert.Equal(fromArcs.Component, fromGraph.Component);
        Assert.Equal(fromArcs.Height, fromGraph.Height);
    }
}

using OtterLogic.Graphs;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class DijkstraTests
{
    /// <summary>
    /// A square with a cheap diagonal: the diagonal wins over the two sides, and the
    /// route is read back from the previous nodes.
    /// </summary>
    [Fact]
    public void FindsTheCheapestRouteAndReadsItBack()
    {
        var graph = WeightedGraph.FromEdges(4, new[] { (0, 1, 1.0), (1, 2, 1.0), (2, 3, 1.0), (3, 0, 1.0), (0, 2, 1.5) });
        var result = Dijkstra.From(graph, new[] { 0 });

        Assert.Equal(new[] { 0.0, 1.0, 1.5, 1.0 }, result.Cost);
        Assert.Equal(new[] { 0, 2 }, result.RouteTo(2));
        Assert.All(result.Source, s => Assert.Equal(0, s));
    }

    /// <summary>Several sources: every node is reached from the nearest one.</summary>
    [Fact]
    public void EveryNodeIsReachedFromItsNearestSource()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1), (1, 2), (2, 3), (3, 4) });
        var result = Dijkstra.From(graph, new[] { 0, 4 });

        Assert.Equal(new[] { 0, 0, 0, 4, 4 }, result.Source);
        Assert.Equal(new[] { 0.0, 1.0, 2.0, 1.0, 0.0 }, result.Cost);
    }

    /// <summary>
    /// Equal-cost choices go to the lower-numbered source, so the answer does not
    /// depend on the order the sources were listed in.
    /// </summary>
    [Fact]
    public void TiesGoToTheLowerSourceWhateverTheOrderGiven()
    {
        var graph = WeightedGraph.FromEdges(3, new[] { (0, 1), (1, 2) });

        var forwards = Dijkstra.From(graph, new[] { 0, 2 });
        var backwards = Dijkstra.From(graph, new[] { 2, 0 });

        Assert.Equal(0, forwards.Source[1]);
        Assert.Equal(forwards.Source, backwards.Source);
        Assert.Equal(forwards.Previous, backwards.Previous);
    }

    /// <summary>
    /// The cost is the caller's, per direction: an edge priced at infinity one way is
    /// a one-way edge, and a node behind it is unreached.
    /// </summary>
    [Fact]
    public void ACallerCostCanForbidADirection()
    {
        var graph = WeightedGraph.FromEdges(3, new[] { (0, 1), (1, 2) });
        var result = Dijkstra.From(graph, new[] { 0 }, (from, to, _) => to > from && to == 2 ? double.PositiveInfinity : 1.0);

        Assert.True(result.Reaches(1));
        Assert.False(result.Reaches(2));
        Assert.Equal(double.PositiveInfinity, result.Cost[2]);
        Assert.Empty(result.RouteTo(2));
    }

    [Fact]
    public void RejectsANegativeCostAndASourceOutsideTheGraph()
    {
        var graph = WeightedGraph.FromEdges(2, new[] { (0, 1) });

        Assert.Throws<InvalidOperationException>(() => Dijkstra.From(graph, new[] { 0 }, (_, _, _) => -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dijkstra.From(graph, new[] { 2 }));
    }

    /// <summary>
    /// With targets the search stops as soon as the last of them is settled. What
    /// it reports for them must be what the full search reports, and what it had
    /// not settled must read as unreached rather than as the bound it held.
    /// </summary>
    [Fact]
    public void TargetsStopTheSearchWithoutChangingTheirAnswer()
    {
        // A chain of ten with a dear shortcut from 0 to 5: node 5 goes into the queue at
        // cost 10 straight away, and its real cost along the chain is 5.
        var edges = Enumerable.Range(0, 9).Select(i => (i, i + 1, 1.0)).Append((0, 5, 10.0));
        var graph = WeightedGraph.FromEdges(10, edges);

        var full = Dijkstra.From(graph, new[] { 0 });
        var early = Dijkstra.From(graph, new[] { 0 }, targets: new[] { 3 });

        Assert.Equal(full.Cost[3], early.Cost[3]);
        Assert.Equal(full.RouteTo(3), early.RouteTo(3));

        // Stopped at 3, node 5 still held its bound of 10. That is not its cost, so it is
        // not reported as one.
        Assert.Equal(5.0, full.Cost[5]);
        Assert.False(early.Reaches(5));
        Assert.True(double.IsPositiveInfinity(early.Cost[5]));
        Assert.Empty(early.RouteTo(9));
    }

    /// <summary>A target nothing reaches does not stop the search from answering for the rest.</summary>
    [Fact]
    public void AnUnreachableTargetLeavesTheReachableAnswered()
    {
        var graph = WeightedGraph.FromEdges(4, new[] { (0, 1), (1, 2) });
        var result = Dijkstra.From(graph, new[] { 0 }, targets: new[] { 2, 3 });

        Assert.Equal(2.0, result.Cost[2]);
        Assert.False(result.Reaches(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dijkstra.From(graph, new[] { 0 }, targets: new[] { 4 }));
    }
}

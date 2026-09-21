using OtterLogic.Graphs;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class BreadthFirstTests
{
    /// <summary>Steps are counted, whatever the edges weigh.</summary>
    [Fact]
    public void CountsStepsAndIgnoresWeights()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1, 9.0), (1, 2, 9.0), (2, 3, 9.0), (0, 3, 0.1), (3, 4, 50.0) });
        var result = BreadthFirst.From(graph, new[] { 0 });

        Assert.Equal(new[] { 0.0, 1.0, 2.0, 1.0, 2.0 }, result.Cost);
        Assert.Equal(new[] { 0, 3, 4 }, result.RouteTo(4));
    }

    /// <summary>
    /// The contract: the same answer as Dijkstra with every step costing one - cost,
    /// source and previous node alike, ties included. A grid is nothing but ties, and
    /// dropping edges from it by a fixed rule leaves islands and detours as well.
    /// </summary>
    [Theory]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 0, 35 })]
    [InlineData(new[] { 17, 5, 30 })]
    public void AgreesWithDijkstraAtUnitCostNodeForNode(int[] sources)
    {
        const int side = 6;
        var edges = new List<(int, int)>();
        for (int r = 0; r < side; r++)
        {
            for (int c = 0; c < side; c++)
            {
                int i = r * side + c;
                if (c + 1 < side && (i * 7 + 3) % 5 != 0) edges.Add((i, i + 1));
                if (r + 1 < side && (i * 11 + 1) % 6 != 0) edges.Add((i, i + side));
            }
        }

        var graph = WeightedGraph.FromEdges(side * side + 1, edges);   // one node left isolated

        var steps = BreadthFirst.From(graph, sources);
        var costs = Dijkstra.From(graph, sources, (_, _, _) => 1.0);

        Assert.Equal(costs.Cost, steps.Cost);
        Assert.Equal(costs.Source, steps.Source);
        Assert.Equal(costs.Previous, steps.Previous);
        Assert.False(steps.Reaches(side * side));
    }

    [Fact]
    public void RejectsASourceOutsideTheGraph()
    {
        var graph = WeightedGraph.FromEdges(2, new[] { (0, 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => BreadthFirst.From(graph, new[] { 2 }));
    }
}

using OtterLogic.Graphs;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class NodeFeaturesTests
{
    private static int Column(string name) => Array.IndexOf(NodeFeatures.Names, name);

    /// <summary>
    /// A star of four leaves: the hub has four connections, no triangles, reaches
    /// everything in one step, and carries every route; a leaf has one connection
    /// and reaches the rest in two steps.
    /// </summary>
    [Fact]
    public void ReadsAStar()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1, 2.0), (0, 2, 2.0), (0, 3, 2.0), (0, 4, 2.0) });
        var table = NodeFeatures.Of(graph);

        Assert.Equal(NodeFeatures.Names.Length, table.ColumnCount);
        Assert.Equal(5, table.NodeCount);

        Assert.Equal(4.0, table.Rows[0, Column(NodeFeatures.Connections)]);
        Assert.Equal(8.0, table.Rows[0, Column(NodeFeatures.Strength)]);
        Assert.Equal(0.0, table.Rows[0, Column(NodeFeatures.Triangles)]);
        Assert.Equal(4.0, table.Rows[0, Column(NodeFeatures.WithinTwoSteps)]);
        Assert.Equal(1.0, table.Rows[0, Column(NodeFeatures.Betweenness)], 12);
        Assert.Equal(1.0, table.Rows[0, Column(NodeFeatures.Closeness)], 12);
        Assert.Equal(3.0, table.Rows[0, Column(NodeFeatures.Stranded)]);
        Assert.Equal(5.0, table.Rows[0, Column(NodeFeatures.PieceSize)]);

        Assert.Equal(1.0, table.Rows[1, Column(NodeFeatures.Connections)]);
        Assert.Equal(4.0, table.Rows[1, Column(NodeFeatures.WithinTwoSteps)]);
        Assert.Equal(0.0, table.Rows[1, Column(NodeFeatures.Betweenness)], 12);
        // A leaf is one step from the hub and two from each other leaf: mean 7/4.
        Assert.Equal(4.0 / 7.0, table.Rows[1, Column(NodeFeatures.Closeness)], 12);
        Assert.Equal(0.0, table.Rows[1, Column(NodeFeatures.Stranded)]);
    }

    /// <summary>Every neighbour pair in a triangle is connected, so every node's triangle share is one.</summary>
    [Fact]
    public void ATriangleIsFullyKnit()
    {
        var graph = WeightedGraph.FromEdges(3, new[] { (0, 1), (1, 2), (2, 0) });
        var table = NodeFeatures.Of(graph);

        for (int i = 0; i < 3; i++)
            Assert.Equal(1.0, table.Rows[i, Column(NodeFeatures.Triangles)]);
    }

    /// <summary>
    /// Without sources the distance columns read -1 and a remark says why; with a
    /// source they count steps and sum costs, and a node in another piece stays -1.
    /// </summary>
    [Fact]
    public void MeasuresDistanceFromTheSourcesWhenGiven()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1, 2.0), (1, 2, 3.0), (3, 4, 1.0) });

        var without = NodeFeatures.Of(graph);
        Assert.All(Enumerable.Range(0, 5), i => Assert.Equal(-1.0, without.Rows[i, Column(NodeFeatures.StepsToSource)]));
        Assert.Contains(without.Remarks, r => r.Contains("No sources"));

        var with = NodeFeatures.Of(graph, new[] { 0 });
        Assert.Equal(0.0, with.Rows[0, Column(NodeFeatures.StepsToSource)]);
        Assert.Equal(2.0, with.Rows[2, Column(NodeFeatures.StepsToSource)]);
        Assert.Equal(5.0, with.Rows[2, Column(NodeFeatures.CostToSource)]);
        Assert.Equal(-1.0, with.Rows[4, Column(NodeFeatures.StepsToSource)]);
        Assert.Equal(-1.0, with.Rows[4, Column(NodeFeatures.CostToSource)]);
        Assert.Equal(2.0, with.Rows[4, Column(NodeFeatures.PieceSize)]);
        Assert.Contains(with.Remarks, r => r.Contains("no route"));
    }

    /// <summary>
    /// Sampled sources estimate closeness: on a long path the middle node's exact
    /// closeness is known, and a fifth of the sources lands within a few per cent.
    /// </summary>
    [Fact]
    public void SampledSourcesEstimateCloseness()
    {
        const int n = 101;
        var graph = WeightedGraph.FromEdges(n, Enumerable.Range(0, n - 1).Select(i => (i, i + 1)));

        var exact = NodeFeatures.Of(graph);
        var sampled = NodeFeatures.Of(graph, maximumSources: 20);

        // The middle node is a mean of 25.5 steps from the other hundred.
        Assert.Equal(1.0 / 25.5, exact.Rows[50, Column(NodeFeatures.Closeness)], 12);
        Assert.InRange(sampled.Rows[50, Column(NodeFeatures.Closeness)], 0.9 / 25.5, 1.1 / 25.5);
        Assert.Contains(sampled.Remarks, r => r.Contains("estimated"));
    }

    [Fact]
    public void RefusesASourceOutsideTheGraph()
    {
        var graph = WeightedGraph.FromEdges(2, new[] { (0, 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => NodeFeatures.Of(graph, new[] { 5 }));
    }
}

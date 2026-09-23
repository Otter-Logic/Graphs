using OtterLogic.Graphs.Spatial;
using Xunit;

namespace OtterLogic.Graphs.Tests;

/// <summary>
/// Drawn lines into a graph: ends within tolerance are one node, numbering
/// follows the input order, and the graph is weighted by length unless told otherwise.
/// </summary>
public class LineNetworkTests
{
    /// <summary>A triangle drawn as three lines whose corners nearly, not exactly, meet — in 3D.</summary>
    private static (double[,] Starts, double[,] Ends) Triangle()
        => (new double[,] { { 0, 0, 0 }, { 3, 0, 0 }, { 0, 4, 5 } },
            new double[,] { { 3, 0, 0.0001 }, { 0, 4, 5 }, { 0.0001, 0, 0 } });

    [Fact]
    public void EndsWithinToleranceBecomeOneNodeInInputOrder()
    {
        var (starts, ends) = Triangle();
        var network = LineNetwork.Weld(starts, ends, 0.001);

        Assert.Equal(3, network.NodeCount);
        Assert.Equal(new[] { 0, 1, 2 }, network.From);
        Assert.Equal(new[] { 1, 2, 0 }, network.To);
        Assert.Empty(network.Collapsed);

        // A node sits where the first end that made it was drawn.
        Assert.Equal(3.0, network.Nodes[1, 0]);
        Assert.Equal(0.0001, network.Nodes[1, 2]);
    }

    [Fact]
    public void EndsOutsideToleranceStaySeparate()
    {
        var (starts, ends) = Triangle();
        var network = LineNetwork.Weld(starts, ends, 0.00001);

        Assert.Equal(5, network.NodeCount);
    }

    [Fact]
    public void TheGraphIsWeightedByLengthInThreeDimensionsUnlessToldOtherwise()
    {
        var (starts, ends) = Triangle();
        var network = LineNetwork.Weld(starts, ends, 0.001);

        var byLength = network.Graph();
        Assert.Equal(3, byLength.EdgeCount);
        // Node 1 is (3, 0, 0.0001) and node 2 is (0, 4, 5): length sqrt(9 + 16 + 25) up to the tolerance.
        var edge = byLength.Edges().Single(e => e.A == 1 && e.B == 2);
        Assert.Equal(Math.Sqrt(50.0), edge.Weight, 3);

        var byWeight = network.Graph(new[] { 7.0, 8.0, 9.0 });
        Assert.Equal(7.0, byWeight.Edges().Single(e => e.A == 0 && e.B == 1).Weight);
    }

    [Fact]
    public void ALineShorterThanTheToleranceCollapsesAndMakesNoConnection()
    {
        var starts = new double[,] { { 0, 0, 0 }, { 5, 5, 5 } };
        var ends = new double[,] { { 1, 0, 0 }, { 5, 5, 5.0001 } };
        var network = LineNetwork.Weld(starts, ends, 0.001);

        Assert.Equal(new[] { 1 }, network.Collapsed);
        Assert.Equal(1, network.Graph().EdgeCount);
        Assert.Equal(3, network.NodeCount);
    }

    [Fact]
    public void RefusesWhatItCannotRead()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LineNetwork.Weld(new double[1, 3], new double[1, 3], 0.0));
        Assert.Throws<ArgumentException>(() => LineNetwork.Weld(new double[1, 2], new double[1, 2], 0.1));
        Assert.Throws<ArgumentException>(() => LineNetwork.Weld(new double[2, 3], new double[1, 3], 0.1));
        Assert.Throws<ArgumentException>(() => LineNetwork.Weld(new double[1, 3], new double[1, 3], 0.1).Graph(new[] { 1.0, 2.0 }));
    }
}

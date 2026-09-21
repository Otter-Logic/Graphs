using OtterLogic.Graphs.Planar;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class PlanarObstaclesTests
{
    private const double Tolerance = 1e-6;

    private static readonly double[,] Square = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };

    // A U opening upwards: two prongs either side of a notch over x in 1..2, y in 1..3.
    private static readonly double[,] U =
    {
        { 0, 0 }, { 3, 0 }, { 3, 3 }, { 2, 3 }, { 2, 1 }, { 1, 1 }, { 1, 3 }, { 0, 3 },
    };

    private static PlanarObstacles Of(params double[][,] polygons) => new(polygons, Tolerance);

    /// <summary>The boundary is not inside: a route is allowed to touch a wall.</summary>
    [Fact]
    public void TheBoundaryIsNotInside()
    {
        var obstacles = Of(Square);

        Assert.True(obstacles.Contains(0.0, 0.0));
        Assert.True(obstacles.Contains(0.999, -0.999));
        Assert.False(obstacles.Contains(2.0, 0.0));
        Assert.False(obstacles.Contains(1.0, 0.3));    // on an edge
        Assert.False(obstacles.Contains(1.0, 1.0));    // on a corner
    }

    [Fact]
    public void AConcaveOutlineKeepsItsNotchOutside()
    {
        var obstacles = Of(U);

        Assert.True(obstacles.Contains(0.5, 2.0));     // left prong
        Assert.True(obstacles.Contains(1.5, 0.5));     // base
        Assert.False(obstacles.Contains(1.5, 2.0));    // the notch
    }

    [Fact]
    public void AStepThroughIsBlockedAndAStepPastIsNot()
    {
        var obstacles = Of(Square);

        Assert.True(obstacles.Blocks(-2, 0, 2, 0));
        Assert.True(obstacles.Blocks(0, 0, 2, 0));         // starts inside
        Assert.True(obstacles.Blocks(-0.5, 0, 0.5, 0));    // wholly inside, meets no edge
        Assert.False(obstacles.Blocks(-2, 2, 2, 2));
        Assert.False(obstacles.Blocks(-2, -3, -2, 3));
    }

    /// <summary>
    /// The cases an edge-crossing test gets wrong, in both directions. Touching a
    /// corner and running along a wall pass through nothing; the diagonal between two
    /// corners of one obstacle crosses no edge and cuts straight through it.
    /// </summary>
    [Fact]
    public void TouchingIsAllowedAndACornerToCornerCutIsNot()
    {
        var obstacles = Of(Square);

        Assert.False(obstacles.Blocks(0, 2, 2, 0));        // grazes the corner at (1, 1)
        Assert.False(obstacles.Blocks(-3, 1, 3, 1));       // runs along the top edge
        Assert.False(obstacles.Blocks(-1, 1, 1, 1));       // is the top edge
        Assert.True(obstacles.Blocks(-1, -1, 1, 1));       // the diagonal
        Assert.True(obstacles.Blocks(-2, -2, 2, 2));       // through two corners and the middle
    }

    /// <summary>
    /// Between the two prong tips of a U the step lies outside the outline, touching it
    /// only at its ends; one row lower it would cross both prongs.
    /// </summary>
    [Fact]
    public void AStepAcrossANotchIsOpenAndAcrossTheProngsIsNot()
    {
        var obstacles = Of(U);

        Assert.False(obstacles.Blocks(1, 3, 2, 3));        // inner tip to inner tip
        Assert.False(obstacles.Blocks(1.5, 1, 1.5, 5));    // up out of the notch from its floor
        Assert.False(obstacles.Blocks(0, 3, 3, 3));        // along the tops: boundary and open air only
        Assert.True(obstacles.Blocks(-1, 2, 4, 2));        // through both prongs
        Assert.True(obstacles.Blocks(0, 0, 3, 3));         // corner to corner through the base
    }

    [Fact]
    public void AClosingVertexIsDroppedAndBadOutlinesAreRefused()
    {
        var closed = new double[,] { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 }, { -1, -1 } };
        Assert.Equal(4, Of(closed).Corners().GetLength(0));

        Assert.Throws<ArgumentException>(() => Of(new double[,] { { 0, 0 }, { 1, 1 } }));
        Assert.Throws<ArgumentException>(() => Of(new double[,] { { 0, 0, 0 }, { 1, 1, 0 }, { 2, 0, 0 } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlanarObstacles(new[] { Square }, 0.0));
    }
}

public class VisibilityGraphTests
{
    private static readonly double[,] Square = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };

    /// <summary>
    /// A square between two points. The shortest way round is known in closed form:
    /// out to a near corner, along one side, in from the far corner - 2 + 2√2.
    /// </summary>
    [Fact]
    public void TheShortestRouteRoundASquareIsTheExactOne()
    {
        var obstacles = new PlanarObstacles(new[] { Square }, 1e-6);
        var result = VisibilityGraph.Of(new double[,] { { -2, 0 }, { 2, 0 } }, obstacles);

        Assert.Equal(2, result.PointCount);
        Assert.Equal(6, result.Graph.NodeCount);

        var routes = Dijkstra.From(result.Graph, new[] { 0 }, targets: new[] { 1 });

        Assert.Equal(2.0 + 2.0 * Math.Sqrt(2.0), routes.Cost[1], 12);
        Assert.Equal(4, routes.RouteTo(1).Length);

        // The two points cannot see each other, and no corner sees the one diagonally opposite.
        Assert.DoesNotContain(1, result.Graph.Neighbours(0).ToArray());
        Assert.DoesNotContain(4, result.Graph.Neighbours(2).ToArray());
    }

    /// <summary>With nothing in the way the route is the straight line.</summary>
    [Fact]
    public void WithAClearViewTheRouteIsStraight()
    {
        var obstacles = new PlanarObstacles(new[] { Square }, 1e-6);
        var result = VisibilityGraph.Of(new double[,] { { -2, 3 }, { 2, 3 } }, obstacles);

        var routes = Dijkstra.From(result.Graph, new[] { 0 }, targets: new[] { 1 });
        Assert.Equal(4.0, routes.Cost[1], 12);
        Assert.Equal(new[] { 0, 1 }, routes.RouteTo(1));
    }

    /// <summary>A point inside an obstacle keeps its index, so the caller's numbering holds, and sees nothing.</summary>
    [Fact]
    public void AnEnclosedPointIsKeptAndIsolated()
    {
        var obstacles = new PlanarObstacles(new[] { Square }, 1e-6);
        var result = VisibilityGraph.Of(new double[,] { { 0, 0 }, { 2, 0 } }, obstacles);

        Assert.True(result.Enclosed[0]);
        Assert.False(result.Enclosed[1]);
        Assert.Equal(0, result.Graph.Neighbours(0).Length);
        Assert.False(Dijkstra.From(result.Graph, new[] { 1 }).Reaches(0));
    }
}

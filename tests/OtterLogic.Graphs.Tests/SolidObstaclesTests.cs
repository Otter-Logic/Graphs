using OtterLogic.Graphs.Spatial;
using Xunit;

namespace OtterLogic.Graphs.Tests;

/// <summary>
/// The same two questions <see cref="PlanarObstaclesTests"/> asks, in space: is
/// this point inside a solid, does this step pass through one — with a closed
/// box for the solid and a single square for a wall with no inside.
/// </summary>
public class SolidObstaclesTests
{
    private const double Tolerance = 1e-6;

    /// <summary>An axis-aligned box as twelve triangles, either winding.</summary>
    private static (double[,] Vertices, int[,] Faces) Box(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        var vertices = new double[,]
        {
            { x0, y0, z0 }, { x1, y0, z0 }, { x1, y1, z0 }, { x0, y1, z0 },
            { x0, y0, z1 }, { x1, y0, z1 }, { x1, y1, z1 }, { x0, y1, z1 },
        };
        var faces = new int[,]
        {
            { 0, 1, 2 }, { 0, 2, 3 }, // bottom
            { 4, 6, 5 }, { 4, 7, 6 }, // top
            { 0, 5, 1 }, { 0, 4, 5 }, // front
            { 3, 2, 6 }, { 3, 6, 7 }, // back
            { 0, 3, 7 }, { 0, 7, 4 }, // left
            { 1, 5, 6 }, { 1, 6, 2 }, // right
        };
        return (vertices, faces);
    }

    /// <summary>A vertical square in the plane x = 1, from y 0..2, z 0..2.</summary>
    private static (double[,] Vertices, int[,] Faces) Wall()
        => (new double[,] { { 1, 0, 0 }, { 1, 2, 0 }, { 1, 2, 2 }, { 1, 0, 2 } }, new int[,] { { 0, 1, 2 }, { 0, 2, 3 } });

    private static SolidObstacles UnitBox() => new(new[] { Box(0, 0, 0, 1, 1, 1) }, Tolerance);

    [Fact]
    public void InsideIsInsideAndTheSurfaceIsNot()
    {
        var box = UnitBox();

        Assert.True(box.Contains(0.5, 0.5, 0.5));
        Assert.False(box.Contains(2.0, 0.5, 0.5));
        Assert.False(box.Contains(1.0, 0.5, 0.5));  // on a face
        Assert.False(box.Contains(1.0, 1.0, 1.0));  // on a corner
        Assert.False(box.Contains(0.5, 0.5, 1.0 + Tolerance / 2)); // within tolerance of the top
    }

    [Fact]
    public void AStepThroughTheMiddleIsBlockedAndOneClearOfItIsNot()
    {
        var box = UnitBox();

        Assert.True(box.Blocks(-1, 0.5, 0.5, 2, 0.5, 0.5));
        Assert.False(box.Blocks(-1, 0.5, 2, 2, 0.5, 2));
        Assert.False(box.Blocks(-1, -1, -1, -0.5, -0.5, -0.5));
    }

    /// <summary>
    /// The case a face-crossing test gets wrong: corner to opposite corner crosses
    /// no face and cuts straight through the inside.
    /// </summary>
    [Fact]
    public void CornerToOppositeCornerThroughTheInsideIsBlocked()
    {
        Assert.True(UnitBox().Blocks(0, 0, 0, 1, 1, 1));
    }

    /// <summary>
    /// And the case it gets wrong the other way: a step along an edge or across a
    /// face touches the box everywhere and passes through nothing.
    /// </summary>
    [Fact]
    public void RunningAlongAnEdgeOrAcrossAFaceIsAllowed()
    {
        var box = UnitBox();

        Assert.False(box.Blocks(0, 0, 0, 1, 0, 0));       // along an edge
        Assert.False(box.Blocks(0, 0, 1, 1, 1, 1));       // diagonally across the top face
        Assert.False(box.Blocks(-1, 0, 1, 2, 0, 1));      // along the top front edge and beyond
        Assert.True(box.Blocks(-1, -1, 0.5, 2, 2, 0.5));  // and, for contrast, diagonally through the middle
    }

    [Fact]
    public void AStepFromInsideToOutsideIsBlocked()
    {
        Assert.True(UnitBox().Blocks(0.5, 0.5, 0.5, 3, 3, 3));
    }

    [Fact]
    public void AWallHasNoInsideButBlocksWhatCrossesIt()
    {
        var wall = new SolidObstacles(new[] { Wall() }, Tolerance);

        Assert.False(wall.Contains(1.0, 1.0, 1.0));
        Assert.True(wall.Blocks(0, 1, 1, 2, 1, 1));   // straight through
        Assert.False(wall.Blocks(0, 1, 3, 2, 1, 3));  // over the top
        Assert.False(wall.Blocks(1, 0.5, 0.5, 1, 1.5, 1.5)); // lying in the wall: along it, not through
        Assert.False(wall.Blocks(0, 2, 2, 2, 2, 2)); // grazing the top corner edge
    }

    [Fact]
    public void ObstaclesCombine()
    {
        var both = new SolidObstacles(new[] { Box(0, 0, 0, 1, 1, 1), Box(3, 0, 0, 4, 1, 1) }, Tolerance);

        Assert.Equal(2, both.Count);
        Assert.True(both.Contains(3.5, 0.5, 0.5));
        Assert.True(both.Blocks(2, 0.5, 0.5, 5, 0.5, 0.5));
        Assert.False(both.Blocks(1.5, 0.5, 0.5, 2.5, 0.5, 0.5));
    }

    [Fact]
    public void RefusesWhatItCannotRead()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SolidObstacles(new[] { Box(0, 0, 0, 1, 1, 1) }, 0.0));
        Assert.Throws<ArgumentException>(() => new SolidObstacles(new[] { (new double[2, 3], new int[,] { { 0, 1, 5 } }) }, Tolerance));
        Assert.Throws<ArgumentException>(() => new SolidObstacles(new[] { (new double[,] { { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 } }, new int[,] { { 0, 1, 2 } }) }, Tolerance));
    }
}

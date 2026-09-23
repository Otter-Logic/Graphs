using OtterLogic.Graphs;
using OtterLogic.Graphs.Methods;
using Xunit;

namespace OtterLogic.Graphs.Tests;

/// <summary>
/// The one call a graph component makes: what runs when no method is wired, and
/// whether what comes back is checked against the graph it is about. The methods
/// themselves are held to their algorithms in <see cref="GraphMethodTests"/>.
/// </summary>
public class PathRunTests
{
    private static readonly double[,] Corners = { { 0, 0, 0 }, { 1, 0, 0 }, { 1, 1, 0 }, { 0, 1, 0 } };

    private static WeightedGraph Square()
        => WeightedGraph.FromEdges(4, new[] { (0, 1, 1.0), (1, 2, 1.0), (2, 3, 1.0), (3, 0, 1.0) });

    [Fact]
    public void WithNothingWiredTheFirstAnswerIsWhatTheGraphIsMadeOf()
    {
        var outcome = PathRun.Solve(new PathQuery(Square()));

        Assert.Equal("Connected Pieces", outcome.Method);
        Assert.Contains("nothing was wired", outcome.Rationale);
        Assert.Contains(outcome.Report(), line => line.Contains("chosen because"));
    }

    [Fact]
    public void WithSourcesOnlyTheAnswerIsDijkstraToEverywhere()
    {
        var outcome = PathRun.Solve(new PathQuery(Square(), sources: new[] { 0 }));

        Assert.Equal("Dijkstra", outcome.Method);
        Assert.Equal(4, outcome.Routes!.Length);
        Assert.Contains("every node", outcome.Rationale);
    }

    [Fact]
    public void WithTargetsOnlyRoutesAreTracedOutwardFromThem()
    {
        var outcome = PathRun.Solve(new PathQuery(Square(), targets: new[] { 2 }));

        Assert.Equal("Dijkstra", outcome.Method);
        Assert.Equal(0.0, outcome.Values![2]);
        Assert.Contains("outward from them", outcome.Rationale);
    }

    [Fact]
    public void OneToOneOnAPlacedGraphIsAStarWhenTheWeightsAllowIt()
    {
        var placed = PathRun.Solve(new PathQuery(Square(), Corners, new[] { 0 }, new[] { 2 }));
        Assert.Equal("A*", placed.Method);
        Assert.Equal(new[] { 0, 1, 2 }, placed.Routes![0]);

        var unplaced = PathRun.Solve(new PathQuery(Square(), null, new[] { 0 }, new[] { 2 }));
        Assert.Equal("Dijkstra", unplaced.Method);

        // Weighted below their length, so A* would warn: Auto takes Dijkstra instead.
        var cheap = WeightedGraph.FromEdges(4, new[] { (0, 1, 0.1), (1, 2, 0.1), (2, 3, 0.1), (3, 0, 0.1) });
        var fallback = PathRun.Solve(new PathQuery(cheap, Corners, new[] { 0 }, new[] { 2 }));
        Assert.Equal("Dijkstra", fallback.Method);
    }

    [Fact]
    public void AWiredMethodRunsAsItselfWithNoRationale()
    {
        var outcome = PathRun.Solve(new PathQuery(Square(), sources: new[] { 0 }), new BreadthFirstMethod());

        Assert.Equal("Breadth-First", outcome.Method);
        Assert.Null(outcome.Rationale);
    }

    [Fact]
    public void AnAnswerThatDoesNotFitTheGraphIsCaughtHere()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathRun.Solve(new PathQuery(Square()), new Miscounting()));
        Assert.Contains("values for 4 nodes", ex.Message);
    }

    [Fact]
    public void TheReportSaysWhatEachOutputHolds()
    {
        var report = PathRun.Solve(new PathQuery(Square(), sources: new[] { 0 }), new BreadthFirstMethod()).Report();

        Assert.Contains(report, l => l.StartsWith("Values is Steps"));
        Assert.Contains(report, l => l.StartsWith("Groups holds"));
        Assert.Contains(report, l => l.StartsWith("Marked lists"));
    }

    [Fact]
    public void TheQueryChecksItsOwnShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PathQuery(Square(), sources: new[] { 9 }));
        Assert.Throws<ArgumentException>(() => new PathQuery(Square(), new double[3, 3]));
        Assert.Throws<ArgumentException>(() => new PathQuery(Square(), new double[4, 2]));

        var query = new PathQuery(Square(), Corners, new[] { 2, 0, 2 }, null);
        Assert.Equal(new[] { 0, 2 }, query.Sources);
        Assert.Equal(Math.Sqrt(2.0), query.Distance(0, 2), 9);
    }

    private sealed record Miscounting : GraphMethod
    {
        public override string Name => "Miscounting";
        public override string Describe() => Name;
        public override PathOutcome Run(PathQuery query)
            => new() { Method = Name, Values = new double[2], ValuesName = "Wrong" };
    }
}

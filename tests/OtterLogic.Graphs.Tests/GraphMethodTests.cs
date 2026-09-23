using OtterLogic.Graphs;
using OtterLogic.Graphs.Methods;
using Xunit;

namespace OtterLogic.Graphs.Tests;

/// <summary>
/// Each method on the wire, held to the algorithm it wraps: the record must hand
/// back exactly what the raw call would, in the common shape, and say what it
/// needs in words when it is not given it.
/// </summary>
public class GraphMethodTests
{
    /// <summary>A square with a cheap diagonal, placed on the unit square.</summary>
    private static PathQuery Square(IEnumerable<int>? sources = null, IEnumerable<int>? targets = null, bool placed = true)
    {
        var graph = WeightedGraph.FromEdges(4, new[] { (0, 1, 1.0), (1, 2, 1.0), (2, 3, 1.0), (3, 0, 1.0), (0, 2, 1.5) });
        var positions = placed ? new double[,] { { 0, 0, 0 }, { 1, 0, 0 }, { 1, 1, 0 }, { 0, 1, 0 } } : null;
        return new PathQuery(graph, positions, sources, targets);
    }

    [Fact]
    public void DijkstraMatchesTheRawCallAndNamesItsValues()
    {
        var query = Square(sources: new[] { 0 }, targets: new[] { 2 });
        var outcome = new DijkstraMethod().Run(query);
        var raw = Dijkstra.From(query.UndirectedOrNull!, new[] { 0 }, targets: new[] { 2 });

        Assert.Equal("Dijkstra", outcome.Method);
        Assert.Equal(new[] { 0, 2 }, Assert.Single(outcome.Routes!));
        Assert.Equal(new[] { 2 }, outcome.RouteEnds);
        Assert.Equal("Cost", outcome.ValuesName);
        Assert.Equal(raw.Cost[2], outcome.Values![2]);
        Assert.Empty(outcome.Marked!);
        Assert.Contains(outcome.Details, d => d.Contains("1 of 1 reached"));
    }

    [Fact]
    public void DijkstraWithoutTargetsRoutesToEveryNodeAndMarksTheUnreached()
    {
        var graph = WeightedGraph.FromEdges(4, new[] { (0, 1), (2, 3) });
        var outcome = new DijkstraMethod().Run(new PathQuery(graph, sources: new[] { 0 }));

        Assert.Equal(4, outcome.Routes!.Length);
        Assert.Equal(new[] { 2, 3 }, outcome.Marked);
        Assert.True(double.IsNaN(outcome.Values![3]));
        Assert.Contains(outcome.Remarks, r => r.Contains("no source reaches"));
    }

    [Fact]
    public void DijkstraSaysWhatItNeedsWhenNoSourceIsWired()
    {
        var ex = Assert.Throws<ArgumentException>(() => new DijkstraMethod().Run(Square()));
        Assert.Contains("source", ex.Message);
    }

    [Fact]
    public void AStarFindsDijkstrasRouteAndReportsWhatItLookedAt()
    {
        var query = Square(sources: new[] { 0 }, targets: new[] { 2 });
        var outcome = new AStarMethod().Run(query);
        var dijkstra = new DijkstraMethod().Run(query);

        Assert.Equal(dijkstra.Routes![0], outcome.Routes![0]);
        Assert.Equal(dijkstra.Values![2], outcome.Values![2]);
        Assert.StartsWith("Explored", outcome.MarkedName);
        Assert.Contains(2, outcome.Marked!);
        Assert.Empty(outcome.Warnings);
    }

    [Fact]
    public void AStarRefusesWithoutPositionsOrWithSeveralEnds()
    {
        Assert.Contains("positions",
            Assert.Throws<ArgumentException>(() => new AStarMethod().Run(Square(new[] { 0 }, new[] { 2 }, placed: false))).Message);
        Assert.Contains("one source to one target",
            Assert.Throws<ArgumentException>(() => new AStarMethod().Run(Square(new[] { 0, 1 }, new[] { 2 }))).Message);
    }

    /// <summary>
    /// A graph weighted in plan: the sloping connection weighs less than its 3D length,
    /// so the estimate has to fall back to plan; and one weighted below even that has
    /// no safe estimate at all.
    /// </summary>
    [Fact]
    public void AStarChoosesTheLongestLineTheWeightsVouchFor()
    {
        var positions = new double[,] { { 0, 0, 0 }, { 1, 0, 5 }, { 2, 0, 0 } };
        var inPlan = new PathQuery(WeightedGraph.FromEdges(3, new[] { (0, 1, 1.0), (1, 2, 1.0) }), positions, new[] { 0 }, new[] { 2 });
        Assert.Equal(EstimateMetric.Plan, AStarMethod.ChooseMetric(inPlan, 1.0, out _));

        var in3D = new PathQuery(WeightedGraph.FromEdges(3, new[] { (0, 1, 6.0), (1, 2, 6.0) }), positions, new[] { 0 }, new[] { 2 });
        Assert.Equal(EstimateMetric.ThreeDimensions, AStarMethod.ChooseMetric(in3D, 1.0, out _));

        var tooCheap = new PathQuery(WeightedGraph.FromEdges(3, new[] { (0, 1, 0.5), (1, 2, 0.5) }), positions, new[] { 0 }, new[] { 2 });
        Assert.Equal(EstimateMetric.Unsafe, AStarMethod.ChooseMetric(tooCheap, 1.0, out double safe));
        Assert.Equal(0.5, safe, 9);
        Assert.Contains(new AStarMethod().Run(tooCheap).Warnings, w => w.Contains("0.5"));
    }

    [Fact]
    public void BreadthFirstCountsStepsAndBucketsRings()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1, 9.0), (1, 2, 9.0), (2, 3, 9.0), (0, 4, 9.0) });
        var outcome = new BreadthFirstMethod().Run(new PathQuery(graph, sources: new[] { 0 }));

        Assert.Equal("Steps", outcome.ValuesName);
        Assert.Equal(new[] { 0.0, 1.0, 2.0, 3.0, 1.0 }, outcome.Values);
        Assert.Equal("Ring", outcome.GroupsName);
        Assert.Equal(new[] { new[] { 0 }, new[] { 1, 4 }, new[] { 2 }, new[] { 3 } }, outcome.Groups);
    }

    /// <summary>
    /// A chain grounded at one end with everything entering: the flow along each
    /// connection is what lies upstream of it, and it matches the raw solve.
    /// </summary>
    [Fact]
    public void PotentialFlowDrainsAtTheTargetsAndReadsFlowPerConnection()
    {
        var graph = WeightedGraph.FromEdges(4, new[] { (0, 1), (1, 2), (2, 3) });
        var outcome = new PotentialFlowMethod().Run(new PathQuery(graph, targets: new[] { 0 }));

        Assert.Equal("Potential", outcome.ValuesName);
        Assert.Equal(0.0, outcome.Values![0]);
        Assert.Equal(3, outcome.ConnectionValues!.Length);
        // Edges are (0,1), (1,2), (2,3): flow runs towards 0, so from higher to lower, negative.
        Assert.Equal(-3.0, outcome.ConnectionValues[0], 6);
        Assert.Equal(-2.0, outcome.ConnectionValues[1], 6);
        Assert.Equal(-1.0, outcome.ConnectionValues[2], 6);
        Assert.Empty(outcome.Marked!);
    }

    [Fact]
    public void PotentialFlowOnADirectedQueryReadsFlowOntoEachArc()
    {
        var directed = DirectedGraph.FromArcs(2, new[] { (0, 1, 2.0), (1, 0, 2.0) }, DuplicateArcs.KeepLargest);
        var outcome = new PotentialFlowMethod().Run(new PathQuery(directed, targets: new[] { 0 }));

        Assert.Equal(2, outcome.ConnectionValues!.Length);
        Assert.Equal(-outcome.ConnectionValues[1], outcome.ConnectionValues[0], 9);
        Assert.Contains(outcome.Remarks, r => r.Contains("direction was ignored"));
    }

    [Fact]
    public void PotentialFlowRefusesWithoutADrain()
    {
        var ex = Assert.Throws<ArgumentException>(() => new PotentialFlowMethod().Run(Square(sources: new[] { 0 })));
        Assert.Contains("target", ex.Message);
    }

    [Fact]
    public void BetweennessScoresEveryNodeAndSinglesOutTheBusiest()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 2), (1, 2), (2, 3), (3, 4) });
        var outcome = new BetweennessMethod().Run(new PathQuery(graph, sources: new[] { 0 }));

        Assert.Equal(Centrality.Betweenness(graph), outcome.Values);
        Assert.Equal(2, outcome.Marked![0]);
        Assert.Contains(outcome.Remarks, r => r.Contains("play no part"));
    }

    [Fact]
    public void ConnectedPiecesGroupsTheGraphAndSaysWhetherTheEndsShareOne()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1), (2, 3) });
        var apart = new ConnectedPiecesMethod().Run(new PathQuery(graph, sources: new[] { 0 }, targets: new[] { 3 }));

        Assert.Equal(new[] { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4 } }, apart.Groups);
        Assert.Equal(new[] { 4 }, apart.Marked);
        Assert.Contains(apart.Details, d => d.Contains("different pieces"));

        var together = new ConnectedPiecesMethod().Run(new PathQuery(graph, sources: new[] { 0 }, targets: new[] { 1 }));
        Assert.Contains(together.Details, d => d.Contains("one piece"));
    }

    /// <summary>Two triangles joined by one edge: its ends are the cut vertices and it is the bridge.</summary>
    [Fact]
    public void CutVerticesMarksTheCutsAndFlagsTheBridge()
    {
        var graph = WeightedGraph.FromEdges(6, new[] { (0, 1), (1, 2), (2, 0), (2, 3), (3, 4), (4, 5), (5, 3) });
        var outcome = new CutVerticesMethod().Run(new PathQuery(graph));

        Assert.Equal(new[] { 2, 3 }, outcome.Marked!.OrderBy(i => i));
        Assert.Equal(new[] { new[] { 2, 3 } }, outcome.Groups);

        var edges = graph.Edges().ToArray();
        for (int e = 0; e < edges.Length; e++)
            Assert.Equal(edges[e] is { A: 2, B: 3 } ? 1.0 : 0.0, outcome.ConnectionValues![e]);
    }

    [Fact]
    public void DependencyLevelsRanksADirectedGraphAndRefusesAnUndirectedOne()
    {
        // 2 depends on 1, 1 on 0; 3 and 4 depend on each other.
        var directed = DirectedGraph.FromArcs(5, new[] { (1, 0), (2, 1), (3, 4), (4, 3) });
        var outcome = new DependencyLevelsMethod().Run(new PathQuery(directed));

        Assert.Equal(new[] { 0.0, 1.0, 2.0, 0.0, 0.0 }, outcome.Values);
        Assert.Equal(new[] { new[] { 0, 3, 4 }, new[] { 1 }, new[] { 2 } }, outcome.Groups);
        Assert.Equal(new[] { 3, 4 }, outcome.Marked);
        Assert.Contains(outcome.Details, d => d.Contains("Circular"));

        var ex = Assert.Throws<ArgumentException>(() => new DependencyLevelsMethod().Run(Square()));
        Assert.Contains("directed", ex.Message);
    }

    [Fact]
    public void MethodsAreValuesAndDescribeThemselves()
    {
        Assert.Equal(new AStarMethod { EstimateScale = 2.0 }, new AStarMethod { EstimateScale = 2.0 });
        Assert.NotEqual(new AStarMethod(), new AStarMethod { EstimateScale = 2.0 });
        Assert.Equal("A*, estimate scale 2", new AStarMethod { EstimateScale = 2.0 }.ToString());
        Assert.Equal("Potential Flow, 0.5 entering per source", new PotentialFlowMethod { Injection = 0.5 }.Describe());
    }
}

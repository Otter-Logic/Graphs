using OtterLogic.Graphs;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class AStarTests
{
    private const int Side = 30;

    /// <summary>
    /// A grid with diagonals, each step weighing its length, and a wall across the
    /// middle with one gap near the far edge — so the straight line to the target is
    /// wrong and the search has to be talked out of it.
    /// </summary>
    private static (WeightedGraph Graph, Func<int, double> NoEstimate) WalledGrid()
    {
        bool Open(int r, int c) => r != Side / 2 || c >= Side - 3;

        var edges = new List<(int, int, double)>();
        for (int r = 0; r < Side; r++)
        {
            for (int c = 0; c < Side; c++)
            {
                if (!Open(r, c)) continue;

                foreach (var (dr, dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
                {
                    int r2 = r + dr, c2 = c + dc;
                    if (r2 >= Side || c2 < 0 || c2 >= Side || !Open(r2, c2)) continue;
                    edges.Add((r * Side + c, r2 * Side + c2, Math.Sqrt(dr * dr + dc * dc)));
                }
            }
        }

        var graph = WeightedGraph.FromEdges(Side * Side, edges);
        return (graph, _ => 0.0);
    }

    private static Func<int, double> StraightLine(int target)
        => node => Math.Sqrt(Math.Pow(node / Side - target / Side, 2) + Math.Pow(node % Side - target % Side, 2));

    /// <summary>
    /// The contract: Dijkstra's cost, from far less of the graph. Both halves matter —
    /// a search that looked at less and found something dearer would be a bug that
    /// reads as a speed-up.
    /// </summary>
    [Fact]
    public void FindsDijkstrasCostWhileLookingAtFarLessOfTheGraph()
    {
        var (graph, _) = WalledGrid();
        int source = 2 * Side + 2, target = (Side - 3) * Side + 4;

        var reference = Dijkstra.From(graph, new[] { source }, targets: new[] { target });
        int settled = Enumerable.Range(0, graph.NodeCount).Count(reference.Reaches);

        var route = AStar.Route(graph, source, target, StraightLine(target), null, out int[] expanded);

        Assert.Equal(reference.Cost[target], route.Cost[target], 9);
        Assert.True(expanded.Length < settled * 0.7, $"A* expanded {expanded.Length}, Dijkstra settled {settled}.");

        // The route is a real one: it starts and ends where asked and every step is an edge.
        var nodes = route.RouteTo(target);
        Assert.Equal(source, nodes[0]);
        Assert.Equal(target, nodes[^1]);
        for (int k = 1; k < nodes.Length; k++)
            Assert.Contains(nodes[k], graph.Neighbours(nodes[k - 1]).ToArray());
    }

    /// <summary>With nothing to go on it is Dijkstra: the same cost, and no cheaper to run.</summary>
    [Fact]
    public void AnEstimateOfZeroIsDijkstra()
    {
        var (graph, zero) = WalledGrid();
        int source = 2 * Side + 2, target = (Side - 3) * Side + 4;

        var reference = Dijkstra.From(graph, new[] { source }, targets: new[] { target });
        var route = AStar.Route(graph, source, target, zero, null, out int[] expanded);

        Assert.Equal(reference.Cost[target], route.Cost[target], 9);
        Assert.Equal(Enumerable.Range(0, graph.NodeCount).Count(reference.Reaches), expanded.Length);
    }

    /// <summary>
    /// An estimate that never overestimates but is not consistent: it sends the search
    /// through B the dear way first, and only then finds the cheap way into B. Without
    /// re-opening B the answer is 6. It is 5.
    /// </summary>
    [Fact]
    public void ReopensANodeWhenACheaperWayInTurnsUp()
    {
        const int s = 0, a = 1, b = 2, t = 3;
        var graph = WeightedGraph.FromEdges(4, new[] { (s, a, 1.0), (a, b, 1.0), (s, b, 3.0), (b, t, 3.0) });
        var estimate = new[] { 0.0, 4.0, 0.0, 0.0 };

        var route = AStar.Route(graph, s, t, node => estimate[node], null, out int[] expanded);

        Assert.Equal(5.0, route.Cost[t]);
        Assert.Equal(new[] { s, a, b, t }, route.RouteTo(t));
        Assert.Equal(new[] { s, b, a, b, t }, expanded);
    }

    /// <summary>Only the route is reported: what was opened on the way holds bounds, not answers.</summary>
    [Fact]
    public void ReportsTheRouteAndNothingElse()
    {
        var graph = WeightedGraph.FromEdges(5, new[] { (0, 1), (1, 2), (0, 3), (3, 4) });
        var route = AStar.Route(graph, 0, 2, _ => 0.0);

        Assert.Equal(new[] { 0, 1, 2 }, route.RouteTo(2));
        Assert.False(route.Reaches(3));
        Assert.False(route.Reaches(4));
    }

    [Fact]
    public void ATargetNothingReachesReadsAsUnreached()
    {
        var graph = WeightedGraph.FromEdges(4, new[] { (0, 1), (2, 3) });
        var route = AStar.Route(graph, 0, 3, _ => 0.0);

        Assert.False(route.Reaches(3));
        Assert.Empty(route.RouteTo(3));
    }

    /// <summary>
    /// One-way streets, and two free steps that lead into each other. The route must
    /// follow the arcs, and the tie between the free steps must not leave two nodes as
    /// each other's previous - which would make reading the route back never end.
    /// </summary>
    [Fact]
    public void FollowsArcsOneWayAndSurvivesFreeStepsBothWays()
    {
        var graph = DirectedGraph.FromArcs(5,
            new[] { (0, 2, 1.0), (2, 1, 0.0), (1, 2, 0.0), (1, 3, 1.0), (3, 0, 1.0), (4, 3, 1.0) },
            DuplicateArcs.KeepSmallest);

        var route = AStar.Route(graph, 0, 3, _ => 0.0);
        Assert.Equal(2.0, route.Cost[3]);
        Assert.Equal(new[] { 0, 2, 1, 3 }, route.RouteTo(3));

        Assert.False(AStar.Route(graph, 0, 4, _ => 0.0).Reaches(4));   // only an arc out of 4
        Assert.Equal(Dijkstra.From(graph, new[] { 3 }).Cost[1], AStar.Route(graph, 3, 1, _ => 0.0).Cost[1]);
    }

    [Fact]
    public void RefusesAnEstimateThatIsNotANumberOrIsNegative()
    {
        var graph = WeightedGraph.FromEdges(3, new[] { (0, 1), (1, 2) });

        Assert.Throws<InvalidOperationException>(() => AStar.Route(graph, 0, 2, _ => double.NaN));
        Assert.Throws<InvalidOperationException>(() => AStar.Route(graph, 0, 2, _ => -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AStar.Route(graph, 0, 3, _ => 0.0));
    }
}

using OtterLogic.Graphs;
using Xunit;

namespace OtterLogic.Graphs.Tests;

public class CondensationTests
{
    /// <summary>A chain of dependence: each node one higher than what it depends on.</summary>
    [Fact]
    public void AChainRanksEachNodeAboveWhatItDependsOn()
    {
        var result = Condensation.Of(4, new[] { (3, 2), (2, 1), (1, 0) });

        Assert.Equal(4, result.ComponentCount);
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4).Select(result.HeightOf).ToArray());
    }

    /// <summary>Nodes that depend on each other share a rank, and what hangs off the loop sits above it.</summary>
    [Fact]
    public void ACycleIsOneComponentAtOneHeight()
    {
        var result = Condensation.Of(5, new[] { (1, 2), (2, 3), (3, 1), (1, 0), (4, 2) });

        Assert.Equal(3, result.ComponentCount);
        Assert.Equal(result.Component[1], result.Component[2]);
        Assert.Equal(result.Component[2], result.Component[3]);
        Assert.Equal(0, result.HeightOf(0));
        Assert.Equal(1, result.HeightOf(2));
        Assert.Equal(2, result.HeightOf(4));
    }

    /// <summary>Height is the longest chain below, not the shortest.</summary>
    [Fact]
    public void HeightFollowsTheLongestChain()
    {
        var result = Condensation.Of(4, new[] { (3, 0), (3, 2), (2, 1), (1, 0) });
        Assert.Equal(3, result.HeightOf(3));
    }

    /// <summary>A chain far longer than any call stack would take.</summary>
    [Fact]
    public void ALongChainDoesNotOverflow()
    {
        const int n = 200_000;
        var result = Condensation.Of(n, Enumerable.Range(1, n - 1).Select(i => (i, i - 1)));
        Assert.Equal(n - 1, result.HeightOf(n - 1));
    }

    /// <summary>
    /// A topological order puts everything a node depends on before it. Read off the
    /// heights, so it needs checking against the arcs rather than against a search.
    /// </summary>
    [Fact]
    public void TheOrderPutsEveryDependencyFirst()
    {
        var arcs = new[] { (5, 2), (5, 0), (4, 0), (4, 1), (2, 3), (3, 1) };
        var result = Condensation.Of(6, arcs);
        var order = result.TopologicalOrder();

        Assert.True(result.IsAcyclic);
        Assert.Equal(Enumerable.Range(0, 6), order.OrderBy(i => i));

        var position = new int[6];
        for (int k = 0; k < order.Length; k++)
            position[order[k]] = k;

        foreach (var (from, to) in arcs)
            Assert.True(position[to] < position[from], $"{to} must come before {from}");

        // Lowest height first and by index within it, so the order is the graph's.
        Assert.Equal(new[] { 0, 1, 3, 4, 2, 5 }, order);
    }

    [Fact]
    public void ACycleIsNotAcyclic()
    {
        Assert.False(Condensation.Of(3, new[] { (0, 1), (1, 2), (2, 0) }).IsAcyclic);
    }
}

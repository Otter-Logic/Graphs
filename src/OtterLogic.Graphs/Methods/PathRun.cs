namespace OtterLogic.Graphs.Methods;

/// <summary>
/// A graph, a question and a method in; an answer a person can act on out.
/// <para>
/// The one entry point the graph component calls, so every decision between a
/// wire and a coloured model lives here rather than in an adaptor: which method
/// runs when none was chosen, and whether what came back fits the graph it is
/// about. Any method goes through the same call, and a toolkit that wants exactly
/// what the component does calls this and gets it.
/// </para>
/// </summary>
public static class PathRun
{
    /// <summary>
    /// Answers <paramref name="query"/> with <paramref name="method"/>, or chooses a
    /// method from what the query carries when none is given.
    /// </summary>
    /// <param name="query">The graph, its positions if any, and the sources and targets the question is about.</param>
    /// <param name="method">The method, or null to read the question off the query and pick one, with the reason in the outcome.</param>
    /// <exception cref="ArgumentException">The query lacks what the method needs, or a setting is outside what it accepts; the message says which.</exception>
    /// <exception cref="InvalidOperationException">The graph's weights are ones the method cannot run on.</exception>
    public static PathOutcome Solve(PathQuery query, GraphMethod? method = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        method ??= new AutoMethod();

        var outcome = method.Run(query);
        outcome.Check(query.NodeCount, query.ConnectionCount);
        return outcome;
    }
}

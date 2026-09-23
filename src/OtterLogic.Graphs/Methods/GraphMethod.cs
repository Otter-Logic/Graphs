namespace OtterLogic.Graphs.Methods;

/// <summary>
/// A graph algorithm with its settings chosen, ready to be handed a graph and a
/// question.
/// <para>
/// The mechanism behind one wire. A Grasshopper user picks a method component —
/// Dijkstra, Potential Flow — sets its one or two settings if it has any, and
/// wires the result into the one component that holds the graph. The method knows
/// nothing about where the graph came from; the graph component knows nothing
/// about how any method works. Adding an algorithm is one record here and one
/// small component there, and the graph component never changes.
/// </para>
/// <para>
/// A record rather than an interface so that a method is a value: two with the
/// same settings are equal, and one can be copied with one setting changed. Each
/// concrete record calls the algorithm's own entry point rather than replacing
/// it, so that entry point stays the one every test and toolkit calls.
/// </para>
/// </summary>
public abstract record GraphMethod
{
    /// <summary>The algorithm's name as a user knows it: "Dijkstra", "Potential Flow".</summary>
    public abstract string Name { get; }

    /// <summary>The name and the settings that matter, in one line for a report.</summary>
    public abstract string Describe();

    /// <summary>
    /// Answers the question on the graph.
    /// </summary>
    /// <exception cref="ArgumentException">The query lacks what this method needs, or a setting is outside what it accepts; the message says which.</exception>
    /// <exception cref="InvalidOperationException">The graph's weights are ones this method cannot run on.</exception>
    public abstract PathOutcome Run(PathQuery query);

    public sealed override string ToString() => Describe();

    /// <summary>What a method only defined on an undirected graph says when it had to forget direction.</summary>
    protected string DirectionIgnored
        => $"The graph is directed and {Name} is only defined on an undirected one, so direction was ignored: "
           + "an arc and its reverse were read as one two-way connection.";

    /// <summary>What a method that scores the whole graph says when it was given ends it has no use for.</summary>
    protected string EndsIgnored
        => $"Sources and Targets play no part in {Name}; the whole graph is scored.";

    /// <summary>"3 nodes" or "1 node".</summary>
    protected static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}

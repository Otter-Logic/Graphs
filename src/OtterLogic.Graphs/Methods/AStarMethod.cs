namespace OtterLogic.Graphs.Methods;

/// <summary>Which straight line A* may steer by, given the graph's weights.</summary>
public enum EstimateMetric
{
    /// <summary>Distance in three dimensions: every connection weighs at least its own 3D length times the scale.</summary>
    ThreeDimensions,

    /// <summary>Distance in plan: only the length ignoring z is vouched for — the case for a graph weighted in plan.</summary>
    Plan,

    /// <summary>Neither is safe: some connection weighs less than its length, so the estimate may overshoot.</summary>
    Unsafe,
}

/// <summary>
/// A* as a method on a wire: the route Dijkstra would find from one source to one
/// target, found by looking towards the target rather than evenly in every
/// direction.
/// <para>
/// The one setting is what a unit of straight-line distance is worth in the
/// graph's weights, at the least: 1 when weights are lengths, one over the fastest
/// speed when they are times. Everything else — whether to measure that line in
/// three dimensions or in plan, and whether the weights can vouch for it at all —
/// is decided here from the graph itself, because it is arithmetic on the weights
/// and positions and not a thing to ask a person.
/// </para>
/// </summary>
public sealed record AStarMethod : GraphMethod
{
    /// <summary>
    /// What one unit of straight-line distance is worth in the graph's weights, at
    /// the very least. 0 switches the estimate off, which makes this Dijkstra.
    /// </summary>
    public double EstimateScale { get; init; } = 1.0;

    public override string Name => "A*";

    public override string Describe() => $"{Name}, estimate scale {EstimateScale:0.###}";

    public override PathOutcome Run(PathQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (double.IsNaN(EstimateScale) || double.IsInfinity(EstimateScale) || EstimateScale < 0.0)
            throw new ArgumentOutOfRangeException(nameof(EstimateScale), EstimateScale, "Estimate Scale must be zero or more.");

        query.RequirePositions(Name);
        query.RequireOneToOne(Name);

        int source = query.Sources[0], target = query.Targets[0];
        int n = query.NodeCount;

        var metric = ChooseMetric(query, EstimateScale, out double safeScale);
        var warnings = new List<string>();
        if (metric == EstimateMetric.Unsafe)
            warnings.Add(
                $"Some connections weigh less than {EstimateScale:0.###} times their straight-line length, so the "
                + $"estimate can overshoot and the route may not be the cheapest. Lower Estimate Scale to "
                + $"{Math.Max(safeScale, 0.0):0.###} or less, or use Dijkstra.");

        double scale = EstimateScale;
        bool inPlan = metric != EstimateMetric.ThreeDimensions;
        double Estimate(int node) => scale * (inPlan ? query.DistanceInPlan(node, target) : query.Distance(node, target));

        int[] expanded;
        var tree = query.DirectedOrNull is { } directed
            ? AStar.Route(directed, source, target, Estimate, null, out expanded)
            : AStar.Route(query.UndirectedOrNull!, source, target, Estimate, null, out expanded);

        var route = tree.RouteTo(target);
        var remarks = new List<string>();
        if (route.Length == 0)
            remarks.Add("No route reaches the target. Connected Pieces shows whether the two are in the same piece"
                        + (query.IsDirected ? "; in a directed graph the arcs may simply not lead there." : "."));

        int looked = expanded.Distinct().Count();

        return new PathOutcome
        {
            Method = Name,
            Routes = new[] { route },
            RouteEnds = new[] { target },
            Values = Routing.Values(tree),
            ValuesName = "Cost",
            Marked = expanded,
            MarkedName = "Explored, in the order the search looked",
            Warnings = warnings,
            Remarks = remarks,
            Details = new[]
            {
                route.Length == 0
                    ? $"No route from {source} to {target}; {looked} of {n} nodes were looked at before giving up."
                    : $"Route from {source} to {target} costs {tree.Cost[target]:0.###}, found after looking at {looked} of {n} nodes.",
                metric switch
                {
                    EstimateMetric.ThreeDimensions => "Steered by straight-line distance in three dimensions.",
                    EstimateMetric.Plan => "Steered by straight-line distance in plan, which is all the weights vouch for.",
                    _ => "Steered by straight-line distance in plan, which the weights do not fully vouch for.",
                },
            },
        };
    }

    /// <summary>
    /// Picks the longest straight line the weights can vouch for, and says when
    /// they can vouch for none.
    /// <para>
    /// A straight-line estimate is safe exactly when no connection weighs less than
    /// scale times its own length: then no route can beat the straight line, and the
    /// estimate never drops by more than the step it drops across. Distance in three
    /// dimensions is the stronger estimate and is used when every connection clears
    /// that bar. A graph weighted by length in plan falls short of it on every
    /// sloping connection, so the fallback is distance in plan.
    /// </para>
    /// </summary>
    /// <param name="safeScale">The largest scale the weights would vouch for in plan; what to lower to when the answer is Unsafe.</param>
    public static EstimateMetric ChooseMetric(PathQuery query, double scale, out double safeScale)
    {
        ArgumentNullException.ThrowIfNull(query);
        safeScale = double.PositiveInfinity;

        if (scale == 0.0 || !query.HasPositions)
            return EstimateMetric.ThreeDimensions;

        double tightest3D = double.PositiveInfinity, tightestPlan = double.PositiveInfinity;
        foreach (var (a, b, weight) in query.Connections())
        {
            double length = query.Distance(a, b);
            double plan = query.DistanceInPlan(a, b);

            if (length > 0.0) tightest3D = Math.Min(tightest3D, weight / length);
            if (plan > 0.0) tightestPlan = Math.Min(tightestPlan, weight / plan);
        }

        safeScale = tightestPlan;

        const double slack = 1.0 - 1e-9;
        if (tightest3D >= scale * slack)
            return EstimateMetric.ThreeDimensions;
        if (tightestPlan >= scale * slack)
            return EstimateMetric.Plan;

        return EstimateMetric.Unsafe;
    }
}

# Graphs

The graph contract under the OtterLogic Rhino tools, and the algorithms that run
on it: routes, flows, and readings of which nodes a graph depends on. Nothing
here is trained and nothing here learns — every answer is a function of the
edges it was handed.

A foundation beside [Core](https://github.com/Otter-Logic/Core). It references
nothing, which is what lets every other layer reference it.

```
       Core     Graphs                    ← this repo
                  ↑
           MachineLearning
                  ↑
   Unsupervised  Supervised  Reinforcement  DeepLearning
                  ↑
   StructuralForm  StructuralDesign  Fabrication  FormFinding  Document
```

A domain toolkit may reference it directly. Ordering a toolpath or routing a duct
is not machine learning, and a toolkit doing only that should not carry an ML
library to get a shortest path.

## Why it is its own repo

All of this began in MachineLearning, because spectral clustering was the first
thing to need a graph. It moved down for the same reason anything moves down in
this organisation: a second kind of user arrived. Load paths in StructuralDesign
already route over it with no learning involved, and toolpaths, erection
sequences and service routing want the same type. Two layers needing one thing
means it belongs beneath both.

It is named for the field rather than for an algorithm, so the next method has
somewhere to go. `PathFinding` would have had nowhere to put a maximum flow.

Inside it the naming runs the other way, by one rule. Where several algorithms
answer one question a class is named for its algorithm — `Dijkstra`,
`BreadthFirst`, `AStar` — because the caller is choosing between
them. Where one algorithm is the sensible answer it is named for the question —
`CutVertices`, `Condensation` — because that is what the caller is looking for.

## What is here

**`WeightedGraph`** — which nodes are related and how strongly, as sorted sparse
rows, with connected components, reweighting, and the symmetric normalised
propagation every graph method above shares. Spectral clustering, constrained
hierarchies and message passing in Unsupervised consume it, and a trained graph
network in DeepLearning will want exactly the same input. Where the edges come
from is the caller's business: a toolkit that knows which of its elements touch
builds the graph from that.

**`DirectedGraph`** is the same idea with connections that run one way: an arc
from a tail to a head. A type of its own rather than a switch on `WeightedGraph`,
because half of what runs on that graph is only true of an undirected one — a
symmetric Laplacian, a cut vertex — and with two types a signature says which it
needs and the mistake does not compile. It differs on purpose in three ways: an
arc and its reverse are two arcs; a zero weight is kept, since a free transfer or
a dependency that takes no time is a real arc where a zero similarity is no
relationship; and any finite weight is allowed. A repeated arc is folded the way
the caller says — smallest for a cost, sum for a capacity — with no default,
because there is no right one. It holds its rows both ways round, so "what
depends on this" is as cheap as "what does this depend on", and every arc has
an id so that a capacity, a flow or a duration lives in a plain array beside it.
`WeightedGraph.ToDirected()` loses nothing; `DirectedGraph.ToUndirected()` loses
direction, and asks how the two ways merge.

**`Dijkstra`** finds the cheapest routes from a set of sources, with the cost of
each edge supplied by the caller rather than read off its similarity weight, and
ties broken by index so a route never flips between equal choices from one solve
to the next. Several sources are one solve, which is what makes "the nearest
exit" cheap; optional targets stop the search as soon as the last is reached.

**`BreadthFirst`** is the same question with every step costing one — fewest
steps, no priority queue — and is held to Dijkstra's answer node for node.

**`AStar`** finds the same cheapest route between two nodes while looking at far
less of the graph, by ordering nodes on cost so far *plus an estimate of what is
left*. The estimate is the caller's — a function from a node to a lower bound on
its remaining cost, straight-line distance being the usual one — because this
layer has no idea where a node is. It must never overestimate; zero everywhere
makes this Dijkstra. A node is re-opened when a cheaper way into it turns up,
which costs nothing with a consistent estimate and is what keeps the answer
right with one that is merely never too high. Tests hold it to Dijkstra's cost,
and to expanding well under what Dijkstra settles.

All three searches run on either graph type through one loop each, and return a
**`RouteTree`**, so a caller reading a route does not care which found it or
whether the streets were one-way.

**`Centrality`** measures how much of a graph's traffic passes through each node —
betweenness by hop count, exact up to a couple of thousand nodes and estimated
from evenly spread sources beyond.

**`CutVertices`** measures how much of a graph each node alone holds on, and from
the same low-link pass reports the articulation points and the bridges — the
edges that are the only connection between what lies either side of them.

**`PotentialFlow`** answers the question a route cannot — not which way is nearest
but how much passes through here — by solving the graph Laplacian with some nodes
grounded: equal routes share the flow, so a symmetric graph gets a symmetric
answer, where a route search has to break the tie one way and make mirror-image
nodes differ.

**`Condensation`** ranks the nodes of a directed graph by what they depend on,
folding every cycle into one component first, since dependence that runs both
ways is not a hierarchy and forcing an order onto it would only record which arc
the search met first. Its result gives the strongly connected components, the
heights, and a topological order read off them.

**`Planar/`** prepares a graph from a drawing, in plan. `PlanarObstacles` holds
closed outlines nothing may pass through and answers two questions — is this
point inside one, does this straight step pass through one — with the boundary
counted as outside, because the shortest way round an obstacle touches its
corners and a rule that refused contact would refuse every shortest path. A
step is judged by cutting it wherever it meets a boundary and testing the middle
of each piece, not by looking for crossed edges, which misses the diagonal
between two corners of one obstacle and wrongly refuses a step that only grazes
a corner. `VisibilityGraph` joins a caller's points and every obstacle corner
wherever one sees another, weighted by distance; `Dijkstra` over it returns the
true shortest path through the open plane rather than the best a grid offered.

**`Spatial/`** does the same in three dimensions. `SolidObstacles` holds
triangulated meshes — a closed one is a solid with an inside, an open one a wall
that blocks only what crosses it — and answers the same two questions on the same
terms as the planar case: the surface is not inside, a step is cut wherever it
meets a face and each piece judged by its middle, and a seam between two
coplanar triangles is not an edge, because a triangulation is not a shape.
`LineNetwork` welds the ends of drawn lines into nodes within a tolerance, the
earliest node on a tie so the numbering is a function of input order alone, and
weighs each connection by the distance between its nodes unless told otherwise.

**`Methods/`** is every algorithm above as a method on a wire, mirroring
`ClusteringMethod` in Unsupervised. `GraphMethod` is a record with a name, a
one-line description and a `Run`; `PathQuery` is what it is handed — the graph of
either kind, n x 3 positions when there are any, and the sources and targets the
question is about; `PathOutcome` is what comes back, reduced to the five shapes an
answer about a graph can take, each named: routes, a value per node, a value per
connection, groups of nodes, and nodes singled out. `PathRun.Solve` is the one
entry point an adaptor calls, and `AutoMethod` is what runs when no method was
chosen: pieces with nothing wired, Dijkstra from the sources, A* for one source
and one target on a placed graph when the weights vouch for the estimate. The
choice of measuring that estimate in three dimensions or in plan lives in
`AStarMethod.ChooseMetric`, because it is arithmetic on the weights and not a
thing to ask a person. Notes travel as two lists of strings, warnings and
remarks, since this repo does not reference Core's `Note`.

## What is deliberately not here

**Anything that knows what a sample is.** The k-nearest-neighbour graph of a point
cloud is `NeighbourGraph.Of` in MachineLearning, because building it means
measuring a distance between rows of a feature matrix, and the one copy of that
distance lives there. A graph here is nodes and edges.

**Any opinion about what the nodes are.** No rule for which elements of a model
count as connected, no cost that knows a beam from a brace, no calling a cut
vertex a "critical member". Those are claims about a domain and live in the
toolkit that has one. The line is the one the ML stack uses: if changing it would
require knowing what a bending moment is, it does not belong here.

**Geometry types.** No `Point3d`, no curves, no reference to Rhino. Where
positions matter — `Planar/` — they arrive as plain arrays of x and y that an
adaptor filled in from whatever it holds. That is what keeps this repo
referencing nothing and its tests runnable with no Rhino.

**Finding neighbours among samples.** A k-nearest graph weighted by distance is
`NeighbourGraph.ByDistance` in MachineLearning, beside the one copy of the
distance it is built on. It takes what blocks an edge as a predicate, which is
how `PlanarObstacles` reaches it without either layer naming the other's types.

## Layout

```
src/OtterLogic.Graphs/   the graph type and one file per algorithm
  Planar/                obstacles in plan, and the visibility graph over them
  Spatial/               obstacles in space, and line ends welded into a network
  Methods/               each algorithm as a method record, and the one entry point that runs them
tests/                   xunit; runs anywhere, no Rhino needed
```

`WeightedGraph.Propagate` is checked against dense arithmetic in MachineLearning's
tests rather than here: the reference graph is scikit-learn's nearest-neighbour
graph, and both the fixture and the code that builds that graph live up there.

## Dependencies

None, deliberately — not even Core. A binary heap, a stack and compressed sparse
rows are all any of this needs.

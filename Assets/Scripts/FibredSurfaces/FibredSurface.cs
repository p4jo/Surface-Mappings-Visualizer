using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MathNet.Numerics.LinearAlgebra;
using QuikGraph;
using QuikGraph.Algorithms;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

/// <summary>
/// This implements the Bestvina-Handel algorithm.
/// All methods are in-place. Copy the graph before.
/// </summary>
public class FibredSurface : IPatchedDrawnsformable
{
    public readonly FibredGraph graph;

    /// <summary>
    /// The peripheral subgraph P contains one loop around every puncture of the surface apart from one orbit of punctures.
    /// P is assumed to have only valence-two vertices (that have higher valence in the main graph).
    /// We also assume that g acts on it as a graph automorphism.
    /// </summary>
    public readonly FibredGraph peripheralSubgraph;

    public GeodesicSurface surface;

    public IEnumerable<IDrawnsformable> Patches =>
        graph.Vertices.Concat<IDrawnsformable>(from edge in Strips select edge.Curve);

    public IEnumerable<UnorientedStrip> Strips => graph.Edges;
    private IEnumerable<Strip> OrientedEdges => Strips.Concat<Strip>(Strips.Select(edge => edge.Reversed()));

    #region Constructors

    public FibredSurface(FibredGraph graph, GeodesicSurface surface, FibredGraph peripheralSubgraph = null)
    {
        this.graph = graph;
        this.surface = surface;
        this.peripheralSubgraph = peripheralSubgraph ?? new FibredGraph(true);
        DeferNames();
    }

    public FibredSurface(IList<(Curve, string, string, string[])> edgeDescriptions, GeodesicSurface surface,
        IEnumerable<string> peripheralEdges = null, IDictionary<string, IDrawnsformable> junctionDrawables = null)
    {
        this.surface = surface;
        junctionDrawables ??= new Dictionary<string, IDrawnsformable>();
        peripheralEdges ??= new List<string>();
        graph = new FibredGraph(true); // up here, so that the junctions reference this

        var vertexNames =
            (from v in edgeDescriptions select v.Item2)
            .Concat(from v in edgeDescriptions select v.Item3).ToHashSet();
        var junctions = new Dictionary<string, Junction>(
            from name in vertexNames
            select new KeyValuePair<string, Junction>(name,
                new Junction(graph,
                    junctionDrawables.TryGetValue(name, out var displayable)
                        ? displayable
                        : edgeDescriptions.FirstOrDefault(tuple => tuple.Item2 == name).Item1?.StartPosition
                          ?? edgeDescriptions.FirstOrDefault(tuple => tuple.Item3 == name).Item1?.EndPosition, name)
            )
        );
        var strips = (
            from tuple in edgeDescriptions
            let curve = tuple.Item1
            let source = tuple.Item2
            let target = tuple.Item3
            let startVector = curve.StartVelocity.Coordinates(surface)
            let endVector = -curve.EndVelocity.Coordinates(surface)
            let startAngle = startVector.ToComplex().Phase
            let endAngle = endVector.ToComplex().Phase
            select new UnorientedStrip(
                curve,
                junctions[source],
                junctions[target],
                new List<Strip>(),
                graph,
                (float)startAngle,
                (float)endAngle
            )
        ).ToList();
        var edges = new Dictionary<string, Strip>((
                from strip in strips select new KeyValuePair<string, Strip>(strip.Name.ToLower(), strip)
            ).Concat(
                from strip in strips select new KeyValuePair<string, Strip>(strip.Name.ToUpper(), strip.Reversed())
            )
        );
        foreach (var (strip, edgePathText) in strips.Zip(edgeDescriptions, (strip, tuple) => (strip, tuple.Item4)))
        {
            strip.EdgePath = edgePathText.Select(edgeName => edges[edgeName]).ToList();
        }

        foreach (var (name, strip) in edges)
        {
            if (strip.Source.image != null && strip.Source.image != strip.Dg?.Source)
                Debug.LogError(
                    $"Two edges at the same vertex have images that don't start at the same vertex! g({name}) = {string.Join(' ', strip.EdgePath.Select(e => e.Name))} starts at o(g({name})) = {strip.Dg?.Source}, but we already set g(o({name})) = {strip.Source.image}.");
            strip.Source.image ??= strip.Dg?.Source;
        }

        graph.AddVerticesAndEdgeRange(strips);

        peripheralSubgraph = new FibredGraph(true);
        peripheralSubgraph.AddVerticesAndEdgeRange((
                from edgeName in peripheralEdges
                select edges[edgeName].UnderlyingEdge
            ).Distinct()
        );
        DeferNames();
    }

    public string Name { get; set; }

    public Color Color
    {
        get => graph.Vertices.First().Color;
        set => Debug.LogError("The graph color should not be set.");
    }

    IPatchedDrawnsformable IDrawnsformable<IPatchedDrawnsformable>.Copy() => Copy();

    public FibredSurface Copy()
    {
        var newGraph = new FibredGraph(true);

        var newVertices = new Dictionary<Junction, Junction>(
            from vertex in graph.Vertices
            select new KeyValuePair<Junction, Junction>(vertex, vertex.Copy(newGraph))
        );

        var newEdges = new Dictionary<UnorientedStrip, UnorientedStrip>(graph.EdgeCount);
        foreach (var edge in Strips)
        {
            newEdges[edge] = edge.CopyUnoriented(graph: newGraph, source: newVertices[edge.Source],
                target: newVertices[edge.Target]);
        }

        foreach (var (edge, newEdge) in newEdges)
            newEdge.EdgePath = edge.EdgePath.Select(
                strip => strip is OrderedStrip { reverse: true }
                    ? newEdges[strip.UnderlyingEdge].Reversed() as Strip
                    : newEdges[strip.UnderlyingEdge] as Strip
            ).ToList();

        newGraph.AddVerticesAndEdgeRange(newEdges.Values);

        var newPeripheralSubgraph = new FibredGraph(true);
        newPeripheralSubgraph.AddVerticesAndEdgeRange(
            from edge in peripheralSubgraph.Edges select newEdges[edge]
        );

        return new FibredSurface(newGraph, surface, newPeripheralSubgraph);
    }

    public void SetMap(IDictionary<string, string[]> map)
    {
        if (!map.Keys.ToHashSet().IsSupersetOf(Strips.Select(s => s.Name)))
            throw new ArgumentException("The map must contain a string array for each edge of the surface.");
        var edges = Strips.ToDictionary(s => s.Name);
        foreach ((string name, var strip) in edges)
            strip.EdgePath = map[name].Select(edgeName => edges[edgeName]).ToList();
    }

    #endregion

    #region Algorithm with suggestion system

    public class BHDisplayOptions
    {
        public IEnumerable<object> options;
        public string description;
        public IEnumerable<object> buttons;
        public bool allowMultipleSelection = false;
    }

    public BHDisplayOptions NextSuggestion()
    {
        // todo? add a set of ITransformables to highlight with each option?
        // Iterate through the steps and give the possible steps to the user
        var a = GetInvariantSubforests();
        if (a.Any())
            return new BHDisplayOptions()
            {
                options = from subforest in a
                    select subforest.WithToString(
                        string.Join(", ", subforest.Edges.Select(v => v.Name))
                    ),
                description = "Collapse an invariant subforest.",
                buttons = new[] { "Collapse" }
            };
        var b = GetLoosePositions();
        if (b.Any())
            return new BHDisplayOptions()
            {
                options = b.Cast<object>(),
                description = "Pull tight at one or more edges.",
                buttons = new[] { "Tighten All", "Tighten Selected" },
                allowMultipleSelection = true
            };
        var
            c = GetValenceOneJunctions(); // shouldn't happen at this point (tight & no invariant subforests => no valence-one junctions)
        if (c.Any())
            return new BHDisplayOptions()
            {
                options = c,
                description = "Remove a valence-one junction. WHAT THE HECK?",
                buttons = new[] { "Valence-1 Removal" },
                allowMultipleSelection = true
            };
        if (AbsorbIntoPeriphery(true))
            return new BHDisplayOptions()
            {
                options = new[] { GetMaximalPeripheralSubgraph() }, // todo? Display Q?
                description = "Absorb into periphery.",
                buttons = new[] { "Absorb" }
            };
        // todo: At this step, if the transition matrix for H is reducible, return with a reduction.
        var d = GetValenceTwoJunctions();
        if (d.Any())
            return new BHDisplayOptions()
            {
                options = d,
                description = "Remove a valence-two junction.",
                buttons = new[] { "Remove Selected", "Remove All" },
                allowMultipleSelection = true
            };
        var e = GetPeripheralInefficiencies();
        if (e.Any())
            return new BHDisplayOptions()
            {
                options = e,
                description = "Fold initial segments of edges that map to edges in the pre-periphery.",
                buttons = new[] { "Fold Selected" }
            };
        var f = GetInefficiencies();
        if (f.Any())
            return new BHDisplayOptions()
            {
                options = f.OrderBy(inefficiency => inefficiency.order),
                description = "Remove an inefficiency.",
                buttons = new[] { "Remove Inefficiency" },
            };

        return null; // finished!
    }

    public void ApplySuggestion(IEnumerable<object> suggestion, object button)
    {
        switch (button)
        {
            case "Collapse":
                if (suggestion.FirstOrDefault() is Helpers.ObjectWithString { obj: FibredGraph subforest })
                    CollapseInvariantSubforest(subforest);
                // only select one subforest at a time because they might intersect and then the other subforests wouldn't be inside the new graph anymore.
                break;
            case "Tighten Selected":
                foreach (object o in suggestion)
                {
                    if (o is not ValueTuple<Strip, EdgePoint[], Junction[]> s) continue;

                    var backTracks = s.Item2.ToArray();
                    // ReSharper disable once ForCanBeConvertedToForeach // we modify the list!
                    for (var i = 0; i < backTracks.Length; i++)
                        PullTightBackTrack(backTracks[i], backTracks);

                    foreach (var junction in s.Item3.ToArray())
                        PullTightExtremalVertex(junction);
                    // todo: pulling tight along an edge and along the reverse edge do not commute! If you do both, some extremal vertices might not be extremal anymore, and some backtracks might not be backtracks anymore. (e.g. if St(v) = {e,f} and g(e) = dDa, g(f) = d, then v is extremal and e has a backtrack, both in d. But ?? maybe no problem?? TODO 
                }

                break;
            case "Tighten All":
                for (int i = 0; i < Strips.Sum(e => e.EdgePath.Count) + graph.Vertices.Count(); i++)
                {
                    var extremalVertex = GetExtremalVertices().FirstOrDefault();
                    if (extremalVertex != null)
                    {
                        PullTightExtremalVertex(extremalVertex);
                        continue;
                    }

                    var backTrack = GetBackTracks().FirstOrDefault();
                    if (backTrack != null)
                    {
                        PullTightBackTrack(backTrack);
                        continue;
                    }

                    break;
                }

                break;
            case "Valence-1 Removal":
                foreach (var junction in suggestion.Cast<Junction>())
                    RemoveValenceOneJunction(junction);
                break;
            case "Absorb":
                AbsorbIntoPeriphery();
                break;
            case "Remove Selected":
                foreach (object o in suggestion.ToArray())
                {
                    if (o is not Junction valence2Junction) continue;
                    // todo: same problem as in Remove All
                    RemoveValenceTwoJunction(valence2Junction, null);
                }

                break;
            case "Remove All":
                var maxSteps = graph.VertexCount;
                for (int i = 0; i < maxSteps; i++)
                {
                    var junction = GetValenceTwoJunctions().FirstOrDefault();
                    // todo: at some point there might be invariant subforests (we didn't check for earlier steps!!)
                    if (junction == null) break;
                    RemoveValenceTwoJunction(junction);
                }

                break;
            case "Fold Selected":
                if (suggestion.FirstOrDefault() is IEnumerable<Strip> edges)
                    RemovePeripheralInefficiency(edges.ToList());
                break;
            case "Remove Inefficiency":
                int index = 0;
                if (index != 0) Debug.Log($"Debuggingly selected the {index}th inefficiency instead of the 0th"); // 
                if (suggestion.ElementAtOrDefault(index) is Inefficiency inefficiency)
                    RemoveInefficiency(inefficiency);
                break;
            default:
                Debug.LogError("Unknown button.");
                break;
        }

        var edgeWithBrokenEdgePath = Strips.FirstOrDefault(s =>
            s.EdgePath.Count != 0 && !Enumerable.Range(0, s.EdgePath.Count - 1)
                .All(i => s.EdgePath[i].Target == s.EdgePath[i + 1].Source));
        if (edgeWithBrokenEdgePath != null)
            Debug.LogError($"The edge {edgeWithBrokenEdgePath} has a broken edge path.");
        var brokenSelfEdge = Strips.FirstOrDefault(s => s.Source == s.Target && s.EdgePath.Count == 0);
        if (brokenSelfEdge != null)
            Debug.LogError(
                $"The edge {brokenSelfEdge} is a self-edge that gets mapped into a vertex! This should not happen as we assume that the fibred surface is embedded as a deformation retract of the surface and thus no loop should be mapped to a vertex (no non-forest into a forest).");
        var duplicateName = graph.Edges.FirstDuplicate(e => e.Name);
        if (duplicateName != null)
            Debug.LogError($"The name of {duplicateName} is used twice.");
    }

    public bool ApplyNextSuggestion()
    {
        var suggestion = NextSuggestion();
        if (suggestion == null) return false; // done!
        ApplySuggestion(suggestion.options, suggestion.buttons.First()); // selected ones (all!)
        return true;
    }

    public void BestvinaHandelAlgorithm()
    {
        var limit = 20 * graph.EdgeCount;
        for (int i = 0; i < limit; i++)
        {
            if (!ApplyNextSuggestion()) break;
        }
        // todo: save result (reducible / growth)
    }

    #endregion

    #region Graph operations

    public string GraphString()
    {
        var edgeString = string.Join("\n", Strips.Select(edge => edge.ToColorfulString()));
        return edgeString;
    }

    public static IEnumerable<Strip> Star(Junction junction)
    {
        return junction.graph.AdjacentEdges(junction).SelectMany(strip =>
            strip.IsSelfEdge() ? new[] { strip, strip.Reversed() } :
            strip.Source != junction ? new[] { strip.Reversed() } :
            new[] { strip });
    }

    public static IEnumerable<Strip> StarOrdered(Junction junction, Strip firstEdge = null)
    {
        var orderedStar = Star(junction).OrderBy(strip => strip.OrderIndexStart);
        return firstEdge != null ? orderedStar.CyclicShift(firstEdge) : orderedStar;
    }

    public IEnumerable<Strip> SubgraphStar(FibredGraph subgraph) =>
        from edge in OrientedEdges
        where subgraph.ContainsVertex(edge.Source)
        where !subgraph.ContainsEdge(edge.UnderlyingEdge)
        select edge;

    public IEnumerable<Strip> SubgraphStarOrdered(FibredGraph subgraph)
    {
        if (subgraph?.VertexCount == 0) yield break;
        var vertex = subgraph!.Vertices.First();
        var star = StarOrdered(vertex).GetEnumerator();
        Strip firstEdge = null;
        var i = 0; // just to stop infinite loop

        while (star.MoveNext()) // this should never break
        {
            var edge = star.Current;
            if (Equals(firstEdge, edge)) break;
            firstEdge ??= edge;
            if (subgraph.ContainsEdge(edge!.UnderlyingEdge))
            {
                vertex = edge.Target;
                star.Dispose();
                star = StarOrdered(vertex, edge.Reversed()).CyclicShift(1).GetEnumerator();
                continue;
            }

            yield return edge;
            if (i++ > graph.EdgeCount)
                throw new("The star of the subgraph didn't loop correctly. Stopped infinite loop");
        }

        star.Dispose();
    }

    /// <summary>
    /// Returns the list (e, σ(e), σ²(e),..., σ^k(e)) where σ is the cyclic order of the star (i.e. the cyclic successor map of
    /// the given list), k is the number of elements in connectedSet and e is an element of connectedSet that is not the successor
    /// of another element of connectedSet (unless k = #star).
    /// So, if connectedSet is not actually connected, then the result will not set-equal to connectedSet.
    /// </summary>
    static IEnumerable<Strip> SortConnectedSetInStar(List<Strip> sortedStar, ICollection<Strip> connectedSet)
    {
        int starIndex = sortedStar.FindIndex(e => connectedSet.Contains(e));
        // this should be the index in the cyclic order where the connected set starts
        if (starIndex == -1) throw new("The connected set is not in the star.");
        if (starIndex == 0) starIndex = sortedStar.FindLastIndex(e => !connectedSet.Contains(e)) + 1;
        // if the connected set is all of star, then this is -1 + 1 = 0.
        return sortedStar.CyclicShift(starIndex).Take(connectedSet.Count);
    }

    /// <summary>
    /// The smallest invariant subgraph containing this edge.
    /// </summary>
    private static FibredGraph OrbitOfEdgeGraph(Strip edge)
    {
        List<UnorientedStrip> orbit = OrbitOfEdge(edge);

        FibredGraph newGraph = new FibredGraph();
        newGraph.AddVerticesAndEdgeRange(orbit);
        return newGraph;
    }

    private static List<UnorientedStrip> OrbitOfEdge(Strip edge)
    {
        List<UnorientedStrip> orbit = new() { edge.UnderlyingEdge };
        for (int i = 0; i < orbit.Count; i++)
        {
            edge = orbit[i];
            foreach (var e in edge.EdgePath)
            {
                if (orbit.Contains(e.UnderlyingEdge)) continue;
                orbit.Add(e.UnderlyingEdge);
            }
        }

        return orbit;
    }

    #endregion

    #region Invariant subforests

    /// <summary>
    /// only subforests whose collapse doesn't destroy the peripheral subgraph, i.e. whose components contain at most one vertex of the peripheral subgraph.
    /// components that are only vertices are missing!
    /// (but that doesn't matter because we only use this in the CollapseInvariantSubforest and MaximalPeripheralGraph methods)
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<FibredGraph> GetInvariantSubforests()
    {
        Dictionary<UnorientedStrip, FibredGraph> subforests = new();

        foreach (var strip in Strips)
        {
            if (subforests.Values.Any(subforest => subforest.Edges.Contains(strip))) continue;
            // there already is a larger subforest containing the one defined from this strip.

            FibredGraph orbitOfEdge = OrbitOfEdgeGraph(strip);

            if (!IsPeripheryFriendlySubforest(orbitOfEdge)) continue;

            foreach (var oldEdge in subforests.Keys.ToArray())
                if (orbitOfEdge.Edges.Contains(oldEdge))
                    subforests.Remove(oldEdge); // the new subforest contains the old one.
            subforests[strip] = orbitOfEdge;
        }

        // todo? See if a union of two subforests is a subforest. If so, we can merge them.
        return subforests.Values;
    }

    /// <summary>
    /// This checks if the graph is a subforest at each component touches the peripheral subgraph in at most one vertex.
    /// Thus if touching and remove are true, this checks if subgraph deformation retracts to the peripheral subgraph.
    /// </summary>
    /// <param name="subgraph"></param>
    /// <param name="peripheralSubgraph">If not present, the saved peripheral subgraph P is taken.</param>
    /// <param name="remove">If remove is true, subgraph is replaced by (subgraph \ peripheralSubgraph)</param>
    /// <param name="touching">If touching is true, each component must touch the peripheral subgraph in exactly one vertex.</param>
    /// <returns></returns>
    bool IsPeripheryFriendlySubforest(FibredGraph subgraph, FibredGraph peripheralSubgraph = null, bool remove = false,
        bool touching = false)
    {
        if (remove)
            return IsPeripheryFriendlySubforest(subgraph.Edges, peripheralSubgraph, false, touching);

        if (!subgraph.IsUndirectedAcyclicGraph()) return false;

        var components = new Dictionary<Junction, int>();
        int numberOfComponents = subgraph.ConnectedComponents(components);
        int[] componentIntersections = new int[numberOfComponents];

        peripheralSubgraph ??= this.peripheralSubgraph;
        foreach (var vertex in peripheralSubgraph.Vertices)
        {
            if (components.TryGetValue(vertex, out var comp))
                componentIntersections[comp]++;
            if (componentIntersections[comp] > 1) return false;
            // one component of the subforest contains more than one vertex of the peripheral subgraph. Collapsing it would destroy the peripheral subgraph.
            // this is what we call "not periphery-friendly"
        }

        return !touching || componentIntersections.All(i => i == 1);
    }

    bool IsPeripheryFriendlySubforest(IEnumerable<UnorientedStrip> edges, FibredGraph peripheralSubgraph = null,
        bool remove = false, bool touching = false)
    {
        peripheralSubgraph ??= this.peripheralSubgraph;

        if (remove)
            return IsPeripheryFriendlySubforest(edges.Except(peripheralSubgraph.Edges), peripheralSubgraph, false,
                touching);

        FibredGraph subgraph = new FibredGraph();
        subgraph.AddVerticesAndEdgeRange(edges);
        return IsPeripheryFriendlySubforest(subgraph, peripheralSubgraph, remove, touching);
    }

    public void CollapseInvariantSubforest(FibredGraph subforest)
    {
        var subforestEdges = Enumerable.ToHashSet(subforest.Edges);

        var componentList = subforest.ComponentGraphs(out var componentDict);

        var newVertices = new List<Junction>();
        foreach (var component in componentList)
        {
            var newVertex = new Junction(
                graph,
                component.Edges.Select(e => e.Curve).Concat<IDrawnsformable>(component.Vertices),
                // yes this is component.Patches but component is a FibredGraph, not FibredSurface... 
                name: NextVertexName()
            );
            newVertices.Add(newVertex);
            var orderIndex = 0;
            foreach (var strip in SubgraphStarOrdered(component).ToList())
            {
                strip.Source = newVertex;
                strip.OrderIndexStart = orderIndex++;
                strip.EdgePath = strip.EdgePath.Where(edge => !subforestEdges.Contains(edge.UnderlyingEdge)).ToList();
            }

            foreach (var junction in component.Vertices)
            {
                graph.RemoveVertex(junction);
            }
        }

        for (var index = 0; index < newVertices.Count; index++)
        {
            var newVertex = newVertices[index];
            var absorbedJunction = componentList[index].Vertices.First();
            newVertex.image = newVertices[componentDict[absorbedJunction.image]];
        }
    }

    #endregion

    #region Valence one and two junctions

    public IEnumerable<Junction> GetValenceOneJunctions() =>
        from vertex in graph.Vertices where Star(vertex).Count() == 1 select vertex;

    public void RemoveValenceOneJunction(Junction junction)
    {
        var star = Star(junction).ToArray();
        if (star.Length != 1) Debug.LogError($"Supposed valence-one junction has valence {star.Length}");
        var removedStrip = star.First();
        Junction otherJunction = removedStrip.Target;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removedStrip.UnderlyingEdge);
        foreach (var strip in Strips)
            strip.EdgePath = strip.EdgePath.Where(edge => !Equals(edge.UnderlyingEdge, removedStrip.UnderlyingEdge))
                .ToList();
        foreach (var vertex in graph.Vertices)
            if (vertex.image == junction)
                vertex.image = otherJunction;
        // todo: isotopy or make the junction bigger to include the removed strip
    }

    public IEnumerable<Junction> GetValenceTwoJunctions() =>
        from vertex in graph.Vertices where Star(vertex).Count() == 2 select vertex;

    /// <summary>
    /// Removes a valence two junction by keeping one of the two strips and make it replace the concatenation of the two strips.
    /// The strip to be removed is chosen as the one in the prePeriphery, or the one with the larger width in the Frobenius-Perron eigenvector.
    /// </summary>
    /// <param name="junction"></param>
    /// <param name="removeStrip"></param>
    public void RemoveValenceTwoJunction(Junction junction, Strip removeStrip = null)
    {
        var star = Star(junction).ToArray();
        if (star.Length != 2) Debug.LogError($"Supposed valence-two junction has valence {star.Length}");

        if (removeStrip == null)
        {
            var prePeriphery = PrePeriphery();
            if (prePeriphery.Contains(star[0].UnderlyingEdge)) removeStrip = star[0];
            else if (prePeriphery.Contains(star[1].UnderlyingEdge)) removeStrip = star[1];
            else
            {
                FrobeniusPerron(out var λ, out var widths, out var lengths);
                removeStrip = widths[star[0].UnderlyingEdge] > widths[star[1].UnderlyingEdge] ? star[0] : star[1];
            }
        }

        Strip keptStrip = Equals(star[0].UnderlyingEdge, removeStrip.UnderlyingEdge) ? star[1] : star[0];
        Junction enlargedJunction = removeStrip.Source == junction ? removeStrip.Target : removeStrip.Source;
        // two options: a) isotope along removeStrip to move junctions that map there to the "enlargedJunction" (but don't actually enlarge it)
        // and enlarge keptStrip to include the removed strip: keptStrip' = removedStrip.Reverse u removeJunction u keptStrip.
        // for better visuals (fewer curves in junctions), we start with this option
        // or: b) include removedStrip and junction into the enlargedJunction and isotope along f(removeStrip), so that it lands in f(enlJun).
        // This isotopy could be faked pretty well! Just display f(keptStrip) as f(removedStrip).Reverse u f(removeJunction) u f(keptStrip)
        // and f(enlJun) as what it was before enlarging.
        // In both cases, the same thing happens combinatorially
        var name = keptStrip.Name.ToLower(); // todo: what if it was a reverse edge?
        var newStrip = keptStrip.CopyUnoriented(name: name,
            source: enlargedJunction,
            orderIndexStart: removeStrip.OrderIndexEnd,
            curve: keptStrip.Curve = removeStrip.Curve.Reversed().Concatenate(keptStrip.Curve),
            edgePath: OrderedStrip.ReversedEdgePath(removeStrip.EdgePath).Concat(keptStrip.EdgePath).ToList()
        );

        graph.RemoveVertex(junction); // removes the old edges as well
        graph.AddVerticesAndEdge(newStrip);

        foreach (var strip in Strips)
            strip.EdgePath = (from e in strip.EdgePath
                    where !Equals(e.UnderlyingEdge, removeStrip.UnderlyingEdge)
                    select e.Equals(keptStrip) ? newStrip :
                        e.Equals(keptStrip.Reversed()) ? newStrip.Reversed() : e
                ).ToList();
        foreach (var vertex in graph.Vertices)
            if (vertex.image == junction)
                vertex.image = enlargedJunction;
    }

    #endregion

    #region Pulling tight

    /// <summary>
    /// These are the positions where the graph map is not tight, so we can pull tight here.
    /// We have to assume that there are no invariant subforests because the isotopy would touch them!
    /// </summary>
    public IEnumerable<(Strip, EdgePoint[], Junction[])> GetLoosePositions() =>
        from strip in OrientedEdges
        let backTracks = GetBackTracks(strip).ToArray()
        let extremalVertices = GetExtremalVertices(strip).ToArray()
        where backTracks.Length != 0 || extremalVertices.Length != 0
        select (strip, backTracks, extremalVertices);

    IEnumerable<Junction> GetExtremalVertices(Strip edge = null)
    {
        if (edge != null)
            return from vertex in graph.Vertices
                let star = Star(vertex)
                // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
                where star.Any() && star.All(strip => Equals(strip.Dg, edge))
                select vertex;
        return from vertex in graph.Vertices
            let star = Star(vertex)
            let firstOutgoingEdge = star.FirstOrDefault()
            // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
            where firstOutgoingEdge != null && firstOutgoingEdge.Dg != null &&
                  star.All(strip => Equals(strip.Dg, firstOutgoingEdge.Dg))
            select vertex;
    }

    IEnumerable<EdgePoint> GetBackTracks(Strip edge = null)
    {
        if (edge != null)
            return from edgePoint in GetBackTracks()
                where Equals(edgePoint.DgAfter(), edge)
                select edgePoint;

        return from strip in Strips
            where strip.EdgePath.Count > 1
            from i in Enumerable.Range(1, strip.EdgePath.Count - 1)
            // only internal points: Valence-2 extremal vertices are found in parallel anyways.
            let edgePoint = new EdgePoint(strip, i)
            where Equals(edgePoint.DgBefore(), edgePoint.DgAfter())
            select edgePoint;
    }

    public void PullTightExtremalVertex(Junction vertex)
    {
        vertex.image = Star(vertex).First()[0].Image;
        foreach (var strip in Star(vertex))
        {
            strip.EdgePath = strip.EdgePath.Skip(1).ToList();
            // for self-loops, this takes one from both ends.
        }
        // isotopy: Move vertex and shorten the strips (only the homeomorphism is changed, not the graph)
        // todo? update EdgePoints?
    }

    public void PullTightBackTrack(EdgePoint backTrack, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        if (!Equals(backTrack.DgBefore(), backTrack.DgAfter()))
        {
            Debug.LogWarning($"Assumed Backtrack at {backTrack} is not a backtrack (anymore)!");
            return;
        }

        var strip = backTrack.edge;
        var i = backTrack.index;
        if (i == 0)
        {
            PullTightExtremalVertex(strip.Source);
            return;
        }

        strip.EdgePath = strip.EdgePath.Take(i - 1).Concat(strip.EdgePath.Skip(i + 1)).ToList();

        for (int k = 0; k < updateEdgePoints.Count; k++)
        {
            var j = updateEdgePoints[k].AlignedIndex(strip, out var reverse);
            if (j < i) continue;
            var res = j == i ? new EdgePoint(strip, j - 1) : new EdgePoint(strip, j - 2);
            updateEdgePoints[k] = reverse ? res.Reversed() : res;
        }

        // todo? isotopy to make the strip shorter
    }

    #endregion

    #region Folding initial segments

    public void FoldEdges(IList<Strip> edges, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();

        if (edges.Count < 2)
        {
            Debug.Log($"Wanted to fold {edges.Count} edges. Nothing to do.");
            return;
        }

        var edgePath = edges[0].EdgePath;
        if (edges.Any(edge => !edge.EdgePath.SequenceEqual(edgePath)))
            throw new("Edges to fold do not have the same edge path.");

        var star = StarOrdered(edges[0].Source).ToList();

        var edgesOrdered = SortConnectedSetInStar(star, edges).ToList();
        if (!edges.ToHashSet().SetEquals(edgesOrdered))
            throw new($"Edges to fold are not connected in the cyclic order: {string.Join(", ", edges)}");
        edges = edgesOrdered;

        var edgeSelection = (
            from i in Enumerable.Range(0, edges.Count)
            select i % 2 == 0 ? edges[edges.Count / 2 + i / 2] : edges[edges.Count / 2 - i / 2 - 1]
        ).ToList();
        var preferredOldEdge =
            edgeSelection.FirstOrDefault(e => e is UnorientedStrip && !e.Name.EndsWith('1') && !e.Name.EndsWith('2')) ??
            edgeSelection.FirstOrDefault(e => !e.Name.EndsWith('1') && !e.Name.EndsWith('2')) ??
            edgeSelection.FirstOrDefault(e => e is UnorientedStrip && !e.Name.EndsWith('2')) ??
            edgeSelection.FirstOrDefault(e => !e.Name.EndsWith('2')) ??
            edgeSelection.FirstOrDefault(e => e is UnorientedStrip) ??
            edgeSelection.First();
        
        string name = null;
        if (!char.IsLetter(preferredOldEdge.Name[^1]))
            name = NextEdgeName();
        else name = preferredOldEdge.Name.ToLower();

        var newVertexName = NextVertexName();

        var targetVerticesToFold =
            (from edge in edges select edge.Target).WithoutDuplicates().ToArray(); // has the correct order
        var waypoints = from edge in edges select edge.Curve.EndPosition;
        var connectingCurve = surface.GetPathFromWaypoints(waypoints, newVertexName);
        // todo? Try to avoid the rest of the fibred surface
        var color = targetVerticesToFold.First().Color;
        var newVertex = new Junction(graph, targetVerticesToFold.Append<IDrawnsformable>(connectingCurve),
            newVertexName, targetVerticesToFold.First().image, color);
        graph.AddVertex(newVertex);
        /* float indexOffset = 1;
        foreach (var vertex in targetVerticesToFold)
        {
          var star_ = Star(vertex).ToArray();
          foreach (var edge in star_)
          {
              edge.Source = newVertex;
              edge.OrderIndexStart += indexOffset;
          }

          graph.RemoveVertex(vertex);
          indexOffset = star_.Max(e => e.OrderIndexStart) + 1; // for this to work we assume that all order indices are > -1
        } */
        int currentSortIndex = 1; // the new edge has sortIndex 0
        foreach (var vertex in targetVerticesToFold)
        {
            var localStar = StarOrdered(vertex).ToList();
            var outerEdges = localStar.ToHashSet();
            outerEdges.ExceptWith(from e in edges select e.Reversed());
            var outerEdgesSorted = SortConnectedSetInStar(localStar, outerEdges);
            foreach (var edge in outerEdgesSorted)
            {
                edge.OrderIndexStart = currentSortIndex++;
                edge.Source = newVertex;
            }         
            graph.RemoveVertex(vertex);
        }
        // graph.RemoveEdges(from edge in edges select edge.UnderlyingEdge);
        
        var newEdge = preferredOldEdge.Copy(name: name, target: newVertex, orderIndexEnd: 0); 
        // orderIndexStart might have been set in the loop above (if one of the targetVerticesToFold was the source of the edge)
        graph.AddEdge(newEdge.UnderlyingEdge);

        foreach (var junction in graph.Vertices)
        {
            if (targetVerticesToFold.Contains(junction.image))
                junction.image = newVertex;
        }

        foreach (var edge in edges)
        {
            for (var k = 0; k < updateEdgePoints.Count; k++)
            {
                int j = updateEdgePoints[k].AlignedIndex(edge, out bool reverse);
                if (j < 0) continue;
                var res = new EdgePoint(newEdge, j);
                updateEdgePoints[k] = reverse ? res.Reversed() : res;
            }
        }

        foreach (var strip in Strips)
        {
            strip.EdgePath = strip.EdgePath.Select(
                edge => edges.Contains(edge) ? newEdge :
                    edges.Contains(edge.Reversed()) ? newEdge.Reversed() : edge
            ).ToList();
        }
    }

    public (Strip, Strip) SplitEdge(EdgePoint splitPoint, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        var splitEdge = splitPoint.edge;
        var edgePath = splitEdge.EdgePath;
        var splitTime = splitPoint.GetCurveTimeInJunction();

        var newVertex = new Junction(graph, splitEdge.Curve[splitTime], NextVertexName(), splitPoint.Image);

        var firstSegment = splitEdge.Copy(
            curve: splitEdge.Curve.Restrict(0, splitTime),
            edgePath: edgePath.Take(splitPoint.index).ToList(),
            target: newVertex,
            orderIndexEnd: 0);

        var secondSegment = splitEdge.Copy(
            curve: splitEdge.Curve.Restrict(splitTime, splitEdge.Curve.Length),
            edgePath: edgePath.Skip(splitPoint.index).ToList(),
            source: newVertex,
            orderIndexStart: 1
        );

        string firstSegmentName, secondSegmentName;
        string originalName = splitEdge.Name.ToLower();
        switch (originalName[^1])
        {
            case '2':
                firstSegmentName = originalName;
                secondSegmentName = originalName[..^1] + '3';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                    secondSegmentName = originalName[..^1] + '4';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                    secondSegmentName = NextEdgeName();
                break;
            case '1':
                firstSegmentName = originalName;
                secondSegmentName = originalName + '+';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                    secondSegmentName = NextEdgeName();
                break;
            default:
                firstSegmentName = originalName + '1';
                if (Strips.Any(edge => edge.Name == firstSegmentName))
                {
                    firstSegmentName = NextEdgeName();
                    secondSegmentName = firstSegmentName + '2';
                    firstSegmentName += '1';
                    break;
                }

                secondSegmentName = originalName + '2';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                {
                    secondSegmentName = originalName + '3';
                    if (Strips.Any(edge => edge.Name == secondSegmentName))
                        secondSegmentName = originalName + "2+";
                }

                break;
        }

        if (splitEdge is OrderedStrip { reverse: true })
        {
            // in this case, the firstSegment and secondSegment are also reversed OrderedStrips
            firstSegment.Name = secondSegmentName.ToUpper();
            secondSegment.Name = firstSegmentName.ToUpper();
        }
        else
        {
            firstSegment.Name = firstSegmentName;
            secondSegment.Name = secondSegmentName;
        }


        for (var k = 0; k < updateEdgePoints.Count; k++)
        {
            var splitPointAligned = splitPoint.AlignedWith(updateEdgePoints[k].edge, out var reverse);
            if (splitPointAligned is null) continue;

            var j = updateEdgePoints[k].index;
            var i = splitPointAligned.index;
            // if the edge point was on the split edge, it is now on the first or second segment
            // if it was on the reversed edge, it is now on the reversed first or second segment
            if (j < i) updateEdgePoints[k] = new EdgePoint(reverse ? secondSegment.Reversed() : firstSegment, j);
            else updateEdgePoints[k] = new EdgePoint(reverse ? firstSegment.Reversed() : secondSegment, j - i);
        }

        for (var k = 0; k < updateEdgePoints.Count; k++)
        {
            // move the edge point index to the right in preparation for the replacement in the next step
            var edgePoint = updateEdgePoints[k];
            updateEdgePoints[k] = new EdgePoint(edgePoint.edge,
                edgePoint.index + edgePoint.edge.EdgePath.Take(edgePoint.index)
                    .Count(edge => edge.UnderlyingEdge.Equals(splitEdge.UnderlyingEdge)));
        }

        // these three things now already happen in the Strip.set_Target and Strip.set_Source accessors
        graph.AddVertex(newVertex);
        graph.AddEdge(firstSegment.UnderlyingEdge);
        graph.AddEdge(secondSegment.UnderlyingEdge);
        graph.RemoveEdge(splitEdge.UnderlyingEdge);

        foreach (var strip in Strips)
        {
            // in each edgePath, replace the splitEdge by the two new edges
            strip.EdgePath = strip.EdgePath.SelectMany(
                edge => edge.Equals(splitEdge)
                    ? new() { firstSegment, secondSegment }
                    : edge.Equals(splitEdge.Reversed())
                        ? new() { secondSegment.Reversed(), firstSegment.Reversed() }
                        : new List<Strip> { edge }
            ).ToList();
        }


        return (firstSegment, secondSegment);
    }

    public void FoldInitialSegment(IList<Strip> strips, int? i = null, IList<EdgePoint> updateEdgePoints = null)
    {
        i ??= Strip.SharedInitialSegment(strips);
        if (i == 0)
        {
            Debug.LogError("The edges do not have a common initial segment.");
            return;
        }

        updateEdgePoints ??= new List<EdgePoint>();
        var l = updateEdgePoints.Count;
        foreach (var strip in strips)
            updateEdgePoints.Add(strip[i.Value]);
        // we have to update the edge points after each split, as else we might split an edge and its reverse (which doesn't exist anymore after we split the original edge), thus creating 4 instead of 3 new edges! (or instead of 2)


        var initialStripSegments = new List<Strip>(strips.Count);
        var terminalStripSegments = new Dictionary<string, Strip>(strips.Count);
        for (var index = 0; index < strips.Count; index++)
        {
            var splitEdgePoint = updateEdgePoints[l + index];
            var edge = splitEdgePoint.edge; // instead of strips[index]
            var splitIndex = splitEdgePoint.index;
            if (splitIndex == edge.EdgePath.Count) // it is never 0
            {
                initialStripSegments.Add(edge);
                continue;
            }

            var (firstSegment, secondSegment) = SplitEdge(splitEdgePoint, updateEdgePoints);
            initialStripSegments.Add(firstSegment);
            terminalStripSegments[edge.Name.ToLower()] = secondSegment;
        }

        // give the remaining segments the original names. 
        // We do this here so that if we split an edge and its reverse the middle part gets the same name as the original edge.
        foreach (var (name, secondSegment) in terminalStripSegments)
            secondSegment.UnderlyingEdge.Name = name;


        FoldEdges(initialStripSegments, updateEdgePoints);
    }

    #endregion

    #region (Essential) Inefficiencies

    public IEnumerable<Inefficiency> GetInefficiencies()
    {
        var gates = Gate.FindGates(graph);
        foreach (var edge in Strips)
        {
            for (int i = 0; i <= edge.EdgePath.Count; i++)
            {
                var inefficiency = CheckEfficiency(edge[i], gates);
                if (inefficiency != null)
                {
                    if (i == 0 || i == edge.EdgePath.Count)
                        Debug.Log("Interpreted a valence-two gate-wise extremal vertex as an inefficiency.");
                    yield return inefficiency;
                }
            }
        }
    }

    [CanBeNull]
    public Inefficiency CheckEfficiency(EdgePoint edgePoint, List<Gate<Junction>> gates)
    {
        var a = edgePoint.DgBefore();
        var b = edgePoint.DgAfter();
        if (a == null || b == null) return null; // the edgePoints is actually a vertex of valence other than two

        if (!Equals(a.Source, b.Source))
            Debug.LogError($"The edge path is not an actual edge path at {edgePoint}!");
        if (!FindGate(a).Edges.Contains(b)) return null; // not inefficient

        return new Inefficiency(edgePoint);

        Gate<Junction> FindGate(Strip edge) => gates.First(gate => gate.Edges.Contains(edge));
        // The gates should form a partition of the set of oriented edges, so this should be well-defined.
    }

    /// <summary>
    /// At this point, we assume that the fibred surface is irreducible, i.e. that all inefficiencies are in H and that there are no
    /// valence-one junctions or edges that get mapped to junctions.
    /// </summary>
    /// <param name="p"></param>
    /// <exception cref="Exception"></exception>
    public void RemoveInefficiency(Inefficiency p)
    {
        if (p.order == 0)
        {
            // The inefficiency is a back track
            // todo: or a valence two extremal vertex
            PullTightBackTrack(p);
            return;
        }

        var initialSegment = p.initialSegmentToFold;
        var edgesToFold = p.edgesToFold;

        while (edgesToFold.Any(edge =>
                   Equals(p, new EdgePoint(edge, initialSegment))
               )) initialSegment--;

        var updateEdgePoints = new List<EdgePoint>() { p };

        if (initialSegment == 0)
        {
            // the special case when one of the split points is at the point p
            // Split the edge c = Dg^{p.order}(p.edge) = Dg(p.edgesToFold[*]) at the first edge point.
            // If there is none, split g(c) = Dg(c) at the first edge point. If there is none ...
            var c = edgesToFold[0].Dg;
            var edgesToSplit = new List<Strip> { c };
            while (edgesToSplit[0].EdgePath.Count == 1 && edgesToSplit.Count <= 2 * graph.EdgeCount)
                edgesToSplit.Insert(0, edgesToSplit[0].Dg);
            if (edgesToSplit[0].EdgePath.Count == 1)
                throw new Exception(
                    "Weird: This edge only gets mapped to single edges under g. This shouldn't happen at this stage unless f is efficient and permutes the edges, g is a homeo, the growth is λ=1 and f is periodic. But it isn't efficient because we are in this method.");

            updateEdgePoints.AddRange(edgesToFold.Select(edge => new EdgePoint(edge, 0)));

            foreach (var edge in
                     edgesToSplit) // we split the last edge first, so that we can split the one before and so on.
                SplitEdge(edge[1], updateEdgePoints);

            edgesToFold = updateEdgePoints.Skip(1).Select(edgePoint => edgePoint.edge).ToList();
            initialSegment = 1;
            // this relies on the EdgePath of these edges having updated during the split of the edge c. 
            // They started with edge.EdgePath = c,..., now they have edge.EdgePath = c1,c2,...
        }

        FoldInitialSegment(edgesToFold, initialSegment, updateEdgePoints);

        // Now p is an inefficiency of degree one lower.

        var pNew = updateEdgePoints[0];
        var newInefficiency = CheckEfficiency(pNew, Gate.FindGates(graph));

        if (newInefficiency == null || newInefficiency.order != p.order - 1)
            Debug.LogError(
                $"Bug: The inefficiency was not turned into an efficiency of order one less: {p} was turned into {newInefficiency ?? pNew}");
        if (newInefficiency != null && newInefficiency.order < p.order)
            RemoveInefficiency(newInefficiency);
    }

    #endregion

    #region Absorbing into the periphery

    FibredGraph GetMaximalPeripheralSubgraph()
    {
        var Q = new FibredGraph(true);
        Q.AddVerticesAndEdgeRange(peripheralSubgraph.Edges);
        foreach (var strip in Strips)
        {
            if (Q.Edges.Contains(strip)) continue;
            var orbit = OrbitOfEdge(strip);
            if (IsPeripheryFriendlySubforest(orbit, Q, true, true))
                // this means that after adding the orbit to Q, it still deformation retracts onto Q (thus P).
                Q.AddVerticesAndEdgeRange(orbit);
        }

        return Q;
    }


    public bool AbsorbIntoPeriphery(bool testDry = false)
    {
        var Q = GetMaximalPeripheralSubgraph();
        if (testDry && (
                Q.Edges.Any(edge => !peripheralSubgraph.ContainsEdge(edge))
                || Q.Vertices.Any(v => Star(v).Count() <= 2)
            )) return true;

        var starQ = SubgraphStar(Q).ToList();
        foreach (var strip in starQ)
        {
            if (testDry && Q.ContainsEdge(strip.Dg!.UnderlyingEdge)) return true;
            strip.EdgePath = strip.EdgePath.SkipWhile(e => Q.Edges.Contains(e)).ToList();
            if (strip.Dg == null) Debug.LogError($"The strip {strip} has been absorbed into the periphery.");
        }

        if (testDry) return false;

        var components = Q.ComponentGraphs(out var componentDict);
        var gates = Gate.FindGates(starQ, vertex => componentDict[vertex]);
        // todo? Display the peripheral gates?
        var newJunctions = new List<Junction>();
        var stars = (from component in components select SubgraphStarOrdered(component).ToList()).ToList();
        foreach (var gate in gates)
        {
            var gateInOrder = SortConnectedSetInStar(stars[gate.junctionIdentifier], gate.Edges).ToList();
            var positions = from edge in gateInOrder select edge.Curve.StartPosition;
            // give each gate γ the fr(γ) from joining the fr(e) for e in the gate along the boundary of <Q>.
            // todo: currently this does not follow the boundary of <Q>.
            string name = NextVertexName();
            var newJunction = new Junction(graph, surface.GetPathFromWaypoints(positions, name), name);
            newJunctions.Add(newJunction);
            var orderIndex = 0;
            foreach (var edge in gateInOrder)
            {
                edge.Source = newJunction; // adds the new junctions to the graph
                edge.OrderIndexStart = orderIndex++;
            }
        }

        // graph.AddVertexRange(newJunctions);
        List<UnorientedStrip> newEdges = new();
        foreach (var star in stars)
        {
            List<Junction> newJunctionsInOrder = new();
            foreach (var edge in star)
            {
                var newJunction = edge.Source; // this is the junction corresponding to the gate of the edge
                var lastJunction = newJunctionsInOrder.LastOrDefault();
                if (newJunction == lastJunction) continue;


                var Dg = edge.Dg;
                var Dg_gate = Enumerable.Range(0, gates.Count).First(j => gates[j].Edges.Contains(Dg));
                newJunction.image = newJunctions[Dg_gate];

                newJunctionsInOrder.Add(newJunction);
            }

            if (newJunctionsInOrder[^1] !=
                newJunctionsInOrder[0]) // this happens if the iteration above started in the middle of a gate.
                newJunctionsInOrder.Add(newJunctionsInOrder[0]);

            var name = NextEdgeName();
            for (var index = 0; index < newJunctionsInOrder.Count - 1; index++)
            {
                var junction = newJunctionsInOrder[index];
                var nextJunction = newJunctionsInOrder[index + 1];
                var junctionEndPosition = (junction.Patches.First() as Curve).EndPosition;
                var nextJunctionStartPosition = (nextJunction.Patches.First() as Curve).StartPosition;

                var newEdge = new UnorientedStrip(
                    curve: surface.GetGeodesic(
                        start: junctionEndPosition,
                        end: nextJunctionStartPosition,
                        name: name + index),
                    source: junction,
                    target: nextJunction,
                    edgePath: new Strip[] { }, // we can only add these once we created them all
                    graph: graph,
                    orderIndexStart: Star(junction).Max(e => e.OrderIndexStart) + 0.5f,
                    orderIndexEnd: Star(nextJunction).Min(e => e.OrderIndexStart) - 0.5f // still > -1!
                );
                newEdges.Add(newEdge);
                graph.AddVerticesAndEdge(newEdge);
            }
        }

        foreach (var strip in newEdges)
        {
            var imageOfSource = strip.Source.image;
            var imageOfEdge = newEdges.First(e => e.Source == imageOfSource);
            if (imageOfEdge.Target != strip.Target.image)
                Debug.LogError(
                    "The new graph map does not seem to act as a graph automorphism on the new peripheral graph.");
            strip.EdgePath = new[] { imageOfEdge };
        }

        // graph.RemoveEdges(Q.Edges);
        foreach (var oldJunction in Q.Vertices)
            graph.RemoveVertex(
                oldJunction); // also removes Q's edges (the star edges should not be adjacent to (the old) Q anymore)
        return true;
    }

    #endregion

    #region Removing peripheral inefficiencies

    public IEnumerable<List<Strip>> GetPeripheralInefficiencies()
    {
        HashSet<UnorientedStrip> prePeriphery = PrePeriphery();
        foreach (var edge in OrientedEdges)
        {
            if (!prePeriphery.Contains(edge.Dg))
                continue;
            var otherEdges = Star(edge.Source).Where(
                e => Equals(e.Dg, edge.Dg)).ToList();
            if (otherEdges.Count > 1)
                yield return otherEdges;
        }
    }

    public void RemovePeripheralInefficiency(List<Strip> foldableEdges)
    {
        FoldInitialSegment(foldableEdges);
    }

    #endregion

    #region Subgraphs related to the periphery

    private List<HashSet<UnorientedStrip>> PrePeripheralDegrees()
    {
        var degrees = new List<HashSet<UnorientedStrip>>();
        degrees.Add(peripheralSubgraph.Edges.ToHashSet());
        var remainingEdges = Strips.ToHashSet();
        remainingEdges.ExceptWith(degrees[0]);
        for (int i = 1; i <= 2 * graph.EdgeCount; i++) // should never reach this limit
        {
            var P_i = (from strip in remainingEdges
                where strip.EdgePath.All(edge => degrees[i - 1].Contains(edge.UnderlyingEdge))
                select strip).ToHashSet();
            degrees.Add(P_i);
            remainingEdges.ExceptWith(P_i);
            if (P_i.Count == 0) break;
        }

        return degrees;
    }

    /// <summary>
    /// This is the subgraph P u pre-P = P0 u P1 u ... that consists of all edges that eventually get mapped into the periphery P.
    /// </summary>
    /// <returns></returns>
    public HashSet<UnorientedStrip> PrePeriphery()
    {
        var prePeripheralDegrees = PrePeripheralDegrees();
        var prePeriphery = new HashSet<UnorientedStrip>();
        foreach (var degree in prePeripheralDegrees)
            prePeriphery.UnionWith(degree);
        return prePeriphery;
    }

    /// <summary>
    /// This is the subgraph H that contains all the edges that are not in the pre-periphery.
    /// These are the edges that will not be "infinitesimal" as determined by the eigenvector defining lengths.
    /// </summary>
    /// <returns></returns>
    public HashSet<UnorientedStrip> EssentialSubgraph()
    {
        var edges = Strips.ToHashSet();
        edges.ExceptWith(PrePeriphery());
        return edges;
    }

    #endregion

    #region Names

    readonly List<string> edgeNames = new()
    {
        "a", "b", "c", "d", /*"e",*/ "x", "y", "z", "w", "u", "h", "i", "j", "k", "l", "m", "n", "o",
        "α", "β", "γ", "δ", "ε", "ζ", "θ", "κ", "λ", "μ", "ξ", "π", "ρ", "σ", "τ", "φ", "ψ", "ω",
    };

    readonly List<string> vertexNames = new()
    {
        "v", "w", "p", "q", "r", "s", "t",
    };

    void DeferNames()
    {
        foreach (var strip in Strips)
        {
            if (edgeNames.Remove(strip.Name))
                edgeNames.Add(strip.Name);
        }
    }

    string NextEdgeName()
    {
        return edgeNames.Except(Strips.Select(edge => edge.Name)).FirstOrDefault();
    }

    string NextVertexName()
    {
        return vertexNames.Except(graph.Vertices.Select(vertex => vertex.Name)).Concat(
            from i in Enumerable.Range(1, 1000) select $"v{i}"
        ).FirstOrDefault();
    }

    #endregion

    #region Transition matrix and weigths

    /// <summary>
    /// The transition matrix is a combinatorial representation of the graph map g: E -> E* forgetting order and direction.
    /// The column corresponding to an edge e is how often g(e) crosses each unoriented edge e' in the graph.
    /// The surface homeomorphism described by this FibredSurface is reducible if the transition matrix of H is reducible, as this means that there is a non-trivial subgraph of G that does not only consist of pre-periphery. The boundary of the induced subgraph is preserved by an isotoped version of the homeomorphism and consists at least in part of essential edges, thus determining a reduction in the sense of the Thurston-Nielsen classification.
    /// </summary>
    /// <returns></returns>
    public Dictionary<(UnorientedStrip, UnorientedStrip), int> TransitionMatrix(
        IEnumerable<UnorientedStrip> strips = null)
    {
        var stripArray = (strips ?? Strips).ToArray();
        var n = graph.EdgeCount;
        var matrix = new Dictionary<(UnorientedStrip, UnorientedStrip), int>();
        foreach (var edge in stripArray)
        foreach (var edgeToCross in stripArray)
            matrix[(edgeToCross, edge)] = edge.EdgePath.Count(e => e.UnderlyingEdge == edgeToCross);
        return matrix;
    }

    public Dictionary<(UnorientedStrip, UnorientedStrip), int> TransitionMatrixEssential() =>
        TransitionMatrix(EssentialSubgraph());

    /// <summary>
    /// Tests whether the (essential) transition matrix M_H is reducible, hence if there is a non-trivial essential preserved subgraph.
    /// 
    /// When the transition matrix is irreducible, we can use Perron-Frobenius theory. The "growth" of the graph map is the spectral radius (Frobenius-Perron eigenvalue) and the associated positive eigenvector is interpreted as the widths of the edges (in the train track sense). 
    /// </summary>
    /// <returns> A non-trivial essential preserved subgraph or null if it doesn't exist, i.e. the transition matrix is reducible. </returns>
    public List<UnorientedStrip> PreservedSubgraph_Reducibility()
    {
        var prePeriphery = PrePeriphery();
        var essentialSubgraph = Strips.ToHashSet();
        essentialSubgraph.ExceptWith(prePeriphery);
        var edgesToCheck = essentialSubgraph.ToHashSet();

        while (true)
        {
            var edge = edgesToCheck.FirstOrDefault();
            if (edge == null) return null;

            var orbit = OrbitOfEdge(edge);
            edgesToCheck.ExceptWith(orbit);
            if (essentialSubgraph.Any(e => !orbit.Contains(e)))
                return orbit;
        }
    }

    /// <summary>
    /// Finds the left and right eigenvectors of the essential transition matrix M_H associated to the Frobenius-Perron eigenvalue (growth) λ.
    /// </summary>
    /// <returns></returns>
    public void FrobeniusPerron(out double λ, out Dictionary<UnorientedStrip, double> widths,
        out Dictionary<UnorientedStrip, double> lengths)
    {
        var matrix = TransitionMatrixEssential();
        var strips = matrix.Keys.Select(pair => pair.Item1).Distinct().ToArray();
        Matrix<double> M = Matrix<double>.Build.Dense(strips.Length, strips.Length,
            (i, j) => matrix[(strips[i], strips[j])]
        );
        var eigenvalueDecomposition = M.Evd();
        int
            columnIndex; // the index of the largest eigenvalue - should be the last index, as the eigenvalues are sorted by magnitude (?)
        (columnIndex, λ) = eigenvalueDecomposition.EigenValues.ArgMaxIndex(c => (float)c.Real);
        var eigenvector = eigenvalueDecomposition.EigenVectors.Column(columnIndex);
        if (eigenvector[0] < 0) eigenvector = eigenvector.Negate();
        var rightEigenvector = eigenvalueDecomposition.EigenVectors.Inverse().Row(columnIndex);
        if (rightEigenvector[0] < 0) rightEigenvector = rightEigenvector.Negate();

        widths = new Dictionary<UnorientedStrip, double>(from i in Enumerable.Range(0, strips.Length)
            select new KeyValuePair<UnorientedStrip, double>(strips[i], eigenvector[i]));
        lengths = new Dictionary<UnorientedStrip, double>(from i in Enumerable.Range(0, strips.Length)
            select new KeyValuePair<UnorientedStrip, double>(strips[i], rightEigenvector[i]));
    }

    // todo: Extend weights to the prePeriphery. Implement the train tracks.

    #endregion
}
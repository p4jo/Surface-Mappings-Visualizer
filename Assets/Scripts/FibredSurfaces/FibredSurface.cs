﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using MathNet.Numerics.LinearAlgebra;
using QuikGraph;
using QuikGraph.Algorithms;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;
using Object = UnityEngine.Object;

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
    
    public IEnumerable<UnorientedStrip> StripsOrdered => graph.Vertices.SelectMany(
        v => StarOrdered(v).Select(e => e.UnderlyingEdge)
    ).Distinct();
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
                          ?? edgeDescriptions.FirstOrDefault(tuple => tuple.Item3 == name).Item1?.EndPosition, 
                    name: name,
                    color: NextVertexColor()) // image is set below
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
        graph.AddVerticesAndEdgeRange(strips);
        SetMap(edgeDescriptions.ToDictionary(tuple => tuple.Item1.Name, tuple => tuple.Item4), GraphMapUpdateMode.Replace);
        // fix vertex targets
        foreach (var strip in OrientedEdges)
        {
            if (strip.Source.image != null && strip.Source.image != strip.Dg?.Source)
                Debug.LogError(
                    $"Two edges at the same vertex have images that don't start at the same vertex! g({strip.Name}) = {string.Join(' ', strip.EdgePath.Select(e => e.Name))} starts at o(g({strip.Name})) = {strip.Dg?.Source}, but we already set g(o({strip.Name})) = {strip.Source.image}.");
            strip.Source.image ??= strip.Dg?.Source;
        }


        peripheralSubgraph = new FibredGraph(true);
        var peripheralEdgesSet = peripheralEdges.Select(n => n.ToLower()).ToHashSet();
        peripheralSubgraph.AddVerticesAndEdgeRange(
            Strips.Where(e => peripheralEdgesSet.Contains(e.Name) )
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
            from oldJunction in graph.Vertices
            select new KeyValuePair<Junction, Junction>(oldJunction, 
                oldJunction.Copy(newGraph)
            )
        );

        var newEdges = new Dictionary<UnorientedStrip, UnorientedStrip>(
            from oldStrip in Strips
            select new KeyValuePair<UnorientedStrip, UnorientedStrip>(oldStrip, 
                oldStrip.CopyUnoriented(graph: newGraph,
                    source: newVertices[oldStrip.Source],
                    target: newVertices[oldStrip.Target]
                )
            )
        );

        foreach (var (oldJunction, newJunction) in newVertices) 
            newJunction.image = newVertices[oldJunction.image];

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

    public void SetMap(IDictionary<string, string[]> map, GraphMapUpdateMode mode)
    {
        if (mode == GraphMapUpdateMode.Postcompose)
        {
            var oldMap = Strips.ToDictionary(
                e => e.Name, 
                e => e.EdgePath.Select(d => d.Name).ToArray()
            );
            SetMap(map, GraphMapUpdateMode.Replace);
            SetMap(oldMap, GraphMapUpdateMode.Precompose);
            return;
        }
            
        if (!map.Keys.Select(k => k.ToLower()).ToHashSet().IsSupersetOf(Strips.Select(s => s.Name)))
            throw new ArgumentException("The map must be given for each edge of the surface.");
        
        var edges = new Dictionary<string, Strip>((
                from strip in Strips select new KeyValuePair<string, Strip>(strip.Name.ToLower(), strip)
            ).Concat(
                from strip in Strips select new KeyValuePair<string, Strip>(strip.Name.ToUpper(), strip.Reversed())
            )
        );
        foreach (var (name, edgePathText) in map)
        {
            if (!edges.TryGetValue(name, out var edge))
                throw new ArgumentException($"The edge {name} is not in the surface.");
            switch (mode)
            {
                case GraphMapUpdateMode.Precompose:
                    edge.EdgePath = edgePathText.SelectMany(edgeName => edges[edgeName].EdgePath).ToList();
                    break;
                case GraphMapUpdateMode.Replace:
                    edge.EdgePath = edgePathText.Select(edgeName => edges[edgeName]).ToList();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        };
    }

    #endregion

    #region Algorithm with suggestion system

    public class AlgorithmSuggestion
    {
        public IEnumerable<(object, string)> options;
        public string description;
        public IEnumerable<string> buttons;
        public bool allowMultipleSelection = false;
        
        public static AlgorithmSuggestion Finished = new AlgorithmSuggestion() { description = "Finished." };
    }

    public AlgorithmSuggestion NextSuggestion()
    {
        // todo? add a set of ITransformables to highlight with each option?
        // Iterate through the steps and give the possible steps to the user
        var a = GetInvariantSubforests();
        if (a.Any())
            return new AlgorithmSuggestion()
            {
                options = from subforest in a
                    select (subforest.Edges.Select(e => e.Name) as object,
                        string.Join(", ", subforest.Edges.Select(v => v.Name))
                    ),
                description = "Collapse an invariant subforest.",
                buttons = new[] { "Collapse" }
            };
        var b = GetLoosePositions();
        if (b.Any())
        {
            return new AlgorithmSuggestion()
            {
                options = from loosePosition in b
                    select (loosePosition.Item1.Name as object,
                        $"In edge {loosePosition.Item1.Name} there are the backtracks " +
                        loosePosition.Item2.Select(edgePoint => edgePoint.ToShortString(3,2)).ToCommaSeparatedString().AddDotsMiddle(200, 30) +
                        " and " + (loosePosition.Item3.Length > 0 ? "the" : "no") + " extremal vertices" +
                        loosePosition.Item3.ToCommaSeparatedString().AddDotsMiddle(200, 30)
                    ),
                description = "Pull tight at one or more edges.",
                buttons = new[] { "Tighten All", "Tighten Selected" },
                allowMultipleSelection = true
            };
        }
        
        var c = GetValenceOneJunctions(); 
        // shouldn't happen at this point (tight & no invariant subforests => no valence-one junctions)
        if (c.Any())
            return new AlgorithmSuggestion()
            {
                options = from v in c select (v.Name as object, v.ToString()),
                description = "Remove a valence-one junction. WHAT THE HECK?",
                buttons = new[] { "Valence-1 Removal" },
                allowMultipleSelection = true
            };
        if (AbsorbIntoPeriphery(true))
            return new AlgorithmSuggestion()
            {
                options = new (object, string)[] { (null,
                        GetMaximalPeripheralSubgraph().Edges.Select(e => 
                        peripheralSubgraph.ContainsEdge(e) 
                            ? e.Name 
                            : $"<b>{e.Name}</b>"
                    ).ToCommaSeparatedString()
                ) }, // todo? Display Q?
                description = "Absorb into periphery.",
                buttons = new[] { "Absorb" }
            };
        // todo: At this step, if the transition matrix for H is reducible, return with a reduction.
        var d = GetValenceTwoJunctions();
        if (d.Any())
            return new AlgorithmSuggestion()
            {
                options = from v in d select (v.Name as object, v.ToString()),
                description = "Remove a valence-two junction.",
                buttons = new[] { "Remove Selected", "Remove All" },
                allowMultipleSelection = true
            };
        var e = GetPeripheralInefficiencies();
        if (e.Any())
            return new AlgorithmSuggestion()
            {
                // l is a list of edges with the same Dg and this Dg(l) is in the periphery.
                options = from l in e select (l.Select(edge => edge.Name) as object,
                    l.Select(edge => edge.Name).ToCommaSeparatedString() + $" are all mapped to {l.First()!.Dg!.Name} ∈ P."
                ),
                description = "Fold initial segments of edges that map to edges in the pre-periphery.",
                buttons = new[] { "Fold Selected" }
            };
        var f = GetInefficiencies();
        if (f.Any())
            return new AlgorithmSuggestion()
            {
                options = from inefficiency in f.OrderBy(inefficiency => inefficiency.order) select (inefficiency.ToSerializationString() as object, inefficiency.ToString()),
                description = "Remove an inefficiency.",
                buttons = new[] { "Remove Inefficiency" },
            };

        return AlgorithmSuggestion.Finished;
    }

    public void ApplySuggestion(IEnumerable<(object, string)> suggestion, object button)
    {
        switch (button)
        {
            case "Collapse":
                if (suggestion.FirstOrDefault().Item1 is IEnumerable<string> subforestEdges)
                    CollapseInvariantSubforest(subforestEdges);
                // only select one subforest at a time because they might intersect and then the other subforests wouldn't be inside the new graph anymore.
                break;
            case "Tighten Selected":
                foreach (var (o ,_) in suggestion)
                {
                    if (o is not string name)
                    {
                        Debug.LogError($"Weird type of suggestion for Tighten Selected: {o}");
                        continue;
                    }
                    PullTightAll(name);
                }
                break;
            case "Tighten All":
                PullTightAll();
                break;
            case "Valence-1 Removal":
                foreach (var (o, _) in suggestion)
                    if (o is string name)
                        RemoveValenceOneJunction(graph.Vertices.First(v => v.Name == name));    
                    else
                        Debug.LogError($"Weird type of suggestion for Valence-1 Removal: {o}");
                break;
            case "Absorb":
                AbsorbIntoPeriphery();
                break;
            case "Remove Selected": // valence 2 junctions
                foreach (var (o, _) in suggestion.ToArray())
                {
                    if (o is string name)
                        RemoveValenceTwoJunction(graph.Vertices.First(v => v.Name == name), null);
                    else
                        Debug.LogError($"Weird type of suggestion for Remove Selected Valence-2 Vertex: {o}");
                    
                    // at some point there might be invariant subforests!
                    if (GetInvariantSubforests().Any())
                        break;
                }

                break;
            case "Remove All": // valence 2 junctions
                var maxSteps = graph.VertexCount;
                for (int i = 0; i < maxSteps; i++)
                {
                    var junction = GetValenceTwoJunctions().FirstOrDefault();
                    // at some point there might be invariant subforests!
                    if (GetInvariantSubforests().Any())
                        break;
                    
                    if (junction == null) break;
                    RemoveValenceTwoJunction(junction);
                }

                break;
            case "Fold Selected": // peripheral inefficiencies
                if (suggestion.FirstOrDefault().Item1 is IEnumerable<string> edges)
                    RemovePeripheralInefficiency(edges.Select(name => OrientedEdges.FirstOrDefault(e => e.Name == name)).ToList());
                else Debug.LogError($"Weird type of suggestion for Fold Selected: {suggestion.FirstOrDefault().Item1}");
                break;
            case "Remove Inefficiency":
                if (suggestion.FirstOrDefault().Item1 is string inefficiencyText)
                {
                    var a = inefficiencyText.Split('@');
                    var edge = OrientedEdges.First(e => e.Name == a[0]);
                    var index = int.Parse(a[1]);
                    RemoveInefficiency(new Inefficiency(edge[index]));
                }
                else Debug.LogError($"Weird type of suggestion for Remove Inefficiency: {suggestion.FirstOrDefault().Item1}");
                // todo: split into steps?
                break;
            default:
                HandleInconsistentBehavior("Unknown button.");
                break;
        }
        // sanity tests
        var edgeWithBrokenEdgePath = Strips.FirstOrDefault(s =>
            s.EdgePath.Count != 0 && !Enumerable.Range(0, s.EdgePath.Count - 1)
                .All(i => s.EdgePath[i].Target == s.EdgePath[i + 1].Source));
        if (edgeWithBrokenEdgePath != null)
            HandleInconsistentBehavior($"The edge {edgeWithBrokenEdgePath} has a broken edge path.");
        var brokenSelfEdge = Strips.FirstOrDefault(s => s.Source == s.Target && s.EdgePath.Count == 0);
        if (brokenSelfEdge != null)
            HandleInconsistentBehavior(
                $"The edge {brokenSelfEdge} is a self-edge that gets mapped into a vertex! This should not happen as we assume that the fibred surface is embedded as a deformation retract of the surface and thus no loop should be mapped to a vertex (no non-forest into a forest).");
        var duplicateName = graph.Edges.FirstDuplicate(e => e.Name);
        if (duplicateName != null)
            HandleInconsistentBehavior($"The name of {duplicateName} is used twice.");
        var weirdCurveName = Strips.FirstOrDefault(s => s.Reversed().Curve.Name != s.Name + "'");
        if (weirdCurveName != null)
            HandleInconsistentBehavior($"The curve {weirdCurveName} has a weird name.");
        var brokenVertex = graph.Vertices.FirstOrDefault(v => v.graph != graph);
        if (brokenVertex != null)
            HandleInconsistentBehavior($"The vertex {brokenVertex} doesn't refer to this graph.");
        var brokenVertexMapEdge = BrokenVertexMapEdge();
        if (brokenVertexMapEdge != null)
            HandleInconsistentBehavior($"The edge {brokenVertexMapEdge.Name} starts at {brokenVertexMapEdge.Source} with g({brokenVertexMapEdge.Source}) = {brokenVertexMapEdge.Source.image}, but g({brokenVertexMapEdge.Name}) starts at o(Dg({brokenVertexMapEdge.Name})) = o({brokenVertexMapEdge.Dg?.Name}) = {brokenVertexMapEdge.Dg?.Source}");
        var brokenVertexGraphAssociation = graph.Vertices.FirstOrDefault(v => v.graph != graph);
        if (brokenVertexGraphAssociation != null)
            HandleInconsistentBehavior($"The vertex {brokenVertexGraphAssociation} doesn't refer to this graph.");
        var brokenEdgeGraphAssociation = Strips.FirstOrDefault(e => e.graph != graph);
        if (brokenEdgeGraphAssociation != null)
            HandleInconsistentBehavior($"The edge {brokenEdgeGraphAssociation} doesn't refer to this graph.");
        for (int k = 0; k < 4; k++)
        {
            foreach (var vertex in graph.Vertices)
            {
                var star = StarOrdered(vertex).ToList();
                var starts = (from edge in star select edge.EdgePath.Take(k)).ToHashSet();
                foreach (var start in starts)
                {
                    var edges = (from edge in star where edge.EdgePath.Take(k).SequenceEqual(start) select edge).ToArray();
                    if (!IsConnectedSet(star, edges))
                        HandleInconsistentBehavior($"The edges {string.Join(", ", edges.Select(e => e.Name))} are not connected in the star of {vertex.Name}, but all start with the same edge path {string.Join(" ", start.Select(e => e.Name))}.");
                }
            }
        }
        var brokenJumpPoints = Strips.FirstOrDefault(e => e.Curve.VisualJumpPoints.Count() != e.Curve.VisualJumpTimes.Count());
        if (brokenJumpPoints != null)
            HandleInconsistentBehavior($"The edge {brokenJumpPoints} has a broken jump point.");
    }

    private Strip BrokenVertexMapEdge() => OrientedEdges.FirstOrDefault(e => e.Dg != null && e.Source.image != e.Dg.Source);

    private static void HandleInconsistentBehavior(string errorMessage)
    {
        Debug.LogError(errorMessage);
        Object.FindObjectsByType<FibredSurfaceMenu>(FindObjectsSortMode.None).First().StopAllCoroutines();
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
        var vertexString = string.Join("\n", graph.Vertices.Select(vertex => vertex.ToColorfulString()));
        var edgeString = string.Join("\n", StripsOrdered.Select(edge => edge.ToColorfulString()));
        return $"<line-indent=-20>{vertexString}\n{edgeString}";
    }

    public static IEnumerable<Strip> Star(Junction junction)
    {
        return junction.graph.AdjacentEdges(junction).SelectMany(strip =>
            strip.IsSelfEdge() ? new[] { strip, strip.Reversed() } :
            strip.Source != junction ? new[] { strip.Reversed() } :
            new[] { strip });
    }
    /// <summary>
    /// This is the cyclic order of the star of the given junction.
    /// If firstEdge is not null, the order is shifted to start with firstEdge.
    /// </summary>
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
                HandleInconsistentBehavior("The star of the subgraph didn't loop correctly. Stopped infinite loop");
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
        int startIndex = sortedStar.FindIndex(connectedSet.Contains);
        // this should be the index in the cyclic order where the connected set starts
        if (startIndex == -1) HandleInconsistentBehavior("The connected set is not in the star.");
        if (startIndex == 0) startIndex = sortedStar.FindLastIndex(e => !connectedSet.Contains(e)) + 1;
        // if the connected set is all of the star, then this is -1 + 1 = 0.
        return sortedStar.CyclicShift(startIndex).Take(connectedSet.Count);
    }
    
    static bool IsConnectedSet(List<Strip> sortedStar, ICollection<Strip> connectedSet) => 
        SortConnectedSetInStar(sortedStar, connectedSet).All(connectedSet.Contains);

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

    public void CollapseInvariantSubforest(IEnumerable<string> edges)
    {
        var subforest = new FibredGraph(true);
        var edgeNames = edges.ToHashSet();
        foreach (var strip in Strips)
        {
            if (edgeNames.Contains(strip.Name))
                subforest.AddVerticesAndEdge(strip);
        }
        CollapseInvariantSubforest(subforest);
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
                name: NextVertexName(),
                color: NextVertexColor()
            );
            newVertices.Add(newVertex);
            
            // ReplaceVertices(SubgraphStarOrdered(component), newVertex);
            
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

        foreach (var junction in graph.Vertices)
        {
            if (junction.image == null) continue;
            if (componentDict.TryGetValue(junction.image, out var index))
                junction.image = newVertices[index];
        }

        for (var index = 0; index < newVertices.Count; index++)
        {
            var newVertex = newVertices[index];
            var absorbedJunction = componentList[index].Vertices.First();
            if (componentDict.TryGetValue(absorbedJunction.image, out var imageIndex))
                newVertex.image = newVertices[imageIndex];
            else
                newVertex.image = absorbedJunction.image;

            graph.AddVertex(newVertex); 
            // shouldn't be necessary because we assign edge.Source for edges in the subgraph star (if that is empty, then our entire graph was a tree, i.e. the surface was a disk, which is too trivial)
        }
    }

    #endregion

    #region Valence one and two junctions

    public IEnumerable<Junction> GetValenceOneJunctions() =>
        from vertex in graph.Vertices where Star(vertex).Count() == 1 select vertex;

    public void RemoveValenceOneJunction(Junction junction)
    {
        var star = Star(junction).ToArray();
        if (star.Length != 1) throw new($"Supposed valence-one junction has valence {star.Length}");
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
        if (star.Length != 2) throw new($"Supposed valence-two junction has valence {star.Length}");

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
            curve: removeStrip.Curve.Reversed().Concatenate(keptStrip.Curve),
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
        // FirstOrDefault() gets called > 300 times on this in a typical call to PullTightAll (takes > 1 second) 
        if (edge != null)
            return from edgePoint in GetBackTracks()
                where Equals(edgePoint.DgAfter(), edge)
                select edgePoint;

        return from strip in Strips
            where strip.EdgePath.Count > 1
            from i in Enumerable.Range(1, strip.EdgePath.Count - 1)
            // only internal points: Valence-2 extremal vertices are found in parallel anyways.
            let edgePoint = new EdgePoint(strip, i)
            // gets called > 20000 times in a typical call to PullTightAll (takes > 1 second)
            where Equals(edgePoint.DgBefore(), edgePoint.DgAfter())
            select edgePoint; 
    }

    private void PullTightExtremalVertex(Junction vertex)
    {
        vertex.image = null;
        foreach (var strip in Star(vertex))
        {
            strip.EdgePath = strip.EdgePath.Skip(1).ToList();
            vertex.image ??= strip.Dg?.Source;
            // for self-loops, this takes one from both ends.
        }
        // isotopy: Move vertex and shorten the strips (only the homeomorphism is changed, not the graph)
        // todo? update EdgePoints?
    }

    private void PullTightBackTrack(EdgePoint backTrack, IList<EdgePoint> updateEdgePoints = null)
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

    public void PullTightAll(string edgeName) => 
        PullTightAll(OrientedEdges.FirstOrDefault(e => e.Name == edgeName));

    public void PullTightAll(Strip edge = null)
    {
        var limit = Strips.Sum(e => e.EdgePath.Count) + graph.Vertices.Count();
        for (int i = 0; i < limit; i++)
        {
            var extremalVertex = GetExtremalVertices(edge).FirstOrDefault();
            if (extremalVertex != null)
            {
                PullTightExtremalVertex(extremalVertex);
                continue;
            }

            var backTrack = GetBackTracks(edge).FirstOrDefault();
            if (backTrack != null)
            {
                PullTightBackTrack(backTrack);
                continue;
            }

            break;
        }
    }

    #endregion

    #region Moving Vertices

    class MovementForFolding
    {
        public readonly IList<Strip> edges;
        public readonly Strip preferredEdge;
        public readonly IReadOnlyDictionary<Junction, IEnumerable<(string, bool)>> vertexMovements; // the vertices that are folded and how they are moved
        public readonly int l; // number of side crossings along f that the resulting edge will have
        private readonly List<(string, bool)> c;
        private int badness = -1;
        private readonly Dictionary<Strip,int> edgeCancellations;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="edges">edges to fold as ordered in the cyclic order of the star</param>
        /// <param name="preferredEdge"></param>
        /// <param name="l"></param>
        /// <exception cref="ArgumentException"></exception>
        public MovementForFolding(IList<Strip> edges, Strip preferredEdge, int l)
        {
            this.edges = edges; 
            this.preferredEdge = preferredEdge;
            this.l = l;
            
            c = preferredEdge.Curve.SideCrossingWord.Take(l).ToList();
            if (c.Count < l)
                throw new ArgumentException($"The edge {preferredEdge} has not enough side crossings: {c.Count} < {l}", nameof(preferredEdge));

            edgeCancellations =
                edges.ToDictionary(edge => edge, edge => edge.Curve.SideCrossingWord.SharedInitialSegment(c));
            
            vertexMovements = new Dictionary<Junction, IEnumerable<(string, bool)>>(
                from edge in edges
                let sharedInitialSegment= edgeCancellations[edge]
                select new KeyValuePair<Junction, IEnumerable<(string, bool)>>(
                    edge.Target, // vertex
                    edge.Curve.SideCrossingWord.Skip(sharedInitialSegment).Inverse()
                        .Concat( c.Skip(sharedInitialSegment) )
                    // the movement of the vertex as a word in the sides of the model surface that it crosses
                    // with the skips this Concat is exactly ConcatWithCancellation
                )
            );
            
        }

        public int Badness {
            get
            {
                if (badness != -1)
                    return badness;
                
                var count = 0;
                foreach (var edge in edges.First().graph.Edges)
                {
                    if (edges.Contains(edge)) continue;
                    
                    var sideCrossingWord = edge.Curve.SideCrossingWord;
                    if (vertexMovements.TryGetValue(edge.Source, out var movement)) 
                        sideCrossingWord = movement.Inverse().ConcatWithCancellation(sideCrossingWord);
                    if (vertexMovements.TryGetValue(edge.Target, out var movement2))
                        sideCrossingWord = sideCrossingWord.ConcatWithCancellation(movement2);
                    count += sideCrossingWord.Count();
                }

                count += l; // l is the number of side crossings along the edge that results from folding
                badness = count;
                return count;
            }
        }

        public void MoveVerticesForFolding(FibredSurface fibredSurface, bool removeEdges = false)
        {
            // shorten the prefereed curve if necessary
            var preferredCurve = preferredEdge.Curve;
            var (t0l, t1l) = preferredCurve.VisualJumpTimes.Prepend(0f).Skip(l);
            var timeFEnd = (t1l + t0l) / 2; 
            if (t1l == 0) // preferredCurve.VisualJumpTimes has only l elements, i.e. the curve ends before crossing another side
                timeFEnd = preferredCurve.Length;

            if (timeFEnd < preferredCurve.Length)
                fibredSurface.MoveJunction(preferredEdge.Reversed(), preferredCurve.Length - timeFEnd);
            preferredCurve = preferredEdge.Curve; // = preferredCurve.Restrict(0, timeFEnd);
            
            if (removeEdges)
                foreach (var edge in edges)
                    if (!Equals(edge, preferredEdge))
                        fibredSurface.graph.RemoveEdge(edge.UnderlyingEdge);

            var stringWidths = (from edge in edges select baseShiftStrength * Mathf.Sqrt(Star(edge.Target).Count())).ToArray();
            var preferredEdgeIndex = edges.IndexOf(preferredEdge);
            var edgeIndex = -1;

            foreach (var edge in edges)
            {
                edgeIndex++;
                if (Equals(edge, preferredEdge))
                    continue;
                var vertex = edge.Target;
                var backwardsCurve = edge.Curve.Reversed();
                
                
                var sharedSegment = edgeCancellations[edge];
                var n = backwardsCurve.SideCrossingWord.Count() - sharedSegment; 
                // pull back the vertex for n steps (through n sides) along the edge.
                // Then the edge agrees with the shared initial segment with the preferred edge.
            
                var timeX = 0f; // the time along the backwards curve "close" to the preferred edge, where the edges attached to the vertex will turn to the preferred edge
                if (n > 0)
                {
                    var (t0x, t1x) = backwardsCurve.VisualJumpTimes.Skip(n - 1);
                    if (t1x == 0) // backwardsCurve.VisualJumpTimes has only n elements, i.e. the curve ends before crossing another side (sharedSegment = 0)
                        t1x = backwardsCurve.Length;
                    timeX = (t0x + 2 * t1x) / 3; // a bit closer to the last side crossing along the original edge that is shared with the preferred edge 
                    // todo: fixed distance to last crossing (or the source vertex if there is no crossing)? Shorter for the preferred edge in that case for better visuals?

                    fibredSurface.MoveJunction(vertex, backwardsCurve, timeX, removeEdges ? edge.Reversed() : null);
                    // moves along backwardsCurve and shortens the edge itself (unless we removed it)
                }

                var (t0, t1) = preferredCurve.VisualJumpTimes.Prepend(0f).Skip(sharedSegment); 
                // the same as the Skip(sharedSegment - 1) but with the first element 0 for the case sharedSegment = 0
                var timeF = (t0 * 3 + t1) / 4; // a bit closer to the last side crossing along the preferred edge that is shared with the original edge 
                if (t1 == 0) // preferredCurve.VisualJumpTimes has only sharedSegment elements, i.e. the curve ends before crossing another side (l = sharedSegment = length of preferredCurve.VisualJumpPoints)
                    timeF = preferredCurve.Length;

                Curve restCurve = null;
                Point timeFPoint = preferredCurve[timeF];
                if (l > sharedSegment)
                {
                    float relativeShiftStrength;
                    if (edgeIndex < preferredEdgeIndex)
                        relativeShiftStrength = stringWidths[edgeIndex] / 2f + stringWidths[(edgeIndex + 1)..preferredEdgeIndex].Sum();
                    else 
                        relativeShiftStrength = - stringWidths[edgeIndex] / 2f - stringWidths[(preferredEdgeIndex + 1)..edgeIndex].Sum();
                        
                    restCurve = new ShiftedCurve(preferredCurve.Restrict(timeF), relativeShiftStrength, ShiftedCurve.ShiftType.FixedEndpoint);
                    timeFPoint = restCurve.StartPosition;
                }
                
                // turning from the backwards curve to the preferred curve
                var intermediateCurve = fibredSurface.GetBasicGeodesic(
                    backwardsCurve[timeX],
                    timeFPoint,
                    "intermediate");

                Curve forwardMovementCurve = intermediateCurve;
                if (restCurve != null)
                    forwardMovementCurve = new ConcatenatedCurve(new[]
                    {
                        intermediateCurve,
                        restCurve
                    }, smoothed: true);

                
                if (forwardMovementCurve.Length > 1e-3) 
                    fibredSurface.MoveJunction(vertex, forwardMovementCurve, forwardMovementCurve.Length);
                // this prolongs the edge (unless we removed it) 
                
                // todo: addedShiftStrength positive or negative depending on the direction of the edge and the cyclic order.
                // More than one (better: add up the widths) if several vertices are moved along this edge, so that all of the edges are disjoint (hard)
                
                continue;
            }
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.AppendLine($"Movement plan: Shorten the \"preferred edge\" {preferredEdge.Name} to the first {l}/{preferredEdge.Curve.SideCrossingWord.Count()} side crossings: ");
            sb.AppendLine($"  {string.Join(", ", c)}");
            sb.AppendLine($"Badness: {Badness}");
            sb.AppendLine($"Vertex movements:");
            foreach (var (vertex, movement) in vertexMovements)
                sb.AppendLine($"{vertex}: {(from sideCrossing in movement select sideCrossing.Item1 + (sideCrossing.Item2 ? "'" : "")).ToCommaSeparatedString()}");
            return sb.ToString(); 
        }
    }

    public void MoveJunction(Strip e, float length) => MoveJunction(e.Source, e.Curve, length, e);

    
    const float baseShiftStrength = 0.07f;
    public void MoveJunction(Junction v, Curve curve, float length, Strip e = null) 
    {
        v.Patches = new []{ curve[length] };
        
        var precompositionCurve = curve.Restrict(0, length).Reversed();
        var star = StarOrdered(v, e).ToList();
        if (e != null)
        {
            star.RemoveAt(0);
            var name = e.Name;
            e.Curve = e.Curve.Restrict(length);
            e.Name = name;
        }
 
        float shift = baseShiftStrength / Mathf.Sqrt(star.Count);
        
        for (var i = 0; i < star.Count; i++)
        {
            var edge = star[i];
            var shiftStrength = ((star.Count - 1f) / 2f - i) * shift;
            var shiftedCurve = new ShiftedCurve(precompositionCurve, shiftStrength, ShiftedCurve.ShiftType.SymmetricFixedEndpoints);

            var name = edge.Name;
            edge.Curve = new ConcatenatedCurve(new[]
            {
                shiftedCurve,
                // surface.GetGeodesic(shiftedCurve.EndPosition, edge.Curve.StartPosition, ""),
                edge.Curve
            }) { Color = edge.Color }; // smooth?
            edge.Name = name;
        }
        
    }

    
    #endregion
    
    #region Folding initial segments

    public Strip FoldEdges(IList<Strip> edges, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();

        if (edges.Count < 2)
        {
            Debug.Log($"Wanted to fold {edges.Count} edges. Nothing to do.");
            return edges.FirstOrDefault();
        }

        var edgePath = edges[0].EdgePath;
        if (edges.Any(edge => !edge.EdgePath.SequenceEqual(edgePath)))
            throw new("Edges to fold do not have the same edge path.");

        var star = StarOrdered(edges[0].Source).ToList();

        var edgesOrdered = SortConnectedSetInStar(star, edges).ToList();
        if (!edges.All(edgesOrdered.Contains))
            throw new($"Edges to fold are not connected in the cyclic order: {string.Join(", ", edges)}");
        
        edges = edgesOrdered;

        
        var movements = new List<MovementForFolding>(
            from edge in edges
            from i in Enumerable.Range(0, edge.Curve.SideCrossingWord.Count() + 1).Reverse()
            select new MovementForFolding(edges, edge, i)
        );
        
        var badness = movements.Min(m => m.Badness); // todo: takes a lot of time

        var moveSelection = (
            from i in Enumerable.Range(0, edges.Count)
            let e = i % 2 == 0 ? edges[edges.Count / 2 + i / 2] : edges[edges.Count / 2 - i / 2 - 1] // prefer the middle edges
            let mv = movements.FirstOrDefault(m => Equals(m.preferredEdge, e) && m.Badness == badness) 
            where mv != null // choose only the ones with the least amount of edge-crossings
            select mv
        ).ToList();
        
        var movement =
            moveSelection.FirstOrDefault(e => e.preferredEdge is UnorientedStrip && !char.IsDigit(e.preferredEdge.Name[^1])) ??
            moveSelection.FirstOrDefault(e => !char.IsDigit(e.preferredEdge.Name[^1])) ??
            moveSelection.FirstOrDefault(e => e.preferredEdge is UnorientedStrip) ??
            moveSelection.First();

        var targetVerticesToFold =
            (from edge in edges select edge.Target).WithoutDuplicates().ToArray(); // has the correct order
        var preferredOldEdge = movement.preferredEdge;
        
        var cyclicOrderAtFoldedVertex = StarAtFoldedVertex().ToList();
        
        movement.MoveVerticesForFolding(this, removeEdges: true);

        string name = null;
        if (!char.IsLetter(preferredOldEdge.Name[^1]))
            name = NextEdgeName();
        else name = preferredOldEdge.Name.ToLower();


        // todo: if the (folding) edges pass through a side, we should isotope in the following way:
        // Move all of the vertices in targetVerticesToFold that arent the source vertex edges[0].Source (all or all but one) along 
        // (any of) the corresponding edge in reverse.
        // Then all but the potential self-edge that we fold don't pass through boundary, so the connecting curve will more likely make sense.
        // If there is a self-edge there, either also move the vertex along the self-edge in reverse
        // (affecting the start of all of our folding edges and all other edges at that vertex)
        // Or move all of our already moved vertices along the self-edge (which prolongs all edges at these vertices)
        
        // the ClampPoint prevents that ClampPoint is called again later, but with a too large tolerance
        // var waypoints = from edge in edges select surface.ClampPoint( edge.Curve.EndPosition, 1e-6f ); 
        // var connectingCurve = surface.GetPathFromWaypoints(waypoints, newVertexName);
        // var patches = targetVerticesToFold.Append<IDrawnsformable>(connectingCurve);
        
        // var newVertexName = NextVertexName();
        // var vertexColor = NextVertexColor();
        var newVertex = preferredOldEdge.Target; // .Copy(name: newVertexName, color: vertexColor);
        // graph.AddVertex(newVertex);

        var newEdge = preferredOldEdge; //.Copy(name: name, target: newVertex, orderIndexEnd: 0);
        newEdge.UnderlyingEdge.Name = name;
        newEdge.OrderIndexEnd = 0;
        // orderIndexStart might have been set in the loop above (if one of the targetVerticesToFold was the source of the edge)
        
        // graph.AddEdge(newEdge.UnderlyingEdge);


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
        ReplaceVertices(cyclicOrderAtFoldedVertex, newVertex);
        ReplaceEdges(edges, newEdge);
        

        return newEdge;

        IEnumerable<Strip> StarAtFoldedVertex()
        {
            yield return preferredOldEdge.Reversed();
            
            foreach (var vertex in targetVerticesToFold)
            {
                var localStar = StarOrdered(vertex).ToList();
                var outerEdges = localStar.ToHashSet();
                outerEdges.ExceptWith(from e in edges select e.Reversed());
                var outerEdgesSorted = SortConnectedSetInStar(localStar, outerEdges);
                foreach (var edge in outerEdgesSorted)
                {
                    yield return edge;
                }         
            }
        }
    }

    private void ReplaceEdges(ICollection<Strip> edges, Strip newEdge)
    {
        foreach (var strip in Strips)
        {
            strip.EdgePath = strip.EdgePath.Select(
                edge => edges.Contains(edge) ? newEdge :
                    edges.Contains(edge.Reversed()) ? newEdge.Reversed() : edge
            ).ToList();
        }
    }

    void ReplaceVertices(ICollection<Junction> vertices, Junction newVertex)
    {
        foreach (var strip in OrientedEdges.ToList())
            if (vertices.Contains(strip.Source))
                strip.Source = newVertex;

        foreach (var vertex in vertices)
            if (vertex != newVertex)
                graph.RemoveVertex(vertex);
        
        foreach (var junction in graph.Vertices)
        {
            if (vertices.Contains(junction.image))
                junction.image = newVertex;
        }
    }
    
    void ReplaceVertices(IEnumerable<Strip> newOrderedStar, Junction newVertex)
    {
        ReplaceVertices(newOrderedStar.Select(strip => strip.Source).ToHashSet(), newVertex);
        int index = 0;
        foreach (var strip in newOrderedStar)
        {
            strip.OrderIndexStart = index;
            index++;
        }
    }

    public (Strip, Strip) SplitEdge(EdgePoint splitPoint, IList<EdgePoint> updateEdgePoints = null, float splitTime = -1f)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        var splitEdge = splitPoint.edge;
        var edgePath = splitEdge.EdgePath;
        if (splitTime < 0)
            splitTime = splitPoint.GetCurveTimeInJunction();

        var newVertex = new Junction(graph, splitEdge.Curve[splitTime], NextVertexName(), splitPoint.Image, NextVertexColor());

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
            throw new("The edges do not have a common initial segment.");
            return;
        }

        updateEdgePoints ??= new List<EdgePoint>();
        var l = updateEdgePoints.Count;
        foreach (var strip in strips)
            updateEdgePoints.Add(strip[i.Value]);
        // we have to update the edge points after each split, as else we might split an edge and its reverse (which doesn't exist anymore after we split the original edge), thus creating 4 instead of 3 new edges! (or instead of 2)


        var initialStripSegments = new List<Strip>(strips.Count);
        var terminalStripSegments = new Dictionary<string, Strip>(strips.Count);
        var arclengthParametrization = (from strip in strips select 
            (   strip.Curve,
                GeodesicSurface.ArclengthFromTime(strip.Curve),
                GeodesicSurface.TimeFromArclength(strip.Curve)
            )).ToArray(); // This took up 90 % of the time (6s for a simple fold), because of ModelSurface.DistanceSquared calling ClampPoint often. 
        float maxDistanceAlongCurves = arclengthParametrization.Min( input =>
        {
            var (curve, lengthFromTime, _) = input;
            float jumpTime = curve.VisualJumpTimes.DefaultIfEmpty(curve.Length).First();
            return lengthFromTime(jumpTime);
        });
        float splitLength = maxDistanceAlongCurves * 0.5f;
        // todo: optimize this with respect to the movements
        
        for (var index = 0; index < strips.Count; index++)
        {
            var splitEdgePoint = updateEdgePoints[l + index];
            var edge = splitEdgePoint.edge; // instead of strips[index]
            int splitIndex = splitEdgePoint.index;
            if (splitIndex == edge.EdgePath.Count) // it is never 0
            {
                initialStripSegments.Add(edge);
                continue;
            }

            var timeFromLength = arclengthParametrization[index].Item3;
            float splitTime = timeFromLength(splitLength);
            var (firstSegment, secondSegment) = SplitEdge(splitEdgePoint, updateEdgePoints, splitTime);
            
            initialStripSegments.Add(firstSegment);
            terminalStripSegments[edge.Name.ToLower()] = secondSegment;
        }

        // give the remaining segments the original names. 
        // We do this here so that if we split an edge and its reverse the middle part gets the same name as the original edge.
        foreach (var (name, secondSegment) in terminalStripSegments)
        {
            secondSegment.UnderlyingEdge.Name = name;
        }


        var newEdge = FoldEdges(initialStripSegments, updateEdgePoints);
        // newEdge.Color = NextEdgeColor();
    }

    #endregion

    #region (Essential) Inefficiencies

    private static IEnumerable<(Strip, Strip)> InefficientConcatenations<T>(List<Gate<T>> gates)
    {
        foreach (var gate in gates)
        {
            var edges = gate.Edges;
            for (var i = 0; i < edges.Count; i++)
            for (var j = i; j < edges.Count; j++)
                yield return (edges[i].Reversed(), edges[j]);
        }
    }

    public IEnumerable<Inefficiency> GetInefficiencies()
    {
        var gates = Gate.FindGates(graph);

        foreach (var (edge1, edge2) in InefficientConcatenations(gates))
        {
            var (edge1Rev, edge2Rev) = (edge1.Reversed(), edge2.Reversed());
            foreach (var strip in Strips)
            {
                for (int i = 1; i < strip.EdgePath.Count; i++)
                {
                    if (
                        (!strip.EdgePath[i - 1].Equals(edge1) || !strip.EdgePath[i].Equals(edge2)) && 
                        (!strip.EdgePath[i - 1].Equals(edge2Rev) || !strip.EdgePath[i].Equals(edge1Rev))
                    ) continue;
                    yield return new Inefficiency(new EdgePoint(strip, i));
                    goto found;
                }
            }
            // todo: valence two vertices with only two gates

            found: ;
        }
        
        // foreach (var edge in Strips) // inefficient!!
        // {
        //     for (int i = 0; i <= edge.EdgePath.Count; i++)
        //     {
        //         var inefficiency = CheckEfficiency(edge[i], gates);
        //         if (inefficiency != null)
        //         {
        //             if (i == 0 || i == edge.EdgePath.Count)
        //                 Debug.Log("Interpreted a valence-two gate-wise extremal vertex as an inefficiency.");
        //             yield return inefficiency;
        //         }
        //     }
        // }
    }

    [CanBeNull]
    public Inefficiency CheckEfficiency(EdgePoint edgePoint, List<Gate<Junction>> gates)
    {
        var a = edgePoint.DgBefore();
        var b = edgePoint.DgAfter();
        if (a == null || b == null) return null; // the edgePoints is actually a vertex of valence other than two

        if (!Equals(a.Source, b.Source))
            throw new($"The edge path is not an actual edge path at {edgePoint}!");
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
            // The inefficiency is a back track or a valence two extremal vertex
            PullTightAll(p.DgAfter());
            return;
        }

        var initialSegment = p.initialSegmentToFold;
        var edgesToFold = p.edgesToFold;

        while (edgesToFold.Any(edge =>
                   Equals(p, new EdgePoint(edge, initialSegment))
               ))
        {
            Debug.Log(
                $"When folding the inefficiency (illegal turn) \"{p}\" we had to decrease the initial segment because the point where the illegal turn happened was the same as the subdivision / folding point");
                initialSegment--;
        }

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

            foreach (var edge in edgesToSplit) // we split the last edge first, so that we can split the one before and so on.
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
           throw new(
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
            if (strip.Dg == null) throw new($"The strip {strip} has been absorbed into the periphery.");
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
            var newJunction = new Junction(graph, surface.GetPathFromWaypoints(positions, name), name, color: NextVertexColor()); // image set below
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
                throw new(
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

    #region Names and Colors

    readonly List<string> edgeNames = new()
    {
        "a", "b", "c", "d", /*"e",*/ "x", "y", "z", /*"w",*/ "u", "h", "i", "j", "k", "l", "m", "n", "o",
        "α", "β", "γ", "δ", "ε", "ζ", "θ", "κ", "λ", "μ", "ξ", "π", "ρ", "σ", "τ", "φ", "ψ", "ω",
    };

    readonly List<string> vertexNames = new()
    {
        "v", "w", "p", "q", "r", "s", "t",
    };
    
    readonly List<Color> edgeColors = Curve.colors;

    readonly List<Color> vertexColors = new()
    {
        Color.black, new Color32(26, 105, 58, 255), new Color32(122, 36, 0, 255),
        new Color(0.3f, 0.3f, 0.3f), 
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

    Color NextEdgeColor()
    {
        var colorUsage = edgeColors.ToDictionary(c => c, c => 0);
        foreach (var strip in Strips) 
            colorUsage[strip.Color]++;
        var (leastUsedColor, _) = colorUsage.Keys.ArgMin(c => colorUsage[c]);
        return leastUsedColor;
    }

    Color NextVertexColor()
    {
        var colorUsage = vertexColors.ToDictionary(c => c, c => 0);
        foreach (var junction in graph.Vertices)
        {
            if (colorUsage.ContainsKey(junction.Color))
                colorUsage[junction.Color]++;
            else
            {
                colorUsage[junction.Color] = 1;
                Debug.LogWarning($"The color {junction.Color} of junction {junction} is not in the list of vertex colors.");   
            }
        }
        var (leastUsedColor, _) = colorUsage.Keys.ArgMin(c => colorUsage[c]);
        return leastUsedColor;
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

    public Curve GetBasicGeodesic(Point start, Point end, string name)
    {
        var surface = this.surface;
        if (surface is ModelSurface modelSurface)
            return modelSurface.GetBasicGeodesic(start, end, name);
        Debug.LogWarning($"The surface {surface} is not a model surface.");
        return surface.GetGeodesic(start, end, name);
    }
}
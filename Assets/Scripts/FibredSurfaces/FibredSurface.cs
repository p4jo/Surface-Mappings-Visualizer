using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using QuikGraph.Algorithms;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

/// <summary>
/// This implements the Bestvina-Handel algorithm.
/// All methods are in-place. Copy the graph before.
/// </summary>
public class FibredSurface: IPatchedTransformable
{
    public readonly FibredGraph graph;
    /// <summary>
    /// The peripheral subgraph P contains one loop around every puncture of the surface apart from one orbit of punctures.
    /// P is assumed to have only valence-two vertices (that have higher valence in the main graph).
    /// We also assume that g acts on it as a graph automorphism.
    /// </summary>
    public readonly FibredGraph peripheralSubgraph;

    public IEnumerable<ITransformable> Patches => graph.Vertices.Concat<ITransformable>(from edge in graph.Edges select edge.Curve);
    private IEnumerable<Strip> Edges => graph.Edges.Concat<Strip>(graph.Edges.Select(edge => edge.Reversed()));

    public FibredSurface(FibredGraph graph, FibredGraph peripheralSubgraph = null)
    {
        this.graph = graph;
        this.peripheralSubgraph = peripheralSubgraph ?? new FibredGraph();
    }

    public FibredSurface(IList<(Curve, string, string, string[])> edgeDescriptions, IEnumerable<string> peripheralEdges = null, IDictionary<string, ITransformable> junctionDrawables = null)
    {
        junctionDrawables ??= new Dictionary<string, ITransformable>();
        peripheralEdges ??= new List<string>();
        graph = new FibredGraph(); // up here, so that the junctions reference this

        var vertexNames = 
            (from v in edgeDescriptions select v.Item2)
            .Concat(from v in edgeDescriptions select v.Item3).ToHashSet();
        var junctions = new Dictionary<string, Junction>(
            from name in vertexNames
            select new KeyValuePair<string, Junction>(name,
                new Junction(graph, 
                    junctionDrawables.TryGetValue(name, out var displayable) ? displayable : 
                        edgeDescriptions.FirstOrDefault(tuple => tuple.Item2 == name).Item1?.StartPosition 
                        ?? edgeDescriptions.FirstOrDefault(tuple => tuple.Item3 == name).Item1?.EndPosition)
            )
        );
        var strips = (
            from tuple in edgeDescriptions
            let curve = tuple.Item1
            let source = tuple.Item2
            let target = tuple.Item3
            select new UnorientedStrip(curve, junctions[source], junctions[target], new List<Strip>(), graph)
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
                Debug.LogError($"Two edges at the same vertex have images that don't start at the same vertex! g({name}) = {string.Join(' ', strip.EdgePath.Select(e => e.Name))} starts at o(g({name})) = {strip.Dg?.Source}, but we already set g(o({name})) = {strip.Source.image}.");
            strip.Source.image ??= strip.Dg?.Source;
        }

        graph.AddVerticesAndEdgeRange(strips);
        
        peripheralSubgraph = new FibredGraph();
        peripheralSubgraph.AddVerticesAndEdgeRange((
            from edgeName in peripheralEdges
            select edges[edgeName].UnderlyingEdge
            ).Distinct()
        );
    }

    public FibredSurface Copy()
    {        
        var newGraph = new FibredGraph(true);

        var newVertices = new Dictionary<Junction, Junction>(
            from vertex in graph.Vertices 
            select new KeyValuePair<Junction, Junction>(vertex, vertex.Copy(newGraph))
        );
        
        var newEdges = new Dictionary<Strip, UnorientedStrip>(graph.EdgeCount);
        foreach (var edge in graph.Edges)
        {
            var newEdge = edge.Copy();
            newEdge.Source = newVertices[edge.Source];
            newEdge.Target = newVertices[edge.Target];
            newEdge.EdgePath = edge.EdgePath.Select(
                strip => strip is OrderedStrip { reverse: true }
                    ? newEdges[strip.UnderlyingEdge].Reversed() as Strip
                    : newEdges[strip.UnderlyingEdge] as Strip
            ).ToList();
            newEdges[edge] = newEdge;
        }
        
        newGraph.AddVerticesAndEdgeRange(newEdges.Values); // technically happens in the Source.set and Target.set accessors
        var newPeripheralSubgraph = new FibredGraph(true);
        newPeripheralSubgraph.AddVerticesAndEdgeRange(
            from edge in peripheralSubgraph.Edges select newEdges[edge]
        );
        
        return new FibredSurface(newGraph, newPeripheralSubgraph);
    }

    
    
    public BHDisplayOptions NextSuggestion()
    {
        // todo? add a set of ITransformables to highlight with each option?
        // Iterate through the steps and give the possible steps to the user
        var a = GetInvariantSubforests();
        if (a.Any()) return new BHDisplayOptions()
        {
            options = a,
            description = "Collapse an invariant subforest.",
            buttons = new[] {"Collapse"}
        };
        var b = GetLoosePositions();
        if (b.Any()) return new BHDisplayOptions()
        {
            options = b.Cast<object>(),
            description = "Pull tight at one or more edges.",
            buttons = new[] {"Tighten Selected", "Tighten All"},
            allowMultipleSelection = true
        };
        var c = GetValenceOneJunctions(); // shouldn't happen at this point (tight & no invariant subforests => no valence-one junctions)
        if (c.Any()) return new BHDisplayOptions()
        {
            options = c,
            description = "Remove a valence-one junction. WHAT THE HECK?",
            buttons = new[] {"Valence-1 Removal"},
            allowMultipleSelection = true
        };
        if (AbsorbIntoPeriphery(true)) return new BHDisplayOptions()
        {
            options = new[]{ GetMaximalPeripheralSubgraph() }, // todo? Display Q?
            description = "Absorb into periphery.",
            buttons = new[] {"Absorb"}
        };
        // todo: At this step, if the transition matrix for H is reducible, return with a reduction.
        var d = GetValenceTwoJunctions();
        if (d.Any()) return new BHDisplayOptions()
        {
            options = d,
            description = "Remove a valence-two junction.",
            buttons = new[] {"Remove Selected", "Remove All"},
            allowMultipleSelection = true
        };
        var e = GetPeripheralInefficiencies();
        if (e.Any()) return new BHDisplayOptions()
        {
            options = e,
            description = "Fold initial segments of edges that map to edges in the pre-periphery.",
            buttons = new[] { "Fold Selected" }
        };
        var f = GetInefficiencies();
        if (f.Any()) return new BHDisplayOptions()
        {
            options = f.OrderBy(inefficiency => inefficiency.order),
            description = "Remove an inefficiency.",
            buttons = new[] {"Remove Inefficiency"},
        };
        
        return null; // finished!
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
        for (int i = 0; i < limit && ApplyNextSuggestion(); i++)
        { }
        // todo: save result (reducible / growth)
    }
    
    public void ApplySuggestion(IEnumerable<object> suggestion, object button)
    { 
        switch (button)
        {
            case "Collapse":
                if (suggestion.FirstOrDefault() is FibredGraph subforest) 
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
                    
                    foreach (var junction in s.Item3.ToArray() ) 
                        PullTightExtremalVertex(junction);
                    // todo: pulling tight along an edge and along the reverse edge do not commute! If you do both, some extremal vertices might not be extremal anymore, and some backtracks might not be backtracks anymore. (e.g. if St(v) = {e,f} and g(e) = dDa, g(f) = d, then v is extremal and e has a backtrack, both in d. But ?? maybe no problem?? TODO 
                }
                break;
            case "Tighten All":
                foreach (var vertex in GetExtremalVertices().ToArray()) // to avoid concurrent modification 
                    PullTightExtremalVertex(vertex);
                
                var backTracks2 = GetBackTracks().ToArray();
                // ReSharper disable once ForCanBeConvertedToForeach // we modify the list!
                for (var i = 0; i < backTracks2.Length; i++) 
                    PullTightBackTrack(backTracks2[i], backTracks2);

                break;
            case "Valence-1 Removal":
                foreach (var junction in suggestion.Cast<Junction>()) 
                    RemoveValenceOneJunction(junction);
                break;
            case "Absorb":
                AbsorbIntoPeriphery();
                break;
            case "Remove Selected":
                foreach (object o in suggestion)
                {
                    if (o is not Junction valence2Junction) continue;
                    // todo: select the strip to keep (lower width eigenvalue, or the pre-P one) 
                    RemoveValenceTwoJunction(valence2Junction, null);
                }
                break;
            case "Remove All":
                foreach (var junction in GetValenceTwoJunctions()) 
                    RemoveValenceTwoJunction(junction, null);
                break;
            case "Fold Selected":
                if (suggestion.FirstOrDefault() is IEnumerable<Strip> edges) 
                    RemovePeripheralInefficiency(edges.ToList());
                break;
            case "Remove Inefficiency":
                if (suggestion.FirstOrDefault() is Inefficiency inefficiency) 
                    RemoveInefficiency(inefficiency);
                break;
            default:
                Debug.LogError("Unknown button.");
                break;
        }
    }

    public class BHDisplayOptions
    {
        public IEnumerable<object> options;
        public string description;
        public IEnumerable<object> buttons;
        public bool allowMultipleSelection = false;
    }

    private static FibredGraph OrbitOfEdge(Strip edge)
    {
        List<UnorientedStrip> orbit = new (){ edge.UnderlyingEdge };
        for (int i = 0; i < orbit.Count; i++)
        {
            edge = orbit[i];
            foreach (var e in edge.EdgePath)
            {
                if (orbit.Contains(e.UnderlyingEdge)) continue;
                orbit.Add(e.UnderlyingEdge);
            }
        }

        FibredGraph newGraph = new FibredGraph();
        newGraph.AddVerticesAndEdgeRange(orbit);
        return newGraph;
    }

    /// <summary>
    /// only subforests whose collapse doesn't destroy the peripheral subgraph, i.e. whose components contain at most one vertex of the peripheral subgraph.
    /// components that are only vertices are missing!
    /// (but that doesn't matter because we only use this in the CollapseInvariantSubforest and MaximalPeripheralGraph methods)
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<FibredGraph> GetInvariantSubforests()
    { 
        Dictionary<UnorientedStrip, FibredGraph> subforests = new();
        
        foreach (var strip in graph.Edges)
        {
            if (subforests.Values.Any(subforest => subforest.Edges.Contains(strip))) continue;
            // there already is a larger subforest containing the one defined from this strip.

            FibredGraph orbitOfEdge = OrbitOfEdge(strip);
            
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
        peripheralSubgraph ??= this.peripheralSubgraph;
        if (remove)
        {
            var edges = subgraph.Edges.ToHashSet();
            edges.ExceptWith(peripheralSubgraph.Edges);
            subgraph = new FibredGraph(true); 
            subgraph.AddVerticesAndEdgeRange(edges);
        };
        if (!subgraph.IsUndirectedAcyclicGraph()) return false;

        var components = new Dictionary<Junction, int>();
        int numberOfComponents = subgraph.ConnectedComponents(components);
        int[] componentIntersections = new int[numberOfComponents];
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
    
    public void CollapseInvariantSubforest(FibredGraph subforest)
    {
        Dictionary<Junction, int> components = new();
        int numberOfComponents = subforest.ConnectedComponents(components);
        var newVertices = (
            from i in Enumerable.Range(0, numberOfComponents)
            select subforest.Vertices.First(
                vertex => components[vertex] == i)
        ).ToList();
        // todo: save the collapsed subgraph into junction - display all but as one junction.
            
        FibredGraph newGraph = new FibredGraph(true);
        newGraph.AddVertexRange(newVertices);
        var subforestEdges = Enumerable.ToHashSet(subforest.Edges); 
        foreach (var strip in graph.Edges.Except(subforestEdges))
        {
            if (components.TryGetValue(strip.Source, out var component)) // a vertex in the subforest
                strip.Source = newVertices[component];
            if (components.TryGetValue(strip.Target, out component))
                strip.Target = newVertices[component];
            strip.EdgePath = strip.EdgePath.Where(edge => !subforestEdges.Contains(edge.UnderlyingEdge)).ToList();
            newGraph.AddEdge(strip);
        }
    }

    public IEnumerable<Junction> GetValenceOneJunctions() =>
        from vertex in graph.Vertices where vertex.Star().Count() == 1 select vertex;
    public void RemoveValenceOneJunction(Junction junction)
    {
        var star = junction.Star().ToArray();
        if (star.Length != 1) Debug.LogError($"Supposed valence-one junction has valence {star.Length}");
        var removedStrip = star.First();
        Junction otherJunction = removedStrip.Target;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removedStrip.UnderlyingEdge);
        foreach (var strip in graph.Edges) 
            strip.EdgePath = strip.EdgePath.Where(edge => edge.UnderlyingEdge != strip).ToList();
        // todo: isotopy or make the junction bigger to include the removed strip
    }

    public IEnumerable<Junction> GetValenceTwoJunctions() =>
        from vertex in graph.Vertices where vertex.Star().Count() == 2 select vertex;
    public void RemoveValenceTwoJunction(Junction junction, UnorientedStrip removeStrip)
    {
        // todo: select the strip to keep
        var star = junction.Star().ToArray();
        if (star.Length != 2) Debug.LogError($"Supposed valence-two junction has valence {star.Length}");
        OrderedStrip keptStrip = star[0].UnderlyingEdge == removeStrip ? star[1] : star[0];
        Junction enlargedJunction = removeStrip.Source == junction ? removeStrip.Target : removeStrip.Source;
        // two options: a) isotope along removeStrip to move junctions that map there to the "enlargedJunction" (but don't actually enlarge it)
        // and enlarge keptStrip to include the removed strip and the removed junction, reinterpreted as a section of a strip.
        // or: b) include removedStrip and junction into the enlargedJunction and isotope along f(removeStrip), so that it lands in f(enlJun).
        // This isotopy could be faked pretty well! Just display f(keptStrip) as f(removedStrip).Reverse u f(removeJunction) u f(keptStrip)
        // and f(enlJun) as what it was before enlarging.
        // In both cases, the same thing happens combinatorially
        keptStrip.Source = enlargedJunction;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removeStrip);
        
        keptStrip.EdgePath.InsertRange(0, OrderedStrip.ReversedEdgePath(removeStrip.EdgePath));
        foreach (var strip in graph.Edges) 
            strip.EdgePath = strip.EdgePath.Where(edge => edge.UnderlyingEdge != removeStrip).ToList();
        foreach (var vertex in graph.Vertices)
            if (vertex.image == junction)
                vertex.image = enlargedJunction;
    }

    
    /// <summary>
    /// These are the positions where the graph map is not tight, so we can pull tight here.
    /// We have to assume that there are no invariant subforests because the isotopy would touch them!
    /// </summary>
    public IEnumerable<(Strip, EdgePoint[], Junction[])> GetLoosePositions() => 
        from strip in Edges
        let backTracks = GetBackTracks(strip).ToArray()
        let extremalVertices = GetExtremalVertices(strip).ToArray()
        where backTracks.Length != 0 || extremalVertices.Length != 0 
        select (strip, backTracks, extremalVertices);

    IEnumerable<Junction> GetExtremalVertices(Strip edge = null)
    {
        if (edge != null)
            return from vertex in graph.Vertices 
                let star = vertex.Star() 
                // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
                where star.Any() && star.All(strip => Equals(strip.Dg, edge))
                select vertex;
        return from vertex in graph.Vertices
            let star = vertex.Star()
            let firstOutgoingEdge = star.FirstOrDefault()
            // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
            where firstOutgoingEdge != null && star.All(strip => Equals(strip.Dg, firstOutgoingEdge.Dg))
            select vertex;
    }

    IEnumerable<EdgePoint> GetBackTracks(Strip edge = null)
    {
        if (edge != null)
            return from edgePoint in GetBackTracks()
                where Equals(edgePoint.DgAfter(), edge)
                select edgePoint;
        
        return from strip in graph.Edges
            from i in Enumerable.Range(1, strip.EdgePath.Count - 1) 
            // only internal points: Valence-2 extremal vertices are found in parallel anyways.
            let edgePoint = new EdgePoint(strip, i)
            where Equals(edgePoint.DgBefore(), edgePoint.DgAfter())
            select edgePoint;
    }

    public void PullTightExtremalVertex(Junction vertex)
    {
        vertex.image = vertex.Star().First()[0].Image;
        foreach (var strip in vertex.Star())
        {
            strip.EdgePath = strip.EdgePath.Skip(1).ToList();
            // for self-loops, this takes one from both ends.
        }
        // todo: isotopy: Move vertex and shorten the strips
        // todo? update EdgePoints?
    }
    
    public void PullTightBackTrack(EdgePoint backTrack, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        
        var strip = backTrack.edge;
        var i = backTrack.index;
        if (i == -1) PullTightExtremalVertex(strip.Source); 
        strip.EdgePath = strip.EdgePath.Take(i - 1).Concat(strip.EdgePath.Skip(i + 1)).ToList();
        
        for (int k = 0; k < updateEdgePoints.Count; k++)
        {
            var j = updateEdgePoints[k].AlignedIndex(strip, out var reverse);
            if (j < i) continue;
            var res = j == i ? new EdgePoint(strip, j - 1) : new EdgePoint(strip, j - 2);
            updateEdgePoints[k] = reverse ? res.Reversed() : res;
        }
        
        // todo: isotopy to make the strip shorter
    }

    public void FoldEdges(IList<Strip> edges, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        var edgePath = edges[0].EdgePath;
        if (edges.Any(edge => !edge.EdgePath.SequenceEqual(edgePath)))
            Debug.LogError("Edges to fold do not have the same edge path.");
        var targetVerticesToFold = Enumerable.ToHashSet(from edge in edges select edge.Target);
        var newVertex = new Junction(graph, targetVerticesToFold, targetVerticesToFold.First().image); 
        // todo: Connect the vertices by a curve that avoids the rest of the fibred surface
        var newEdge = edges[0]; // not clean code!
        graph.RemoveEdges(from edge in edges.Skip(1) select edge.UnderlyingEdge);
        foreach (var vertex in targetVerticesToFold)
        {
            foreach (var edge in vertex.Star().ToArray()) // to avoid concurrent modification 
                edge.Source = newVertex;
            graph.RemoveVertex(vertex);
        }

        foreach (var edge in edges.Skip(1))
        {
            for (int k = 0; k < updateEdgePoints.Count; k++)
            {
                var j = updateEdgePoints[k].AlignedIndex(edge);
                if (j < 0) continue;
                updateEdgePoints[k] = new EdgePoint(newEdge, j);
            }
        }

        foreach (var strip in graph.Edges)
        {
            strip.EdgePath = strip.EdgePath.Select(
                edge => edges.Contains(edge) ? newEdge : 
                    edges.Contains(edge.Reversed()) ? newEdge.Reversed() : edge
            ).ToList();
        }
        
    }
    
    public (UnorientedStrip, UnorientedStrip) SplitEdge(EdgePoint splitPoint, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        var splitEdge = splitPoint.edge;
        var edgePath = splitEdge.EdgePath;
        var splitTime = splitPoint.GetCurveTimeInJunction();
        
        var newVertex = new Junction(graph, splitEdge.Curve[splitTime], splitPoint.Image);

        var firstSegment = splitEdge.Copy();
        firstSegment.EdgePath = edgePath.Take(splitPoint.index).ToList();
        firstSegment.Target = newVertex;
        firstSegment.Curve = firstSegment.Curve.Restrict(0, splitTime);
        
        var secondSegment = splitEdge.Copy();
        secondSegment.EdgePath = edgePath.Skip(splitPoint.index).ToList();
        secondSegment.Source = newVertex;
        secondSegment.Curve = secondSegment.Curve.Restrict(splitTime, secondSegment.Curve.Length);

        if (splitEdge is OrderedStrip { reverse: true }) { // this should be avoided
            firstSegment.Name = splitEdge.UnderlyingEdge.Name + "-1"; // the first segment from the end; but it is by default reversed; when it is reversed, it looks like D-1. I.E. if we split at D[1], D becomes d-1 d-2 and d becomes D-2 D-1.
            secondSegment.Name = splitEdge.UnderlyingEdge.Name + "-2";
        }
        else {
            firstSegment.Name = splitEdge.Name + "1";
            secondSegment.Name = splitEdge.Name + "2";
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
        { // move the edge point index to the right in preparation for the replacement in the next step
            var edgePoint = updateEdgePoints[k];
            updateEdgePoints[k] = new EdgePoint(edgePoint.edge, 
                edgePoint.index + edgePoint.edge.EdgePath.Take(edgePoint.index).Count(edge => edge.UnderlyingEdge.Equals(splitEdge.UnderlyingEdge)));
        }
        // these three things now already happen in the Strip.set_Target and Strip.set_Source accessors
        // graph.AddVertex(newVertex);
        // graph.AddEdge(firstSegment);
        // graph.AddEdge(secondSegment);
        graph.RemoveEdge(splitEdge.UnderlyingEdge);
        
        foreach (var strip in graph.Edges)
        { // in each edgePath, replace the splitEdge by the two new edges
            strip.EdgePath = strip.EdgePath.SelectMany(
                edge => edge.Equals(splitEdge)
                    ? new() {firstSegment, secondSegment}
                    : edge.Equals(splitEdge.Reversed()) ?
                        new() {secondSegment.Reversed(), firstSegment.Reversed()} 
                        : new List<Strip> {edge}
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
        // var terminalStripSegments = new List<Strip>(strips.Length);
        for (var index = 0; index < strips.Count; index++)
        {
            var splitEdgePoint = updateEdgePoints[l + index];
            var edge = splitEdgePoint.edge; // instead of strips[index]
            var splitIndex = splitEdgePoint.index;
            if (splitIndex == edge.EdgePath.Count) // it is never 0
            {
                initialStripSegments.Add(edge);
                // terminalStripSegments.Add(null);
                continue;
            }
            
            if (splitEdgePoint.edge is OrderedStrip { reverse: true })
            {
                // only split according to the chosen direction (of the "undirected" edge, which in fact is actually very directed, determining the orientation of the graph).
                var (firstSegment, secondSegment) = SplitEdge(splitEdgePoint.Reversed(), updateEdgePoints);
                firstSegment.Name = edge.UnderlyingEdge.Name;
                initialStripSegments.Add(secondSegment.Reversed());
            }
            else
            {
                var (firstSegment, secondSegment) = SplitEdge(splitEdgePoint, updateEdgePoints);
                secondSegment.Name = edge.Name;
                initialStripSegments.Add(firstSegment);
            }

            // terminalStripSegments.Add(secondSegment);
        }

        FoldEdges(initialStripSegments, updateEdgePoints);
    }

    public IEnumerable<Inefficiency> GetInefficiencies()
    {
        var gates = Gate.FindGates(graph);
        foreach (var edge in graph.Edges)
        {
            for (int i = 0; i <= edge.EdgePath.Count; i++)
            {
                var inefficiency = CheckEfficiency(edge[i], gates);
                if (inefficiency != null)
                {
                    if (i==0 || i == edge.EdgePath.Count) Debug.Log("Interpreted a valence-two gate-wise extremal vertex as an inefficiency.");
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
        
        if (!Equals(a.Source, b.Source)) Debug.LogError("The edge path is not an actual edge path.");
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
        
        
        while (p.edgesToFold.Any(edge => 
                Equals(p, new EdgePoint(edge, p.initialSegmentToFold))
        )) p.initialSegmentToFold--;
        
        var updateEdgePoints = new List<EdgePoint>(){ p };

        if (p.initialSegmentToFold == 0)
        {
            // the special case when one of the split points is at the point p
            // Split the edge c = Dg^{p.order}(p.edge) = Dg(p.edgesToFold[*]) at the first edge point.
            // If there is none, split g(c) = Dg(c) at the first edge point. If there is none ...
            var c = p.edgesToFold[0].Dg;
            var edgesToSplit = new List<Strip> { c };
            while(edgesToSplit[0].EdgePath.Count == 1 && edgesToSplit.Count <= 2 * graph.EdgeCount) edgesToSplit.Insert(0, edgesToSplit[0].Dg);
            if (edgesToSplit[0].EdgePath.Count == 1) throw new Exception("Weird: This edge only gets mapped to single edges under g. This shouldn't happen at this stage unless f is efficient and permutes the edges, g is a homeo, the growth is λ=1 and f is periodic. But it isn't efficient because we are in this method.");
            foreach (var edge in edgesToSplit)
            {
                SplitEdge(edge[1], updateEdgePoints);
            }
            
            
            p.initialSegmentToFold = 1;
            // this relies on the EdgePath of these edges having updated during the split of the edge c. 
            // They started with edge.EdgePath = c,..., now they have edge.EdgePath = c1,c2,...
        }
        FoldInitialSegment(p.edgesToFold, p.initialSegmentToFold, updateEdgePoints);
        
        // Now p is an inefficiency of degree one lower.
        
        var pNew = updateEdgePoints[0];
        var newInefficiency = CheckEfficiency(pNew, Gate.FindGates(graph));
        
        if (newInefficiency == null || newInefficiency.order != p.order - 1)
            Debug.LogError($"Bug: The inefficiency was not turned into an efficiency of order one less: {p} was turned into {newInefficiency ?? pNew}");
        if (newInefficiency != null && newInefficiency.order < p.order)    
            RemoveInefficiency(newInefficiency);
    }

    FibredGraph GetMaximalPeripheralSubgraph()
    {
        var Q = new FibredGraph(true);
        Q.AddVerticesAndEdgeRange(peripheralSubgraph.Edges);
        foreach (var strip in graph.Edges)
        {
            if (Q.Edges.Contains(strip)) continue;
            var orbit = OrbitOfEdge(strip);
            if (IsPeripheryFriendlySubforest(orbit, Q, true, true))
                // this means that after adding the orbit to Q, it still deformation retracts onto Q (thus P).
                Q.AddVerticesAndEdgeRange(orbit.Edges);
        }
        return Q;
    }
    

    public bool AbsorbIntoPeriphery(bool testDry = false)
    {
        var Q = GetMaximalPeripheralSubgraph();
        if (testDry && Q.Edges.Any(edge => !peripheralSubgraph.ContainsEdge(edge))) return true;
        
        var starQ = (
            from vertex in Q.Vertices
            let starV = vertex.Star()
            from strip in starV
            where !Q.Edges.Contains(strip.UnderlyingEdge)
            select strip
        ).ToList();
        foreach (var strip in starQ)
        {
            while (Q.Edges.Contains(strip.Dg?.UnderlyingEdge))
            {
                if (testDry) return true;
                strip.EdgePath.RemoveAt(0);
            }
            if (strip.EdgePath.Count == 0) Debug.LogError("The strip has been absorbed into the periphery.");
        }
        if (testDry) return peripheralSubgraph.Vertices.Any(v => v.Star().Count() <= 2); 
            // the periphery is maximal, nothing to do.
            // todo? check also when !testDry and return here with only removing valence-two vertices.

        var components = new Dictionary<Junction, int>();
        Q.ConnectedComponents(components);
        var gates = Gate.FindGates(Q, vertex => components[vertex]);
        var newJunctions = new List<Junction>();
        foreach (var gate in gates)
        {
            var frontiers = from edge in gate.Edges select edge.Curve.Restrict(0, 0.1f);
            // todo: give each gate γ the fr(γ) from joining the fr(e) for e in the gate along the boundary of <Q>.
            var newJunction = new Junction(graph, frontiers, null);
            newJunctions.Add(newJunction);
            foreach (var edge in gate.Edges)
                edge.Source = newJunction;
        }

        graph.AddVertexRange(newJunctions);

        for (var i = 0; i < gates.Count; i++)
        {
            Gate<int> gate = gates[i];
            var Dg = gate.Edges.First().Dg;
            var Dg_gate = Enumerable.Range(0, gates.Count).First(j => gates[j].Edges.Contains(Dg));
            newJunctions[i].image = newJunctions[Dg_gate];
            // todo: new edges between the new junctions; map them accordingly (uniquely defined by the images of the new junctions: g still acts as a graph automorphism on the new P). Needs the order of the gates! (we know that they are connected in the cyclic order)
        }

        foreach (var edge in starQ)
        {
            var gateId = Enumerable.Range(0, gates.Count).First(j => gates[j].Edges.Contains(edge));
            edge.Source = newJunctions[gateId];
        }

        graph.RemoveEdges(Q.Edges);
        foreach (var oldJunction in Q.Vertices)
            graph.RemoveVertex(oldJunction); // todo: check if the implementation realized that no edges are left at this junction.
        return true;
    }
    
    public List<HashSet<UnorientedStrip>> prePeripheralDegree()
    {
        var degrees = new List<HashSet<UnorientedStrip>>();
        degrees.Add(peripheralSubgraph.Edges.ToHashSet());
        var remainingEdges = graph.Edges.ToHashSet();
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

    public IEnumerable<List<Strip>> GetPeripheralInefficiencies()
    {
        var prePeripheralDegrees = prePeripheralDegree();
        var prePeriphery = new HashSet<UnorientedStrip>();
        foreach (var degree in prePeripheralDegrees)
            prePeriphery.UnionWith(degree);
        foreach (var edge in Edges)
        {
            if (!prePeriphery.Contains(edge.Dg)) 
                continue;
            var otherEdges = edge.Source.Star().Where(
                e => Equals(e.Dg, edge.Dg) ).Cast<Strip>().ToList();
            if (otherEdges.Count > 1)
                yield return otherEdges;
        }
    }

    public void RemovePeripheralInefficiency(List<Strip> foldableEdges)
    {
        FoldInitialSegment(foldableEdges);
    }

}
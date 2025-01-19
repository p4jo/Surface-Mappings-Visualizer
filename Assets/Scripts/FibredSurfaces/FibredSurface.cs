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
    public FibredGraph graph;
    /// <summary>
    /// The peripheral subgraph is assumed to have only valence-two vertices (that have higher valence in the main graph).
    /// We also assume that g acts on it as a graph automorphism.
    /// </summary>
    public FibredGraph peripheralSubgraph;
    
    public List<Gate> gates;
    public IEnumerable<ITransformable> Patches => graph.Vertices.Concat<ITransformable>(from edge in graph.Edges select edge.Curve);
    public IEnumerable<Strip> Edges => graph.Edges.Concat<Strip>(graph.Edges.Select(edge => edge.Reversed()));

    public FibredSurface(FibredGraph graph, FibredGraph peripheralSubgraph)
    {
        this.graph = graph;
        this.peripheralSubgraph = peripheralSubgraph;
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
        
        newGraph.AddVerticesAndEdgeRange(newEdges.Values);
        var newPeripheralSubgraph = new FibredGraph(true);
        newPeripheralSubgraph.AddVerticesAndEdgeRange(
            from edge in peripheralSubgraph.Edges select newEdges[edge]
        );
        
        return new FibredSurface(newGraph, newPeripheralSubgraph);
    }

    public BHDisplayOptions NextSuggestion()
    {
        // Iterate through the steps and give the possible steps to the user
        var a = GetInvariantSubforests();
        if (a.Count > 0) return new BHDisplayOptions()
        {
            options = a,
            description = "Collapse an invariant subforest.",
            buttons = new[] {"Collapse"}
        };
        return null;
    }
    
    public void ApplySuggestion(IEnumerable<object> suggestion, object button)
    { // todo
        
        switch (button)
        {
            case "Collapse":
                CollapseInvariantSubforest((FibredGraph) suggestion);
                break;
            
        }
        if (suggestion is FibredGraph subforest)
        {
            CollapseInvariantSubforest(subforest);
        }
    }

    public class BHDisplayOptions
    {
        public IEnumerable<object> options;
        public string description;
        public IEnumerable<object> buttons;
    }

    private static List<UnorientedStrip> OrbitOfEdge(Strip edge)
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

        return orbit;
    }

    /// <summary>
    /// only subforests whose collapse doesn't destroy the peripheral subgraph, i.e. whose components contain at most one vertex of the peripheral subgraph.
    /// 
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<FibredGraph> GetInvariantSubforests()
    { 
        Dictionary<UnorientedStrip, FibredGraph> subforests = new();
        
        foreach (var strip in graph.Edges)
        {
            if (subforests.Values.Any(subforest => subforest.Edges.Contains(strip))) continue;
            // there already is a larger subforest containing the one defined from this strip.

            List<UnorientedStrip> orbitOfEdge = OrbitOfEdge(strip);
            
            FibredGraph newGraph = new FibredGraph();
            newGraph.AddVerticesAndEdgeRange(orbitOfEdge);
            // actually, components that are only vertices are missing, but that doesn't matter because we only use this in the CollapseInvariantSubforest method.
            if (!newGraph.IsUndirectedAcyclicGraph()) continue;

            var components = new Dictionary<Junction, int>();
            int numberOfComponents = newGraph.ConnectedComponents(components);
            int[] componentIntersections = new int[numberOfComponents];
            foreach (var vertex in peripheralSubgraph.Vertices)
            {
                if (components.TryGetValue(vertex, out var comp))
                    componentIntersections[comp]++;
                if (componentIntersections[comp] > 1) break;   
            } 
            if (componentIntersections.Any(i => i > 1)) continue; 
            // one component of the subforest contains more than one vertex of the peripheral subgraph. Collapsing it would destroy the peripheral subgraph.
            
                        
            foreach (var oldEdge in subforests.Keys.ToArray())
            {
                if (orbitOfEdge.Contains(oldEdge)) subforests.Remove(oldEdge); // the new subforest contains the old one.
            }
            subforests[strip] = newGraph;
        }
        // todo? See if a union of two subforests is a subforest. If so, we can merge them.
        return subforests.Values;
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
        var star = junction.Star().ToArray();
        if (star.Length != 2) Debug.LogError($"Supposed valence-two junction has valence {star.Length}");
        OrderedStrip keptStrip = star[0].UnderlyingEdge == removeStrip ? star[1] : star[0];
        Junction enlargedJunction = removeStrip.Source == junction ? removeStrip.Target : removeStrip.Source;
        // todo: include removedStrip and junction into the enlargedJunction
        keptStrip.Source = enlargedJunction;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removeStrip);
        foreach (var strip in graph.Edges) 
            strip.EdgePath = strip.EdgePath.Where(edge => edge.UnderlyingEdge != removeStrip).ToList();
    }


    public IEnumerable<Junction> GetExtremalVertices() => 
        from vertex in graph.Vertices 
        let star = vertex.Star() 
        let firstOutgoingEdge = star.FirstOrDefault()?.EdgePath.FirstOrDefault() 
        where star.All(strip => Equals(strip.EdgePath.FirstOrDefault(), firstOutgoingEdge))
        select vertex;

    public IEnumerable<EdgePoint> GetBackTracks() =>
        from strip in graph.Edges
        from i in Enumerable.Range(0, strip.EdgePath.Count - 1)
        where Equals(strip.EdgePath[i], strip.EdgePath[i + 1].Reversed())
        select new EdgePoint(strip, i);

    public void PullTightExtremalVertex(Junction vertex, Strip firstOutgoingEdge)
    {
        vertex.image = firstOutgoingEdge.Target;
        foreach (var strip in vertex.Star())
        {
            strip.EdgePath = strip.EdgePath.Skip(1).ToList();
            // for self-loops, this takes one from both ends.
        }
        // todo: isotopy: Move vertex and shorten the strips
    }
    
    public void PullTightBackTrack(Strip strip, int i)
    {
        strip.EdgePath = strip.EdgePath.Take(i).Concat(strip.EdgePath.Skip(i + 2)).ToList();
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
            foreach (var edge in vertex.Star()) 
                edge.Source = newVertex;
            graph.RemoveVertex(vertex);
        }

        foreach (var edge in edges.Skip(1))
        {
            for (int k = 0; k < updateEdgePoints.Count; k++)
            {
                var j = updateEdgePoints[k].IndexDirectionFixed(edge);
                if (j < 0) continue;
                updateEdgePoints[k] = new EdgePoint(newEdge, j);
            }
        }
        
    }
    
    public (UnorientedStrip, UnorientedStrip) SplitEdge(EdgePoint splitPoint, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        var splitEdge = splitPoint.edge;
        var i = splitPoint.i;
        var edgePath = splitEdge.EdgePath;
        var splitTime = splitPoint.GetCurveTimeInJunction();
        
        var newVertex = new Junction(graph, splitEdge.Curve[splitTime], splitPoint.Image);

        var firstSegment = splitEdge.Copy();
        firstSegment.EdgePath = edgePath.Take(i).ToList();
        firstSegment.Target = newVertex;
        firstSegment.Curve = firstSegment.Curve.Restrict(0, splitTime);
        
        var secondSegment = splitEdge.Copy();
        secondSegment.EdgePath = edgePath.Skip(i).ToList();
        secondSegment.Source = newVertex;
        secondSegment.Curve = secondSegment.Curve.Restrict(splitTime, secondSegment.Curve.Length);
        
        graph.AddVertex(newVertex);
        graph.AddEdge(firstSegment);
        graph.AddEdge(secondSegment);
        graph.RemoveEdge(splitEdge.UnderlyingEdge);

        foreach (var strip in graph.Edges)
        {
            strip.EdgePath = strip.EdgePath.SelectMany(
                edge => edge.Equals(splitEdge)
                    ? new() {firstSegment, secondSegment}
                    : edge.Equals(splitEdge.Reversed()) ?
                        new() {secondSegment.Reversed(), firstSegment.Reversed()} 
                        : new List<Strip> {edge}
            ).ToList();
        }
        
        for (var k = 0; k < updateEdgePoints.Count; k++)
        {
            var j = updateEdgePoints[k].IndexDirectionFixed(splitEdge);
            if (j < 0) continue;
            if (j < i) updateEdgePoints[k] = new EdgePoint(firstSegment, j);
            else updateEdgePoints[k] = new EdgePoint(secondSegment, j - i);
        }

        return (firstSegment, secondSegment);
    }

    public void FoldInitialSegment(IList<Strip> strips, int? i = null, IList<EdgePoint> updateEdgePoints = null)
    {
        i ??= SharedInitialSegment(strips);
        updateEdgePoints ??= new List<EdgePoint>();

        var initialStripSegments = new List<Strip>(strips.Count);
        // var terminalStripSegments = new List<Strip>(strips.Length);
        foreach (var edge in strips)
        {
            if (edge.EdgePath.Count == i)
            {
                initialStripSegments.Add(edge);
                // terminalStripSegments.Add(null);
                continue;
            }

            var (firstSegment, secondSegment) = SplitEdge(new EdgePoint(edge, i.Value), updateEdgePoints);
            
            initialStripSegments.Add(firstSegment);
            // terminalStripSegments.Add(secondSegment);
        }
        FoldEdges(initialStripSegments, updateEdgePoints);
    }

    private static int SharedInitialSegment(IList<Strip> strips)
    {
        IEnumerable<Strip> initialSegment = strips[0].EdgePath;
        foreach (var strip in strips.Skip(1)) 
            initialSegment = initialSegment.Zip(strip.EdgePath, (a, b) => Equals(a, b) ? a : null);

        int i = initialSegment.TakeWhile(strip => strip != null).Count();
        if (i == 0) Debug.LogError("The edges do not have a common initial segment.");
        return i;
    }

    public IEnumerable<Inefficiency> GetInefficiencies()
    {
        gates = Gate.FindGates(graph);
        foreach (var edge in graph.Edges)
        {
            for (int i = 0; i < edge.EdgePath.Count - 1; i++)
            {
                var inefficiency = CheckEfficiency(edge, i);
                if (inefficiency != null) yield return inefficiency;
            }
        }
        
    }

    [CanBeNull]
    public Inefficiency CheckEfficiency(Strip edge, int i)
    {
        Strip a = edge.EdgePath[i].Reversed();
        Strip b = edge.EdgePath[i + 1];
        if (a.Source != b.Source) Debug.LogError("The edge path is not an actual edge path.");
        if (!FindGate(a).Edges.Contains(b)) return null;
        // Gatewise backtracking detected.
        Strip aOld = a;
        for (int j = 0; j <= 2 * graph.EdgeCount; j++)
        {
            if (!Equals(a, b))
            {
                aOld = a;
                a = a!.Dg;
                b = b!.Dg;
                continue;
            }

            List<Strip> edgesToFold = (from e in aOld!.Source.Star() where Equals(e.Dg, a) select e).ToList<Strip>();
            return new Inefficiency(edge, i, j, edgesToFold, SharedInitialSegment(edgesToFold));
        }
        throw new Exception("Bug: Two edges in the same gate didn't eventually get mapped to the same edge under Dg.");
        
        Gate FindGate(Strip edge) => gates.First(gate => gate.Edges.Contains(edge));     
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
        // todo Caution: It also might be a valence-two junction now
        
    }

}
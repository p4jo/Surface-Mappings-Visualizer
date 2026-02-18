using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph.Algorithms;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public partial class FibredSurface
{
    /// <summary>
    /// only subforests whose collapse doesn't destroy the peripheral subgraph, i.e. whose components contain at most one vertex of the peripheral subgraph.
    /// components that are only vertices are missing!
    /// (but that doesn't matter because we only use this in the CollapseSubforest and MaximalPeripheralGraph methods)
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

    public AlgorithmSuggestion InvariantSubforestsSuggestion()
    {
        var a = GetInvariantSubforests();
        if (a.Count == 0) return null;
        return new AlgorithmSuggestion(
            options: from subforest in a
            select (
                subforest.Edges.Select(e => e.Name) as object,
                string.Join(", ", subforest.Edges.Select(e => e.ColorfulName))
            ), 
            description: "Collapse an invariant subforest.", 
            buttons: new[] { AlgorithmSuggestion.subforestAtOnceButton, AlgorithmSuggestion.subforestInStepsButton }
        );
    }

    /// <summary>
    /// Use PreservedSubgraph_Reducibility instead
    /// </summary>
    /// <returns></returns>
    public HashSet<UnorientedStrip> InvariantSubgraph() => 
        Strips.Select(edge => OrbitOfEdge(edge)).FirstOrDefault(
            orbit => orbit.Count < graph.EdgeCount
        );

    /// <summary>
    /// This checks if the graph is a subforest at each component touches the peripheral subgraph in at most one vertex.
    /// Thus if touching and remove are true, this checks if subgraph deformation retracts to the peripheral subgraph.
    /// </summary>
    /// <param name="subgraph"></param>
    /// <param name="peripheralSubgraph">If not present, the saved peripheral subgraph P is taken.</param>
    /// <param name="remove">If remove is true, subgraph is replaced by (subgraph \ peripheralSubgraph)</param>
    /// <param name="touching">If touching is true, each component must touch the peripheral subgraph in exactly one vertex.</param>
    /// <returns></returns>
    bool IsPeripheryFriendlySubforest(FibredGraph subgraph, HashSet<UnorientedStrip> peripheralSubgraph = null, bool remove = false,
        bool touching = false)
    {
        if (remove)
            return IsPeripheryFriendlySubforest(subgraph.Edges, peripheralSubgraph, false, touching);

        if (!subgraph.IsUndirectedAcyclicGraph()) return false;

        var components = new Dictionary<Junction, int>();
        int numberOfComponents = subgraph.ConnectedComponents(components);
        int[] componentIntersections = new int[numberOfComponents];

        peripheralSubgraph ??= this.peripheralSubgraph;
        foreach (var vertex in peripheralSubgraphVertices)
        {
            if (components.TryGetValue(vertex, out var comp))
                componentIntersections[comp]++;
            if (componentIntersections[comp] > 1) return false;
            // one component of the subforest contains more than one vertex of the peripheral subgraph. Collapsing it would destroy the peripheral subgraph.
            // this is what we call "not periphery-friendly"
        }

        return !touching || componentIntersections.All(i => i == 1);
    }

    bool IsPeripheryFriendlySubforest(IEnumerable<UnorientedStrip> edges, HashSet<UnorientedStrip> peripheralSubgraph = null,
        bool remove = false, bool touching = false)
    {
        peripheralSubgraph ??= this.peripheralSubgraph;

        if (remove)
            return IsPeripheryFriendlySubforest(edges.Except(peripheralSubgraph), peripheralSubgraph, false,
                touching);

        FibredGraph subgraph = new FibredGraph();
        subgraph.AddVerticesAndEdgeRange(edges);
        return IsPeripheryFriendlySubforest(subgraph, peripheralSubgraph, remove, touching);
    }

    public IEnumerator<AlgorithmSuggestion> CollapseSubforest(IEnumerable<string> edges)
    {
        var subforest = new FibredGraph(true);
        var edgeDict = OrientedEdges.ToDictionary(e => e.Name);
        subforest.AddVerticesAndEdgeRange(
            from edgeName in edges
            select edgeDict[edgeName].UnderlyingEdge
        );
        return CollapseSubforest(subforest);
    }

    public IEnumerator<AlgorithmSuggestion> CollapseSubforest(FibredGraph subforest)
    {
        var subforestEdges = subforest.Edges.ToHashSet();

        var componentList = subforest.ComponentGraphs(out var componentDict);

        var newVertices = new List<Junction>();
        foreach (var component in componentList)
        {
            // Direct Union of the edges and vertices of the component to a vertex that is large (using the "drawables")
            // var newVertex = new Junction(
            //     graph,
            //     drawables: component.Edges.Select(e => e.Curve).Concat<IDrawnsformable>(component.Vertices),
            //     // yes this is component.Patches but component is a FibredGraph, not FibredSurface... 
            //     name: NextVertexName(),
            //     color: NextVertexColor()
            // );
            if (component.EdgeCount == 0)
                continue; // nothing to collapse
            yield return new AlgorithmSuggestion(
                options: from v in component.Vertices.OrderByDescending(
                        v => graph.AdjacentDegree(v)
                    )
                    select (v.Name as object, v.ColorfulName),
                description: $"Pull component {{{component.Edges.ToCommaSeparatedString(e => e.ColorfulName)}}} towards vertex",
                buttons: new[] { AlgorithmSuggestion.collapseInvariantSubforestContinueButton }    
            ); 
            // this pauses execution and asks the user by giving an algorithm suggestion (unless they chose to skip these in-between steps)
            var selectedOption = selectedOptionsDuringAlgorithmPause?.FirstOrDefault();
            Junction newVertex = null;
            if (selectedOption is string selectedVertexName)
                newVertex = component.Vertices.FirstOrDefault(v => v.Name == selectedVertexName);
            newVertex ??= component.Vertices.ArgMax(v => graph.AdjacentDegree(v)).Item1;
            
            newVertices.Add(newVertex);
            
            // ReplaceVertices(SubgraphStarOrdered(component), newVertex);

            if (!component.IsUndirectedAcyclicGraph())
                // would probably give a terrible infinite loop in PullTowardsCenter
                throw new InvalidOperationException(
                    "The subforest we collapse is not actually a subforest, the following component contains a loop: " + component.Edges.ToCommaSeparatedString(e => e.ColorfulName));

            PullTowardsCenter(newVertex, null, 1f);
            
            foreach (var junction in component.Vertices)
            {
                if (junction != newVertex)
                    graph.RemoveVertex(junction); 
                // this also removes the edges of component (because they have both ends in component.Vertices, and not both are newVertex) 
            }

            continue;

            // Moves the source of the strip along the strip (removing it) and returns the cyclic order. This is done recursively, edge for edge, so that the strips are parallel but not at the same place.
            IEnumerable<Strip> PullTowardsCenter(Junction junction, Strip stripToCenter, float orderIndexWidth)
            {
                junction ??= stripToCenter.Source;
                var star = StarOrdered(junction, stripToCenter, removeFirstEdgeIfProvided: true).ToList();
                var newStar = new List<Strip>(4 * star.Count);
                
                for (int i = 0; i < star.Count; i++)
                {
                    var edgeToChild = star[i];
                    if (component.Edges.Contains(edgeToChild.UnderlyingEdge))
                    {
                        var orderIndexWidthChild = star[(i + 1) % star.Count].OrderIndexStart - edgeToChild.OrderIndexStart;
                        if (orderIndexWidthChild < 0) // edgeToChild has the highest OrderIndexStart
                            orderIndexWidthChild = 2f;
                        newStar.AddRange(PullTowardsCenter(null, edgeToChild.Reversed(), orderIndexWidthChild));
                    }
                    else
                        newStar.Add(edgeToChild);
                }
                
                if (!newStar.SequenceEqual(StarOrdered(junction, stripToCenter, removeFirstEdgeIfProvided: true)))
                    throw new Exception("The cyclic order or origin map weren't set correctly in PullTowardsCenter. " +
                                        $"The new star {string.Join(", ", StarOrdered(junction, stripToCenter, removeFirstEdgeIfProvided: true).Select(e => e.Name))} is not equal to the expected list of edges {string.Join(", ", newStar.Select(e => e.Name))}.");

                if (stripToCenter == null)
                    return newStar;
                
                foreach (var prolongedStrip in newStar)
                {
                    prolongedStrip.EdgePath = stripToCenter.EdgePath.Inverse.Concat(prolongedStrip.EdgePath);          
                }
                
                MoveJunction(stripToCenter);
                // this changes all the curves in newStar = StarOrdered(junction, stripToCenter).Skip(1).ToList(),
                // prolonging them at the start by stripToCenter.Reversed().Curve
                // the cyclic order needs to be correct
                // we then have to set the source and order index:

                var scale = orderIndexWidth / newStar.Count;
                foreach (var (index, edge) in newStar.Enumerate())
                {
                    edge.Source = stripToCenter.Target;
                    edge.OrderIndexStart = stripToCenter.OrderIndexEnd + scale * index; 
                    // we needed the orderIndexWidth, so that the edge.OrderIndexStart is in [stripFromCenter.OrderIndexStart, stripFromCenter.NextInCyclicOrder.OrderIndexStart).
                }
                graph.RemoveEdge(stripToCenter.UnderlyingEdge);
                return newStar;
            }
        }

        foreach (var strip in Strips)
        {
            strip.EdgePath = strip.EdgePath.Replace(edge => subforestEdges.Contains(edge.UnderlyingEdge) ? null : edge);
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
}
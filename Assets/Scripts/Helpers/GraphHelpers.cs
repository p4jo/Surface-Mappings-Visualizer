using System.Collections.Generic;
using QuikGraph;
using QuikGraph.Algorithms;

public static class GraphHelpers
{
    public static (TVertex, TVertex, TTag) Deconstruct<TVertex, TTag>(this TaggedEdge<TVertex, TTag> edge) =>
        (edge.Source, edge.Target, edge.Tag);

    public static List<UndirectedGraph<TVertex, TEdge>> ComponentGraphs<TVertex, TEdge>(
        this UndirectedGraph<TVertex, TEdge> subforest, out Dictionary<TVertex, int> components) where TEdge : IEdge<TVertex>
    {
        components = new(subforest.VertexCount);
        var numberOfComponents = subforest.ConnectedComponents(components);
        var componentList = new List<UndirectedGraph<TVertex, TEdge>>(numberOfComponents);
        for (var i = 0; i < numberOfComponents; i++)
            componentList.Add(new UndirectedGraph<TVertex, TEdge>());
        foreach (var vertex in components.Keys) 
            componentList[components[vertex]].AddVertex(vertex);
        foreach (var strip in subforest.Edges)
            componentList[components[strip.Source]].AddEdge(strip);
        return componentList;
    }
    
    public static List<UndirectedGraph<TVertex, TEdge>> ComponentGraphs<TVertex, TEdge>(
        this UndirectedGraph<TVertex, TEdge> subforest) where TEdge : IEdge<TVertex> =>
        ComponentGraphs(subforest, out _);
}

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
        components = new();
        int numberOfComponents = subforest.ConnectedComponents(components);
        var componentList = new List<UndirectedGraph<TVertex, TEdge>>(numberOfComponents);
        for (var i = 0; i < numberOfComponents; i++)
            componentList.Add(new());
        foreach (var strip in subforest.Edges)
            componentList[components[strip.Source]].AddVerticesAndEdge(strip);
        return componentList;
    }
    
    public static List<UndirectedGraph<TVertex, TEdge>> ComponentGraphs<TVertex, TEdge>(
        this UndirectedGraph<TVertex, TEdge> subforest) where TEdge : IEdge<TVertex> =>
        ComponentGraphs(subforest, out _);
}

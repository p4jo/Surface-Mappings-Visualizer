using QuikGraph;

public static class GraphHelpers
{
    public static (VertexType, VertexType, TagType) Deconstruct<VertexType, TagType>(this TaggedEdge<VertexType, TagType> edge) =>
        (edge.Source, edge.Target, edge.Tag);
}

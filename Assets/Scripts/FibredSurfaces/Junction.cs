using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public class Junction: IPatchedTransformable
{
    // todo? add the ribbon graph structure, i.e. make sure that the cyclic ordering is clear? So far not needed.
    readonly FibredGraph graph;
    
    public IEnumerable<ITransformable> Patches { get; set; }
    // todo: save and display embedded disk and actually a collection of disks and strips.


    /// <summary>
    /// f maps this junction into the image junction, i.e. g(this) = image;
    /// </summary>
    public Junction image;

    private static int _lastId = 0;
    private readonly int id = _lastId++;
    
    public Junction(FibredGraph graph, IEnumerable<ITransformable> drawables, Junction image)
    {
        this.graph = graph;
        Patches = drawables;
        this.image = image;
    }
    public Junction(FibredGraph graph, ITransformable drawable, Junction image) : this(graph, new[] {drawable}, image)
    { }
    
    public Junction Copy(FibredGraph graph) => new(graph, Patches, image);

    public IEnumerable<OrderedStrip> Star()
    {
        return graph.AdjacentEdges(this).Select(
            strip => new OrderedStrip(strip, strip.Source != this)
        ).Concat(
            from strip in graph.AdjacentEdges(this)
            where strip.IsSelfEdge()
            select new OrderedStrip(strip, true)
        );
    }

    public override string ToString() => "Junction " + id;
}
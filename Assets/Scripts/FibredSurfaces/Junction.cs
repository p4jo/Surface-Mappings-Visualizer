using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public class Junction: IPatchedTransformable, IEquatable<Junction>
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
    public string Name { get; set; }
    
    public Junction(FibredGraph graph, IEnumerable<ITransformable> drawables, string name, Junction image = null)
    {
        this.graph = graph;
        Patches = drawables;
        this.image = image;
        Name = "v" + id;
    }
    public Junction(FibredGraph graph, ITransformable drawable, string name = null, Junction image = null) : this(graph, new[] {drawable}, name, image)
    { }
    
    public Junction Copy(FibredGraph graph, string name = null) => new(graph, Patches.ToArray(), name ?? Name, image);

    public IEnumerable<OrderedStrip> Star(FibredGraph graph = null)
    {
        graph ??= this.graph;
        return graph.AdjacentEdges(this).Select(
            strip => new OrderedStrip(strip, strip.Source != this)
        ).Concat(
            from strip in graph.AdjacentEdges(this)
            where strip.IsSelfEdge()
            select new OrderedStrip(strip, true)
        );
    }

    public override string ToString() => Name;

    public bool Equals(Junction other) => id == other?.id;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Junction)obj);
    }

    public override int GetHashCode() => id;
}
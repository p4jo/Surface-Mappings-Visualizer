
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using QuikGraph;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;


public abstract class Strip: IEdge<Junction>, IDrawable
{
    public readonly FibredSurface fibredSurface;
    public FibredGraph graph => fibredSurface.graph;
    
    protected Strip(FibredSurface fibredSurface)
    {
        this.fibredSurface = fibredSurface;
    }
    
    public abstract Curve Curve { get; set; }

    /// <summary>
    /// f maps this strip into the fibred Surface, and it traces out the edgePath g(this) = edgePath;
    /// </summary>
    public abstract EdgePath EdgePath { get; set; }
    public abstract float OrderIndexStart { get; set; }
    public abstract float OrderIndexEnd { get; set; }
    
    [CanBeNull] public virtual Strip Dg => EdgePath.FirstOrDefault();


    public abstract Junction Target { get; set; }
    public abstract Junction Source { get; set; }
    
    public abstract UnorientedStrip UnderlyingEdge { get; }
    public abstract string Name { get; set; }
    public abstract Color Color { get; set; }
    IDrawable IDrawable.Copy() => Copy();


    public virtual Junction OtherVertex(Junction vertex) => vertex == Source ? Target : Source;
    
    public abstract Strip Reversed();

    public override bool Equals(object other) =>
        other switch
        {
            OrderedStrip orderedStrip => orderedStrip.Equals(this),
            UnorientedStrip otherStrip => ReferenceEquals(this, otherStrip),
                // Source == otherStrip.Source && Target == otherStrip.Target &&
                // Curve.Equals(otherStrip.Curve) && EdgePath.SequenceEqual(otherStrip.EdgePath),
            //  this actually *should* be ReferenceEquals, shouldn't it? Parallel edges might be considered equal. (well no, because of the saved curve). However, the "UnorderedStrip"s should not be doubled. ALSO, THIS CAUSES A STACKOVERFLOW (self-reference in SequenceEqual)
            _ => false
        };
    

    public EdgePoint this[int i] => new(this, i);

    public override string ToString() => $"{Name}: {Source} -> {Target} with g({Name}) = {EdgePath.ToString(250, 10)}";
    
    public string ToColorfulString()
    {
        return $"{ColorfulName(this)}: {ColorfulName(Source)} -> {ColorfulName(Target)} with g({ColorfulName(this)}) = {EdgePath.ToColorfulString(300, 15)}";
        
        string ColorfulName(IDrawable obj) => obj.ColorfulName; 
        // Yep, we cannot call Curve.ColorfulName because, why? It implements IDrawnsformable, thus IDrawable, but interface members can only be called if the variable type is that interface.
    }

    public static int SharedInitialSegment(IList<Strip> strips)
    {
        IEnumerable<Strip> initialSegment = strips[0].EdgePath;
        foreach (var strip in strips.Skip(1)) 
            initialSegment = initialSegment.Zip(strip.EdgePath, (a, b) => Equals(a, b) ? a : null);

        int i = initialSegment.TakeWhile(strip => strip != null).Count();
        if (i == 0) Debug.LogError("The edges do not have a common initial segment.");
        return i;

    }

    public virtual UnorientedStrip CopyUnoriented(FibredSurface fibredSurface = null, Curve curve = null, Junction source = null,
        Junction target = null, EdgePath edgePath = null, string name = null, float? orderIndexStart = null, float? orderIndexEnd = null)
    {
        var res = new UnorientedStrip(curve ?? Curve.Copy(), source ?? Source, target ?? Target, edgePath ?? EdgePath,
            fibredSurface ?? this.fibredSurface, orderIndexStart ?? this.OrderIndexStart, orderIndexEnd ?? this.OrderIndexEnd);
        if (name != null) res.Name = name;
        return res;
    }
    
    public abstract Strip Copy(FibredSurface fibredSurface = null, Curve curve = null, Junction source = null,
        Junction target = null, EdgePath edgePath = null, string name = null, float? orderIndexStart = null, float? orderIndexEnd = null);
}

public class UnorientedStrip : Strip
{
    public override Curve Curve { get; set; }
    public override EdgePath EdgePath { get; set; }
    public override UnorientedStrip UnderlyingEdge => this;

    public override float OrderIndexEnd { get; set; }
    public override float OrderIndexStart { get; set; }

    public UnorientedStrip(Curve curve, Junction source, Junction target, EdgePath edgePath,
        FibredSurface fibredSurface, float orderIndexStart, float orderIndexEnd, bool newColor = false, bool newName = false, bool addToGraph = false) : base(fibredSurface)
    {
        Curve = curve;
        this.source = source;
        this.target = target;
        EdgePath = edgePath;
        this.OrderIndexEnd = orderIndexEnd;
        this.OrderIndexStart = orderIndexStart;
        if (newColor)
            Color = fibredSurface.NextEdgeColor();
        if (newName)
            Name = fibredSurface.NextEdgeName();
        if (addToGraph)
            fibredSurface.graph.AddVerticesAndEdge(this);
    }

    private Junction target;
    public override Junction Target
    {
        get => target;
        set {
            graph.RemoveEdge(UnderlyingEdge);
            target = value;
            graph.AddVerticesAndEdge(UnderlyingEdge);
        }
    }

    private Junction source;
    public override Junction Source
    {
        get => source;
        set { 
            graph.RemoveEdge(UnderlyingEdge);
            source = value;
            graph.AddVerticesAndEdge(UnderlyingEdge);
        }
    }
    
    public override string Name
    {
        get => Curve.Name;
        set {
            if (value.Any(char.IsUpper)) Debug.LogError("An unoriented strip should have a lowercase name!");
            Curve.Name = value.ToLower();
            if (reversed != null) reversed.Curve.Name = value + "'";
        }
    }

    public override Color Color
    {
        get => Curve.Color;
        set {
            Curve.Color = value;
            if (reversed != null) reversed.Curve.Color = value;
        }
    }

    private OrderedStrip reversed;
    public override Strip Reversed() => reversed ??= new OrderedStrip(this, true);

    public override Strip Copy(FibredSurface fibredSurface = null, Curve curve = null, Junction source = null,
        Junction target = null, EdgePath edgePath = null, string name = null, float? orderIndexStart = null, float? orderIndexEnd = null)
        => CopyUnoriented(fibredSurface, curve, source, target, edgePath, name, orderIndexStart, orderIndexEnd);

    /// <summary>
    /// DO THIS ONLY BEFORE USING THE FIBRED SURFACE!
    /// This does not update any edge paths!
    /// </summary>
    public void ReplaceWithInverseEdge()
    {
        var name = Name;
        Curve = Curve.Reversed();
        Name = name;
        (source, target) = (target, source);
        (OrderIndexStart, OrderIndexEnd) = (OrderIndexEnd, OrderIndexStart);
    }
}

public class OrderedStrip: Strip
{
    public override Curve Curve
    {
        get => reverse ? UnderlyingEdge.Curve.Reversed() : UnderlyingEdge.Curve;
        set
        {
            var underlyingCurve = reverse ? value.Reversed() : value; 
            if (underlyingCurve.Name.EndsWith("'")) 
                underlyingCurve.Name = underlyingCurve.Name[..^1];
            UnderlyingEdge.Curve = underlyingCurve;
        }
    }

    public override UnorientedStrip UnderlyingEdge { get; }
    public override string Name
    {
        get => reverse ? UnderlyingEdge.Name.ToUpper() : UnderlyingEdge.Name;
        set
        {
            if (reverse && value.Any(char.IsLower)) Debug.LogError("A reverse strip should have an uppercase Name!");
            UnderlyingEdge.Name = value.ToLower();
        }
    }

    public override Color Color
    {
        get => UnderlyingEdge.Color;
        set => UnderlyingEdge.Color = value;
    }

    // "<s>" + UnderlyingEdge.Name + "</s>"   (strikethrough in RichText; workaround for overbar; use for fancy display?)
    // For the moment, we just use the name of the underlying edge in UPPERCASE.
    
    /// <summary>
    /// actually this should never be false.
    /// </summary>
    public readonly bool reverse; 

    public OrderedStrip(UnorientedStrip underlyingEdge, bool reverse): base(underlyingEdge.fibredSurface)
    {
        this.UnderlyingEdge = underlyingEdge;
        this.reverse = reverse;
    }

    public override Junction Source
    {
        get => reverse ? UnderlyingEdge.Target : UnderlyingEdge.Source;
        set
        {
            if (reverse) UnderlyingEdge.Target = value;
            else UnderlyingEdge.Source = value;
        }
    }

    public override Junction Target
    {
        get => reverse ? UnderlyingEdge.Source : UnderlyingEdge.Target;
        set {
            if (reverse) UnderlyingEdge.Source = value;
            else UnderlyingEdge.Target = value;
        }
    }

    public override EdgePath EdgePath
    {
        get => reverse ? UnderlyingEdge.EdgePath.Inverse() : UnderlyingEdge.EdgePath;
        set => UnderlyingEdge.EdgePath = reverse ? value.Inverse() : value;
    }

    public override float OrderIndexStart
    {
        get => UnderlyingEdge.OrderIndexEnd;
        set => UnderlyingEdge.OrderIndexEnd = value;
    }

    public override float OrderIndexEnd
    {
        get => UnderlyingEdge.OrderIndexStart;
        set => UnderlyingEdge.OrderIndexStart = value;
    }

    public override Strip Reversed() => reverse ? UnderlyingEdge : new OrderedStrip(UnderlyingEdge, true);

    public override bool Equals(object obj) =>
        obj switch
        {
            OrderedStrip other => reverse == other.reverse && UnderlyingEdge.Equals(other.UnderlyingEdge),
            UnorientedStrip otherStrip => !reverse && UnderlyingEdge.Equals(otherStrip),
            _ => false
        };

    public override Strip Copy(FibredSurface fibredSurface = null, Curve curve = null,
        Junction source = null, Junction target = null, EdgePath edgePath = null, string name = null, float? orderIndexStart = null, float? orderIndexEnd = null)
    {
        if (!reverse) return UnderlyingEdge.CopyUnoriented(fibredSurface, curve, source, target, edgePath, name); 
        
        if (edgePath != null) edgePath = edgePath.Inverse();
        if (curve != null) curve = curve.Reversed();
        (source, target) = (target, source);
        (orderIndexStart, orderIndexEnd) = (orderIndexEnd, orderIndexStart);
        
        var unorientedCopy = UnderlyingEdge.CopyUnoriented(fibredSurface, curve, source, target, edgePath, name, orderIndexStart, orderIndexEnd);
        return unorientedCopy.Reversed();
    }

    public override Junction OtherVertex(Junction vertex) => UnderlyingEdge.OtherVertex(vertex);
}
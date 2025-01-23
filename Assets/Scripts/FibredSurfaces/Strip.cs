
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using QuikGraph;
using UnityEngine;

public abstract class Strip: IEdge<Junction>
{
    public virtual Curve Curve { get; set; }

    /// <summary>
    /// f maps this strip into the fibred Surface, and it traces out the edgePath g(this) = edgePath;
    /// </summary>
    public virtual List<Strip> EdgePath { get; set; }
    
    [CanBeNull] public virtual Strip Dg => EdgePath.FirstOrDefault();
    
    public virtual Junction Source { get; set; }
    public virtual Junction Target { get; set; }
    public abstract UnorientedStrip UnderlyingEdge { get; }
    public virtual string Name => Curve.Name;

    public virtual Junction OtherVertex(Junction vertex) => vertex == Source ? Target : Source;
    
    public abstract Strip Reversed();

    public override bool Equals(object other) =>
        other switch
        {
            OrderedStrip orderedStrip => orderedStrip.Equals(this),
            UnorientedStrip otherStrip => Source == otherStrip.Source && Target == otherStrip.Target &&
                                Curve.Equals(otherStrip.Curve) && EdgePath.SequenceEqual(otherStrip.EdgePath),
            // todo. this actually *should* be ReferenceEquals, shouldn't it? Parallel edges might be considered equal. (well no, because of the saved curve). However the "UnorderedStrip"s should not be doubled.
            _ => false
        };
    
    public virtual UnorientedStrip Copy() => new() {
        Curve = Curve,
        EdgePath = EdgePath,
        Source = Source,
        Target = Target
    };

    public EdgePoint this[int i] => new(this, i);

    public override string ToString() => $"Strip {Curve.Name}";

    public static int SharedInitialSegment(IList<Strip> strips)
    {
        IEnumerable<Strip> initialSegment = strips[0].EdgePath;
        foreach (var strip in strips.Skip(1)) 
            initialSegment = initialSegment.Zip(strip.EdgePath, (a, b) => Equals(a, b) ? a : null);

        int i = initialSegment.TakeWhile(strip => strip != null).Count();
        if (i == 0) Debug.LogError("The edges do not have a common initial segment.");
        return i;

    }
}

public class UnorientedStrip : Strip
{
    public ITransformable Drawable => Curve;
    public override UnorientedStrip UnderlyingEdge => this;

    private OrderedStrip reversed;
    public override Strip Reversed() => reversed ??= new OrderedStrip(this, true);

}

public class OrderedStrip: Strip
{
    public override Curve Curve => reverse ? UnderlyingEdge.Curve.Reverse() : UnderlyingEdge.Curve;
    
    public override UnorientedStrip UnderlyingEdge { get; }
    public override string Name => reverse ? UnderlyingEdge.Name.ToUpper() : UnderlyingEdge.Name;
    // "<s>" + UnderlyingEdge.Name + "</s>"   (strikethrough in RichText; workaround for overbar; use for fancy display?
    // For the moment, we just use the name of the underlying edge in UPPERCASE.
    
    /// <summary>
    /// actually this should never be false.
    /// </summary>
    public readonly bool reverse; 

    public OrderedStrip(UnorientedStrip underlyingEdge, bool reverse)
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

    public override List<Strip> EdgePath => reverse ? ReversedEdgePath(UnderlyingEdge.EdgePath) : UnderlyingEdge.EdgePath;

    public static List<Strip> ReversedEdgePath(IEnumerable<Strip> edgePath) =>
        edgePath.Reverse().Select(
            strip => strip.Reversed()
        ).ToList();

    public override Strip Reversed() => reverse ? UnderlyingEdge : new OrderedStrip(UnderlyingEdge, true);

    public override bool Equals(object obj) =>
        obj switch
        {
            OrderedStrip other => reverse == other.reverse && UnderlyingEdge.Equals(other.UnderlyingEdge),
            UnorientedStrip otherStrip => !reverse && UnderlyingEdge.Equals(otherStrip),
            _ => false
        };

    public override Junction OtherVertex(Junction vertex) => UnderlyingEdge.OtherVertex(vertex);
}
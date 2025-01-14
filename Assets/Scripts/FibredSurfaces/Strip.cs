
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using QuikGraph;

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

    public virtual Junction OtherVertex(Junction vertex) => vertex == Source ? Target : Source;
    
    public abstract OrderedStrip Reversed();

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
}

public class UnorientedStrip : Strip
{
    public ITransformable Drawable => Curve;
    public override UnorientedStrip UnderlyingEdge => this;
    public override OrderedStrip Reversed() => new(this, true);

}

public class OrderedStrip: Strip
{
    public override Curve Curve => UnderlyingEdge.Curve.Reverse();
    
    public override UnorientedStrip UnderlyingEdge { get; }
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
            strip => strip.Reversed() as Strip
        ).ToList();

    public override OrderedStrip Reversed() => new(UnderlyingEdge, !reverse);

    public override bool Equals(object obj) =>
        obj switch
        {
            OrderedStrip other => reverse == other.reverse && UnderlyingEdge.Equals(other.UnderlyingEdge),
            UnorientedStrip otherStrip => !reverse && UnderlyingEdge.Equals(otherStrip),
            _ => false
        };

    public override Junction OtherVertex(Junction vertex) => UnderlyingEdge.OtherVertex(vertex);
}
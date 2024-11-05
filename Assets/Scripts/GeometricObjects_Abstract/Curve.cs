using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public abstract class Curve: ITransformable<Curve>
{
    public abstract float Length { get; }

    public abstract Point EndPosition { get; }
    public abstract Point StartPosition { get; }
    public abstract Vector3 EndVelocity { get; }
    public abstract Vector3 StartVelocity { get; }

    public abstract DrawingSurface Surface { get; }

    public virtual IEnumerable<Point> NonDifferentiablePoints => Enumerable.Empty<Point>();

    public virtual Vector3 this[float t] => ValueAt(t);

    public abstract Vector3 ValueAt(float t);
    public abstract Vector3 DerivativeAt(float t);
    
    public virtual Curve Concatenate(Curve curve) => new ConcatenatedCurve(new Curve[] { this, curve });

    private Curve reverseCurve;
    public virtual Curve Reverse() => reverseCurve ??= new ReverseCurve(this);

    public virtual float GetClosestPoint(Vector3 point)
    {
        float f(float t) => (point - this[t]).sqrMagnitude;
        float fDeriv(float t) => 2 * Vector3.Dot(DerivativeAt(t), this[t] - point);

        float learningRate = 0.1f * Length;
        float t = Length / 2;
        for (var i = 0; i < 1000; i++)
        {
            float gradient = fDeriv(t);
            float change = learningRate * gradient;
            t -= change;
            if (t < 0) return 0;
            if (t > Length) return Length;

            if (Mathf.Abs(change) < 1e-6 || Mathf.Abs(f(t)) < 1e-6)
                break;
        }
        return t;
    }

    public abstract Curve ApplyHomeomorphism(Homeomorphism homeomorphism); // todo implement? (make homeos C1 diffeos!)
}

public class ConcatenatedCurve : Curve
{
    private readonly Curve[] segments;
    private IEnumerable<Point> nonDifferentiablePoints;

    public override DrawingSurface Surface => segments.First().Surface;

    public override float Length => length;
    private readonly float length;
    
    public override IEnumerable<Point> NonDifferentiablePoints => nonDifferentiablePoints ??= CalculateSingularPoints();

    public ConcatenatedCurve(IEnumerable<Curve> curves)
    {
        segments = curves.ToArray();
        
        length = (from segment in segments select segment.Length).Sum();
        if (length == 0) throw new("Length of curve is zero");

    }

    public override Point EndPosition => segments.Last().EndPosition;
    public override Point StartPosition => segments.First().StartPosition;
    public override Vector3 EndVelocity => segments.Last().EndVelocity;
    public override Vector3 StartVelocity => segments.First().StartVelocity;

    public override Vector3 ValueAt(float t)
    {
        t %= length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.ValueAt(t);
            t -= segment.Length;
        }

        throw new("What the heck");
    }

    public override Vector3 DerivativeAt(float t)
    {
        t %= length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.DerivativeAt(t);
            t -= segment.Length;
        }

        throw new("What the heck");
    }

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new ConcatenatedCurve(from segment in segments select segment.ApplyHomeomorphism(homeomorphism));
    
    
    private IEnumerable<Point> CalculateSingularPoints()
    {
        Curve nextCurve = segments.Last();
        List<Point> res = new();
        for (int i = 0; i <= segments.Length - 2; i++)
        {
            var curve = nextCurve;
            nextCurve = segments[i];
            if (curve.EndPosition != nextCurve.StartPosition)
                res.Add(new ConcatenationJumpPoint() { incomingCurve = curve, outgoingCurve = nextCurve });
        }
        return res;
    }
}

public class ReverseCurve : Curve
{
    private readonly Curve curve;

    public ReverseCurve(Curve curve)
    {
        this.curve = curve;
    }

    public override float Length => curve.Length;
    public override Point EndPosition => curve.StartPosition;
    public override Point StartPosition => curve.EndPosition;
    public override Vector3 EndVelocity => - curve.StartVelocity;
    public override Vector3 StartVelocity => - curve.EndVelocity;
    public override DrawingSurface Surface => curve.Surface;

    public override Vector3 ValueAt(float t) => curve.ValueAt(Length - t);
    public override Vector3 DerivativeAt(float t) => - curve.DerivativeAt(Length - t);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Reverse();
}

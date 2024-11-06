using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public abstract class Curve: ITransformable<Curve>
{
    public abstract string Name { get; }
    
    public abstract float Length { get; }

    public abstract Point EndPosition { get; }
    public abstract Point StartPosition { get; }
    public abstract Vector3 EndVelocity { get; }
    public abstract Vector3 StartVelocity { get; }

    public abstract DrawingSurface Surface { get; }

    public virtual IEnumerable<Point> NonDifferentiablePoints => Enumerable.Empty<Point>();

    public virtual Point this[float t] => ValueAt(t);

    public abstract Point ValueAt(float t);
    public abstract Vector3 DerivativeAt(float t);
    
    public virtual Curve Concatenate(Curve curve) => new ConcatenatedCurve(new Curve[] { this, curve });

    private Curve reverseCurve;

    public virtual Curve Reverse() => reverseCurve ??= new ReverseCurve(this);

    public virtual float GetClosestPoint(Vector3 point)
    {
        // todo: difference for points
        float f(float t) => (point - this[t].Position).sqrMagnitude;
        float fDeriv(float t) => 2 * Vector3.Dot(DerivativeAt(t), this[t].Position - point);

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

    public virtual Curve ApplyHomeomorphism(Homeomorphism homeomorphism) => new TransformedCurve(this, homeomorphism);
}

public class TransformedCurve : Curve
{
    private readonly Curve curve;
    private readonly Homeomorphism homeomorphism;

    public TransformedCurve(Curve curve, Homeomorphism homeomorphism)
    {
        this.curve = curve;
        this.homeomorphism = homeomorphism;
        if (curve.Surface != homeomorphism.source)
            throw new Exception("Homeomorphism does not match surface");
    }

    public override string Name => curve.Name + " --> " + homeomorphism.target.Name;
    
    public override float Length => curve.Length;
    public override Point EndPosition => curve.EndPosition.ApplyHomeomorphism(homeomorphism);
    public override Point StartPosition => curve.StartPosition.ApplyHomeomorphism(homeomorphism);
    public override Vector3 EndVelocity => throw new NotImplementedException("Derivative of homeo missing"); 
    public override Vector3 StartVelocity => throw new NotImplementedException("Derivative of homeo missing");
    public override DrawingSurface Surface => homeomorphism.target;
    public override Point ValueAt(float t) => curve.ValueAt(t).ApplyHomeomorphism(homeomorphism);

    public override Vector3 DerivativeAt(float t) => throw new NotImplementedException("Derivative of homeo missing");
    // we should have a TangentVector class that is transformable. For this, homeomorphisms must have a derivative. This is harder to do in the explicit examples; in the long run, I would actually want this, i.e. everything is C1.

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism) =>
        new TransformedCurve(curve, homeomorphism * this.homeomorphism);
}

public class ConcatenatedCurve : Curve
{
    private readonly Curve[] segments;
    private IEnumerable<Point> nonDifferentiablePoints;


    public override string Name { get; }

    public override DrawingSurface Surface => segments.First().Surface;

    public override float Length { get; }

    public override IEnumerable<Point> NonDifferentiablePoints => nonDifferentiablePoints ??= CalculateSingularPoints();

    public ConcatenatedCurve(IEnumerable<Curve> curves)
    {
        segments = curves.ToArray();

        float length = (from segment in segments select segment.Length).Sum();
        if (length == 0) throw new Exception("Length of curve is zero");
        Length = length;
        
        Name = string.Join(" -> ", from segment in segments select segment.Name);
    }

    public override Point EndPosition => segments.Last().EndPosition;
    public override Point StartPosition => segments.First().StartPosition;
    public override Vector3 EndVelocity => segments.Last().EndVelocity;
    public override Vector3 StartVelocity => segments.First().StartVelocity;

    public override Point ValueAt(float t)
    {
        t %= Length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.ValueAt(t);
            t -= segment.Length;
        }

        throw new Exception("What the heck");
    }

    public override Vector3 DerivativeAt(float t)
    {
        t %= Length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.DerivativeAt(t);
            t -= segment.Length;
        }

        throw new Exception("What the heck");
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

    public override string Name => curve.Name + '\'';
    public override float Length => curve.Length;
    public override Point EndPosition => curve.StartPosition;
    public override Point StartPosition => curve.EndPosition;
    public override Vector3 EndVelocity => - curve.StartVelocity;
    public override Vector3 StartVelocity => - curve.EndVelocity;
    public override DrawingSurface Surface => curve.Surface;

    public override Point ValueAt(float t) => curve.ValueAt(Length - t);
    public override Vector3 DerivativeAt(float t) => - curve.DerivativeAt(Length - t);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Reverse();
}

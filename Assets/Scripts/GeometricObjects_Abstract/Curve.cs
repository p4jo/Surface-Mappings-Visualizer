using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public interface ICurve
{
    public float Length { get; }

    public IPoint EndPosition { get; }
    public IPoint StartPosition { get; }
    public Vector3 EndVelocity { get; }
    public Vector3 StartVelocity { get; }
    
    public DrawingSurface Surface { get; }
    
    public virtual IEnumerable<Vector3> NonDifferentiablePoints => Enumerable.Empty<Vector3>();

    public virtual Vector3 this[float t] => ValueAt(t);

    public Vector3 ValueAt(float t);
    public Vector3 DerivativeAt(float t);
    
    public virtual ICurve Concatenate(ICurve curve) => new ConcatenatedCurve(new ICurve[] { this, curve });

    public virtual ICurve Reverse() => new ReverseCurve(this);

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

    ICurve ApplyHomeomorphism(Homeomorphism homeomorphism);
}

public class ConcatenatedCurve : ICurve
{
    private readonly ICurve[] segments;
    private IEnumerable<IPoint> nonDifferentiablePoints;

    public DrawingSurface Surface => segments.First().Surface;

    public float Length { get; private set; }

    public IEnumerable<IPoint> NonDifferentiablePoints
    {
        get => CalculateSingularPoints();
        protected set => nonDifferentiablePoints = value;
    }

    public ConcatenatedCurve(IEnumerable<ICurve> curves)
    {
        segments = curves.ToArray();
        
        Length = (from segment in segments select segment.Length).Sum();
        if (Length == 0) throw new System.Exception("Length of curve is zero");

    }

    public IPoint EndPosition => segments.Last().EndPosition;
    public IPoint StartPosition => segments.First().StartPosition;
    public Vector3 EndVelocity => segments.Last().EndVelocity;
    public Vector3 StartVelocity => segments.First().StartVelocity;

    public Vector3 ValueAt(float t)
    {
        t %= Length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.ValueAt(t);
            t -= segment.Length;
        }

        throw new System.Exception("What the heck");
    }

    public Vector3 DerivativeAt(float t)
    {
        t %= Length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.DerivativeAt(t);
            t -= segment.Length;
        }

        throw new System.Exception("What the heck");
    }

    public ICurve ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new ConcatenatedCurve(from segment in segments select segment.ApplyHomeomorphism(homeomorphism));
    
    
    private IEnumerable<IPoint> CalculateSingularPoints()
    {
        ICurve curve, nextCurve = segments.Last();
        List<IPoint> res = new();
        for (int i = 0; i <= segments.Length - 2; i++)
        {
            curve = nextCurve;
            nextCurve = segments[i];
            if (curve.EndPosition != nextCurve.StartPosition)
                res.Add(new ConcatenationJumpPoint() { incomingCurve = curve, outgoingCurve = nextCurve });
        }
        
        NonDifferentiablePoints = res;
        return res;
    }
}

public class ReverseCurve : ICurve
{
    private readonly ICurve curve;

    public ReverseCurve(ICurve curve)
    {
        this.curve = curve;
    }

    public float Length => curve.Length;
    public IPoint EndPosition => curve.StartPosition;
    public IPoint StartPosition => curve.EndPosition;
    public Vector3 EndVelocity => - curve.StartVelocity;
    public Vector3 StartVelocity => - curve.EndVelocity;
    public DrawingSurface Surface => curve.Surface;

    public Vector3 ValueAt(float t) => curve.ValueAt(Length - t);
    public Vector3 DerivativeAt(float t) => - curve.DerivativeAt(Length - t);

    public ICurve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Reverse();
}

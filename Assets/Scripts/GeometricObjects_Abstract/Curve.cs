using System.Collections.Generic;
using System.Linq;
using kmty.NURBS;
using UnityEngine;


public interface ICurve
{
    public float Length { get; }

    public Vector3 EndPosition { get; }
    public Vector3 StartPosition { get; }
    public Vector3 EndVelocity { get; }
    public Vector3 StartVelocity { get; }

    public Vector3 this[float value] { get; }
    
    public virtual ICurve Concatenate(ICurve curve) => new ConcatenatedCurve(new ICurve[] { this, curve });

    public virtual ICurve Reverse() => new ReverseCurve(this);
}


public class ConcatenatedCurve : ICurve
{
    private readonly ICurve[] segments;

    public float Length { get; private set; }

    public ConcatenatedCurve(IEnumerable<ICurve> curves)
    {
        segments = curves.ToArray();
        
        Length = (from segment in segments select segment.Length).Sum();
        if (Length == 0) throw new System.Exception("Length of curve is zero");
    }

    public Vector3 EndPosition => segments.Last().EndPosition;
    public Vector3 StartPosition => segments.First().StartPosition;
    public Vector3 EndVelocity => segments.Last().EndVelocity;
    public Vector3 StartVelocity => segments.First().StartVelocity;

    public virtual Vector3 this[float value]
    {
        get
        {
            value %= Length;
            foreach (var segment in segments)
            {
                if (value < segment.Length)
                    return segment[value];
                value -= segment.Length;
            }

            throw new System.Exception("What the heck");
        }
    }

}

public class ReverseCurve : ICurve
{
    readonly ICurve curve;

    public ReverseCurve(ICurve curve)
    {
        this.curve = curve;
    }

    public float Length => curve.Length;
    public Vector3 EndPosition => curve.StartPosition;
    public Vector3 StartPosition => curve.EndPosition;
    public Vector3 EndVelocity => - curve.StartVelocity;
    public Vector3 StartVelocity => - curve.EndVelocity;

    public Vector3 this[float value] => curve[Length - value];
}

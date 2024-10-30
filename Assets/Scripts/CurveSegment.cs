using System.Collections.Generic;
using System.Linq;
using kmty.NURBS;
using UnityEngine;

public abstract class CurveSegment
{
    protected abstract IEnumerable<CP> GetSplinePoints(float resolution);
    public float length { get; private set; }
    
    // todo: cache, display, dispose of old ones ...
    public Spline GetSpline(float resolution)
    {
        return new Spline(GetSplinePoints(resolution).ToArray(), 3, SplineType.Loop);
    }
    
    public SurfaceHandler GetTube(float resolution)
    {
        // return new SurfaceHandler(GetSpline(resolution));
        // todo: create a tube from a spline
        throw new System.NotImplementedException();
    }
    
    // todo: create an instance of a spline class ⇒ get tangent vectors and positions at any time

    public static implicit operator Curve(CurveSegment segment)
        => new(new[]{ segment });

    public float this[float value]
    {
        get { throw new System.NotImplementedException(); }
    }
}

public class Curve
{
    private readonly CurveSegment[] segments;

    public readonly float length;

    public Vector3 endPosition, startPosition, endVelocity, startVelocity;

    public float this[float value]
    {
        get
        {
            value %= length;
            foreach (var segment in segments)
            {
                if (value < segment.length)
                    return segment[value];
                value -= segment.length;
            }
            throw new System.Exception("What the heck");
        }
    }

    public Curve(IEnumerable<CurveSegment> curveSegments)
    {
        segments = curveSegments.ToArray();
        length = (from segment in segments select segment.length).Sum();
        if (length == 0) throw new System.Exception("Length of curve is zero");
    }

    public static Curve operator * (Homeomorphism f, Curve c)
    {
        throw new System.NotImplementedException();
    }

    public static Curve operator *(Curve a, Curve b)
    {
        // todo: logic for not perfectly matching endpoints or direction vectors
        return new(a.segments.Concat(b.segments));
    }
}

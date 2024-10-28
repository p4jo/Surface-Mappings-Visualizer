using System.Collections.Generic;
using System.Linq;
using kmty.NURBS;
using UnityEngine;

public abstract class CurveSegment
{
    protected abstract IEnumerable<CP> GetSplinePoints(float resolution);
    
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
    
}

public abstract class Curve: MonoBehaviour
{
    public abstract List<CurveSegment> Segments { get; }
    
    public static Curve operator * (Homeomorphism f, Curve c)
    {
        throw new System.NotImplementedException();
    }
}

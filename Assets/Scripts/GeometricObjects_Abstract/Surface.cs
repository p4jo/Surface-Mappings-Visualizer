using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Surface 
{
    public string Name { get; protected set; }
    public int Genus { get; protected set; }
    public readonly List<Point> punctures = new();
    public readonly bool is2D;

    protected Surface(string name, int genus, bool is2D)
    {
        this.Name = name;
        this.Genus = genus;
        this.is2D = is2D;
    }


    /// <summary>
    /// Bring the point into the boundary / significant point if it is close. Return null if too far from the surface.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public abstract Point ClampPoint(Vector3? point);

    
    /// <summary>
    /// A right-handed basis with the normal vector of the surface as the third vector.
    /// </summary>
    public abstract TangentSpace BasisAt(Point position);
    
    public abstract Vector3 MinimalPosition { get; }
    public abstract Vector3 MaximalPosition { get; }

    public virtual Homeomorphism GetAutomorphism(AutomorphismType type, IDrawnsformable[] parameters) => null;
}

public abstract class GeodesicSurface: Surface
{
    protected GeodesicSurface(string name, int genus, bool is2D) : base(name, genus, is2D){}

    public abstract Curve GetGeodesic(Point start, Point end, string name);
    public abstract Curve GetGeodesic(TangentVector startVelocity, float length, string name);

    public virtual Curve GetPathFromWaypoints(IEnumerable<Point> points, string name)
    {
        var pointArray = points.ToArray();
        var geodesicSegments = 
            from i in Enumerable.Range(0, pointArray.Length - 1)
            let start = pointArray[i]
            let end = pointArray[i+1]
            select GetGeodesic(start, end, name);
        // todo: optimize over possible tangent vectors at the concatenation -> do this in ConcatenatedCurve!
        var concatenatedCurve = new ConcatenatedCurve(geodesicSegments, name);
        return concatenatedCurve.Smoothed();  
    }
    
    public virtual Curve ShiftedCurve(Curve curve, float shift, string name = null) => new ShiftedCurve(curve, shift, name);

    public abstract float DistanceSquared(Point startPoint, Point endPoint);

    public virtual float CurveLength(Curve curve)
    {
        var res = 0.1f;
        while (res > curve.Length / 4) res /= 2;
        return ArclengthAtTime(curve, res)[^1];
    }
    
    protected static List<float> ArclengthAtTime(Curve curve, float res = 0.05f)
    {
        
        if (curve.Surface is not GeodesicSurface surface)
        {
            Debug.LogError("The curve must be on a GeodesicSurface to calculate the arclength parameterization.");
            return Enumerable.Range(0, (int) (curve.Length / res) + 1).Select(i => i * res).ToList();
        }
        var times = new List<float> { 0f }; 
        // l -> t: times[i] = l[i * res]

        float T = curve.Length;
        var l = 0f;
        var lastPoint = curve.StartPosition;
        for (float t = res; t <= T + res; t += res)
        {
            if (t > T) t = T;
            var p = curve[t];
            l += MathF.Sqrt(surface.DistanceSquared(p, lastPoint));
            times.Add(l);
        }

        return times;
    }
    public static Curve ByArclength(Curve curve)
    {
        if (curve.Surface is not GeodesicSurface surface)
        {
            Debug.LogError("The curve must be on a GeodesicSurface to calculate the arclength parameterization.");
            return curve;
        }

        var res = 0.1f;
        while (res > curve.Length / 4) res /= 2;
        var lengths = ArclengthAtTime(curve, res);
        // t -> l

        TangentVector DerivativeAt(float targetLength)
        {
            var i = 0;
            while (lengths[i] < targetLength) i++;
            var dtdl = 1 / ( lengths[i] - lengths[i - 1] );
            var t = res * i + (targetLength - lengths[i - 1]) * dtdl;
            return dtdl * curve.DerivativeAt(t);
        }
        
        return new ParametrizedCurve(curve.Name + " by arclength", curve.Length, surface, DerivativeAt); 
    }
}

public class ShiftedCurve : Curve
{
    private readonly Curve curve;
    private readonly Func<float, float> shift;

    public ShiftedCurve(Curve curve, Func<float, float> shift, string name = null)
    {
        if (curve.Surface is not GeodesicSurface) 
            throw new ArgumentException($"To shift the curve {curve}, the underlying surface must be a GeodesicSurface.");
        this.curve = curve;
        this.shift = shift;
        this.Name = name ?? curve.Name + " shifted by " + shift(curve.Length / 2);
    }

    public ShiftedCurve(Curve curve, float shift, string name = null) : this(curve, t => shift, name)
    { }
    
    public sealed override string Name { get; set; }
    
    public override Curve Copy() => new ShiftedCurve(curve, shift, Name) {Color = Color};

    public override float Length => curve.Length;
    public override Surface Surface => curve.Surface;
    
    private GeodesicSurface geodesicSurface => (GeodesicSurface) Surface;
    
    public override Point ValueAt(float t)
    {
        var basis = curve.BasisAt(t);
        var localCurve = geodesicSurface.GetGeodesic(basis.B.Normalized, shift(t), $"shift geodesic for {curve.Name} at time {t}");
        return localCurve.EndPosition;
    }

    public override TangentVector DerivativeAt(float t)
    {
        var basis = curve.BasisAt(t);
        var localCurve = geodesicSurface.GetGeodesic(basis.B.Normalized, shift(t), $"shift geodesic for {curve.Name} at time {t}");
        return new (localCurve.EndPosition, basis.basis.a); // this is not really correct, but for small shifts it is good enough. It is unfeasible to calculate this correctly (derivative of exponential map...)
    }
}

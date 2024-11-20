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

    public abstract TangentSpace BasisAt(Point position);
    
    public abstract Vector3 MinimalPosition { get; }
    public abstract Vector3 MaximalPosition { get; }

    public virtual Homeomorphism GetAutomorphism(AutomorphismType type, ITransformable[] parameters) => null;
}

public abstract class GeodesicSurface: Surface
{
    protected GeodesicSurface(string name, int genus, bool is2D) : base(name, genus, is2D){}

    public abstract Curve GetGeodesic(Point start, Point end, string name);

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

    public abstract float DistanceSquared(Point startPoint, Point endPoint);
}
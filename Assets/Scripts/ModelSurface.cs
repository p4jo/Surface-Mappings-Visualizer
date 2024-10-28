
using System;
using System.Collections.Generic;
using UnityEngine;

public class ModelSurface: DrawingSurface
{
    private readonly GeometryType geometryType;
    private readonly string boundaryWord;

    public ModelSurface(string name, int genus, int punctures, GeometryType geometryType, string boundaryWord) : base(name, genus)
    {
        this.geometryType = geometryType;
        this.boundaryWord = boundaryWord;
    }
    
    public Geodesic GetGeodesic(Vector3 start, Vector3 end)
    {
        switch (geometryType)
        {
            case GeometryType.Flat:
                return new FlatGeodesic(start, end);
            case GeometryType.Hyperbolic:
                return new HyperbolicGeodesic(start, end);
            case GeometryType.Spherical:
                return new SphericalGeodesic(start, end);
            default:
                throw new NotImplementedException();
        }
    }

}

public enum GeometryType
{
    /// <summary>
    /// This represents the manifolds as flat polygons with geodesics as straight lines
    /// - curvature is concentrated at the punctures.
    /// </summary>
    Flat,
    
    /// <summary>
    /// This represents the manifolds as polygons in the Poincaré disk model of the hyperbolic plane.
    /// All sides and geodesics are circular arcs (or straight lines). 
    /// </summary>
    Hyperbolic,
    
    /// <summary>
    /// this is probably not necessary:
    /// punctured spheres will be displayed as the open disk (either flat or hyperbolic)
    /// and the non-punctured sphere has completely trivial mapping class group
    /// </summary>
    Spherical,
}
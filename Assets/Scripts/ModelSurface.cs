
using System;
using System.Collections.Generic;
using System.Linq;
using Dreamteck;
using UnityEngine;

public class ModelSurface: DrawingSurface
{
    private readonly GeometryType geometryType;
    private readonly Dictionary<string, Curve> boundaries = new();

    public ModelSurface(string name,
        int genus,
        int punctures,
        GeometryType geometryType,
        IDictionary<string, (Vector2, Vector2)> boundaries
    ) : base(name, genus, true)
    {
        this.geometryType = geometryType;
        foreach (var (label, (start, end)) in boundaries)        
        {
            this.boundaries[label] = GetGeodesic(start, end);
        }

        for (int i = 0; i < punctures; i++)
        {
            this.punctures.Add();
        }
    }

    
    class Vertex
    {
           List<(Curve, bool)> boundaryCurves = new(); 
    }
    protected List<(List<Vector3>, float)> Vertices(float εSquared = 0.0001f)
    {
        var allVertexStars = new List<(Vector3, List<(Curve, bool)>)>();
        foreach (var curve in boundaries.Values)
        {
            AddDirectedEdgeToAStar(false, curve);
            AddDirectedEdgeToAStar(true, curve);
        }

        throw new NotImplementedException();
        foreach (var (position, directedEdges) in allVertexStars)
        {
            // var angles = new List<float>();
            // for (int i = 0; i < directedEdges.Count; i++)
            // {
            //     var (curve, curveReverse) = directedEdges[i];
            //     var (nextCurve, nextCurveReverse) = directedEdges[(i + 1) % directedEdges.Count];
            //     var direction = 
            // }
            // var angles = (
            //     from edge in directedEdges
            //     let curve = edge.Item1
            //     let reverse = edge.Item2
            //     let direction = reverse ? curve.startVelocity : -curve.endVelocity
            //     select direction.cosine
            // ).ToList();
        }

        void AddDirectedEdgeToAStar(bool reverse, Curve curve)
        {
            var position = reverse ? curve.startPosition : curve.endPosition;
            var succeeded = false;
            foreach (var (pos, vertexStar) in allVertexStars)
            {
                if (!((pos - position).sqrMagnitude <= εSquared)) continue;
                vertexStar.Add((curve, reverse));
                succeeded = true;
                break;
            }
            if (!succeeded) 
                allVertexStars.Add((curve.startPosition, new() {(curve, reverse)}));
        }
    }

    public void InsertBoundaries(int addedGenus, int addedPunctures, IDictionary<string, (Vector2, Vector2)> extraBoundaries)
    {
        Genus += addedGenus;
        
    }
    
    public GeodesicSegment GetGeodesic(Vector2 start, Vector2 end)
    {
        return geometryType switch
        {
            GeometryType.Flat => new FlatGeodesicSegment(start, end),
            GeometryType.Hyperbolic => new HyperbolicGeodesicSegment(start, end),
            GeometryType.Spherical => new SphericalGeodesicSegment(start, end),
            _ => throw new NotImplementedException()
        };
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
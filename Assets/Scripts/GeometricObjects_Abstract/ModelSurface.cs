
using System;
using System.Collections.Generic;
using System.Linq;
using Dreamteck;
using UnityEngine;
using Random = UnityEngine.Random;


public class ModelSurface: DrawingSurface
{
    const float tau = 2 * Mathf.PI;

    #region local types
    public record PolygonSide
    {
        public readonly string label;
        public readonly Vector2 start, end;
        public readonly bool rightIsInside;

        public PolygonSide(string label, Vector2 start, Vector2 end, bool rightIsInside)
        {
            this.label = label;
            this.start = start;
            this.end = end;
            this.rightIsInside = rightIsInside;
        }
    }
    
    protected record GeodesicSide
    {
        public readonly string label;
        public readonly ICurve curve;
        public readonly bool rightIsInside;
        public readonly float angle;
        public int vertexIndex = -1;
        public GeodesicSide other;

        public GeodesicSide(ICurve curve, bool rightIsInside, string label)
        {
            this.curve = curve;
            this.rightIsInside = rightIsInside;
            this.label = label;
            angle = Mathf.Atan2(curve.StartVelocity.y, curve.StartVelocity.x);
        }

        public GeodesicSide(PolygonSide side, GeometryType geometryType): this(
            GetGeodesic(side.start, side.end, geometryType),
            side.rightIsInside,
            side.label
        ) { }

        public GeodesicSide Reverse() => new(curve.Reverse(), !rightIsInside, label + "'");

        public void AddOther(GeodesicSide side)
        {
            if (other != null)
                throw new Exception("Check your polygon! Three sides have the same label.");
            other = side;
            side.other = this;
            if (other.rightIsInside == rightIsInside)
                Debug.Log("Check your polygon! Two sides with the same label have the surface on the same side." +
                          " This might mean that it is not orientable?");
        }
    }
    
     protected record Vertex
     {
           public readonly List<GeodesicSide> boundaryCurves = new(); 
           public float angle;
     }
     
    #endregion

    private readonly GeometryType geometryType;
    private readonly List<GeodesicSide> identifiedSides = new();
    private readonly List<Vertex> vertices = new();

    public ModelSurface(string name,
        int genus,
        int punctures,
        GeometryType geometryType,
        List<PolygonSide> identifiedSides
    ) : base(name, genus, true)
    {
        this.geometryType = geometryType;
        foreach (var side in identifiedSides)
        {
            var newSide = new GeodesicSide(side, geometryType);
            var oldSide = this.identifiedSides.FirstOrDefault(existingSide => existingSide.label == newSide.label);
            oldSide?.AddOther(newSide);
        }
        if ((from side in this.identifiedSides select side.other != null).Any())
            throw new Exception("Check your polygon! Some sides are not paired.");

        #region find vertices
        
        var polygonVertices = new List<(Vector3, List<GeodesicSide>)>();

        void AddDirectedEdgeToAStar(GeodesicSide side)
        {
            var curve = side.curve;
            var position = curve.StartPosition;
            var succeeded = false;
            foreach (var (pos, vertexStar) in polygonVertices)
            {
                if (!((pos - position).sqrMagnitude <= (float)0.0001)) continue;
                vertexStar.Add(side);
                succeeded = true;
                break;
            }
            if (!succeeded) 
                polygonVertices.Add((curve.StartPosition, new() { side }));
        }
        
        foreach (var curve in this.identifiedSides)
        {
            AddDirectedEdgeToAStar(curve);
            AddDirectedEdgeToAStar(curve.Reverse());
        }
        
        foreach ((_, List<GeodesicSide> identifiedGeodesics) in polygonVertices)
        {
            identifiedGeodesics.Sort((a, b) => a.angle.CompareTo(b.angle));
        }
        
        while (true)
        {
            var vertexIndex = vertices.Count;

            var (edgesAtCurrentPolygonVertex, edge) = (
                    from polygonVertex in polygonVertices
                    let edgesAtThisPolygonVertex = polygonVertex.Item2
                    let unassignedEdge = edgesAtThisPolygonVertex.FirstOrDefault(
                        edge => edge.vertexIndex == -1
                    )
                    select (edgesAtThisPolygonVertex, unassignedEdge)                    
                ).FirstOrDefault(
                    pair => pair.Item2 != null
                );
            
            if (edge == null) // all edges have been assigned to a vertex
                break;
            
            // todo: we might want to do something with the angles
            List<float> angles = new();
            
            edge.vertexIndex = vertexIndex; 
            var vertex = new Vertex();
            vertex.boundaryCurves.Add(edge);

            while (true)
            {
                var i = edgesAtCurrentPolygonVertex.IndexOf(edge);
                var turnDirection = edge.rightIsInside ? -1 : 1;
                var newEdge = edgesAtCurrentPolygonVertex[(i + turnDirection) % edgesAtCurrentPolygonVertex.Count];
                
                if (newEdge.rightIsInside == edge.rightIsInside)
                    throw new Exception("Check your polygon! Two edges next to each other on a vertex don't agree on what is the inside of the polygon.");
                
                var angle = newEdge.angle - edge.angle;
                if (angle < 0)
                    angle += tau;
                angles.Add(angle);
                vertex.angle += angle;

                if (newEdge.vertexIndex == vertexIndex)
                {
                    if (newEdge != vertex.boundaryCurves[0])
                        throw new Exception("Check your polygon! The supposed cyclic order of edges closed back in on itself, but not where it should.");
                    break; // we have come full circle
                }
                
                if (newEdge.vertexIndex != -1)
                    throw new Exception("Check your polygon!");
            
            
                edge.vertexIndex = vertexIndex; 
                vertex.boundaryCurves.Add(edge);
                
                edge = newEdge.other;
                    
            }
            vertices.Add(vertex);
        }
        
        #endregion
        
        // todo: think about this ordering
        var vert = vertices.OrderByDescending(vertex => Mathf.Abs(tau / vertex.angle - 1));
        this.punctures.AddRange((
                from vertex in vert
                select vertex.boundaryCurves.First().curve.StartPosition
            ).Concat(
                from _ in Enumerable.Range(0, punctures) 
                select (Vector3) Random.insideUnitCircle
            ).Take(punctures)
        );
    }

    
    

    public void InsertBoundaries(int addedGenus, int addedPunctures, IEnumerable<PolygonSide> extraBoundaries)
    {
        // todo: implement (this should be basically the same as the constructor and be called from there)
        // todo: check if the inside of the polygon agrees! All rays (from boundary curves) into the "inside" should hit boundary curves on their "inside" side!
        Genus += addedGenus;
        // the homeomorphism must be defined elsewhere (where this is called from)
    }
    
    public static GeodesicSegment GetGeodesic(Vector2 start, Vector2 end, GeometryType geometryType)
    {
        return geometryType switch
        {
            GeometryType.Flat => new FlatGeodesicSegment(start, end),
            GeometryType.Hyperbolic => new HyperbolicGeodesicSegment(start, end),
            GeometryType.Spherical => new SphericalGeodesicSegment(start, end),
            _ => throw new NotImplementedException()
        };
    }

    public ModelSurface Copy()
    {
        var res = new ModelSurface(Name, Genus, 0, geometryType, new List<PolygonSide>());
        // todo: this is very hacky
        res.punctures.AddRange(punctures);
        res.vertices.AddRange(vertices);
        res.identifiedSides.AddRange(identifiedSides);
        return res;
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
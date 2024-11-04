using System;
using System.Collections.Generic;
using System.Linq;
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

    #endregion

    private readonly GeometryType geometryType;
    private readonly List<ModelSurfaceSide> sideCurves = new();
    private readonly List<ModelSurfaceVertex> vertices = new();
    private readonly List<PolygonSide> sidesAsParameters;

    public ModelSurface(string name,
        int genus,
        int punctures,
        GeometryType geometryType,
        List<PolygonSide> identifiedSides
    ) : base(name, genus, true)
    {
        this.geometryType = geometryType;
        sidesAsParameters = identifiedSides;
        
        foreach (var side in identifiedSides)
        {
            var newSide = new ModelSurfaceSide(side, geometryType, this);
            var oldSide = this.sideCurves.FirstOrDefault(existingSide => existingSide.label == newSide.label);
            oldSide?.AddOther(newSide);
        }
        if ((from side in this.sideCurves select side.other != null).Any())
            throw new Exception("Check your polygon! Some sides are not paired.");

        #region find vertices
        
        var polygonVertices = new List<(Vector3, List<ModelSurfaceSide>)>();

        void AddDirectedEdgeToAStar(ModelSurfaceSide side)
        {
            var curve = side.curve;
            var position = curve.StartPosition.Position;
            var succeeded = false;
            foreach (var (pos, vertexStar) in polygonVertices)
            {
                if (!pos.ApproximatelyEquals(position)) continue;
                vertexStar.Add(side);
                succeeded = true;
                break;
            }
            if (!succeeded) 
                polygonVertices.Add((curve.StartPosition.Position, new() { side }));
        }
        
        foreach (var curve in this.sideCurves)
        {
            AddDirectedEdgeToAStar(curve);
            AddDirectedEdgeToAStar(curve.Reverse());
        }
        
        foreach ((_, List<ModelSurfaceSide> identifiedGeodesics) in polygonVertices)
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
            var vertex = new ModelSurfaceVertex();
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
                let c = ClampPoint((Vector3) Random.insideUnitCircle) 
                // todo: this MUST NOT be disjoint from the surface. Also it should be approximately the size of the surface
                where c != null
                select c
            ).Take(punctures)
        );
    }

    
    
    /// <summary>
    /// This actually doen't necessarily preserve the locations of the old punctures (if newly added corners have worse
    /// angles than the ones already there)
    /// </summary>
    /// <param name="addedGenus"></param>
    /// <param name="addedPunctures"></param>
    /// <param name="extraBoundaries"></param>
    public ModelSurface WithAddedBoundaries(int addedGenus, int addedPunctures, IEnumerable<PolygonSide> extraBoundaries)
    {
        return new(Name, Genus + addedGenus, 0, geometryType,
            sidesAsParameters.Concat(extraBoundaries).ToList());
        
        // todo: implement (this should be basically the same as the constructor and be called from there)
        // todo: check if the inside of the polygon agrees! All rays (from boundary curves) into the "inside" should hit boundary curves on their "inside" side!
        Genus += addedGenus;
        // the corresponding homeomorphisms must be defined elsewhere (where this is called from)
    }
    
    public static GeodesicSegment GetGeodesic(Vector2 start, Vector2 end, GeometryType geometryType, DrawingSurface surface)
    {
        return geometryType switch
        {
            GeometryType.Flat => new FlatGeodesicSegment(start, end, surface),
            GeometryType.Hyperbolic => new HyperbolicGeodesicSegment(start, end, surface),
            GeometryType.Spherical => new SphericalGeodesicSegment(start, end, surface),
            _ => throw new NotImplementedException()
        };
    }

    public override IPoint ClampPoint(Vector3? point) // todo: this is extremely inefficient
    {
        if (point == null) return null;
        var p = point.Value;
        ModelSurfaceSide closestSide = null;
        var closeness = float.MaxValue;
        Vector3 difference = Vector3.zero;
        var time = 0f;
        foreach (var side in sideCurves)
        {
            float t = side.curve.GetClosestPoint(p);
            Vector3 diff = side.curve[t] - p;
            float l = diff.sqrMagnitude;
            if (l >= closeness) continue;
            
            time = t;
            closeness = l;
            difference = diff;
            closestSide = side;
        }
        if (closestSide is null)
            throw new("Why are there no sides at this point?");
        if (closeness < 0.1f)
        {
            if (time == 0)
                return vertices[closestSide.vertexIndex];
            if (time == closestSide.curve.Length)
                return vertices[closestSide.other.vertexIndex];
            return new ModelSurfaceBoundaryPoint(closestSide, time);
        }
        var forward = closestSide.curve.DerivativeAt(time);
        var right = new Vector3(forward.y, -forward.x); // orientation!
        var rightness = Vector3.Dot(right, difference);
        if (rightness < 0 && closestSide.rightIsInside || rightness > 0 && !closestSide.rightIsInside)
        { // this is outside
            return null;
        }

        return new BasicPoint(p);
    }

    public override void UpdatePoint(IPoint point)
    {
        throw new NotImplementedException();
    }
}


public record ModelSurfaceSide: ICurve
{
    public readonly string label;
    public readonly ICurve curve;
    public readonly bool rightIsInside;
    public readonly float angle;
    public int vertexIndex = -1;
    public ModelSurfaceSide other;

    public ModelSurfaceSide(ICurve curve, bool rightIsInside, string label)
    {
        this.curve = curve;
        this.rightIsInside = rightIsInside;
        this.label = label;
        angle = Mathf.Atan2(curve.StartVelocity.y, curve.StartVelocity.x);
    }

    public ModelSurfaceSide(ModelSurface.PolygonSide side, GeometryType geometryType, DrawingSurface surface): this(
        ModelSurface.GetGeodesic(side.start, side.end, geometryType, surface),
        side.rightIsInside,
        side.label
    ) { }

    public float Length => curve.Length;
    public IPoint EndPosition => curve.EndPosition;
    public IPoint StartPosition => curve.StartPosition;
    public Vector3 EndVelocity => curve.EndVelocity;
    public Vector3 StartVelocity => curve.StartVelocity;
    public DrawingSurface Surface => curve.Surface;
    public Vector3 ValueAt(float t) => curve.ValueAt(t);

    public Vector3 DerivativeAt(float t)
    {
        throw new NotImplementedException();
    }

    public ModelSurfaceSide Reverse() => new(curve.Reverse(), !rightIsInside, label + "'");
    public ICurve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        return new ModelSurfaceSide(curve.ApplyHomeomorphism(homeomorphism), rightIsInside, label);
        // todo: think about orientation
    }

    public void AddOther(ModelSurfaceSide side)
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
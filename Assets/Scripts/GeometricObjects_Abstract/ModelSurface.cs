using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework.Constraints;
using UnityEngine;
using Random = UnityEngine.Random;


public class ModelSurface: GeodesicSurface
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

    public readonly GeometryType geometryType;
    public readonly List<ModelSurfaceSide> sideCurves = new();
    public readonly List<ModelSurfaceVertex> vertices = new();
    public readonly List<PolygonSide> sidesAsParameters;
    private readonly GeodesicSurface geometrySurface; // Euclidean or Hyperbolic plane.
    
    public static readonly Dictionary<GeometryType, GeodesicSurface> BaseGeometrySurfaces = new()
    {
        [GeometryType.Flat] = new EuclideanPlane(),
        [GeometryType.Hyperbolic] = new HyperbolicPlane(),
        [GeometryType.Spherical] = null // todo?
    };
    
    public override Vector3 MinimalPosition { get; }
    public override Vector3 MaximalPosition { get; }

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
            var oldSide = this.sideCurves.FirstOrDefault(existingSide => existingSide.Name == newSide.Name);
            oldSide?.AddOther(newSide);
            this.sideCurves.Add(newSide);
        }
        if (sideCurves.Any(side => side.other == null))
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
            AddDirectedEdgeToAStar(curve.ReverseModelSide());
        }
        
        foreach ((_, List<ModelSurfaceSide> identifiedGeodesics) in polygonVertices)
        {
            identifiedGeodesics.Sort((a, b) => a.angle.CompareTo(b.angle));
            // increasing index means turning left
        }

        MinimalPosition = new(polygonVertices.Min(pair => pair.Item1.x), polygonVertices.Min(pair => pair.Item1.y));
        MaximalPosition = new(polygonVertices.Max(pair => pair.Item1.x), polygonVertices.Max(pair => pair.Item1.y)); ;
        
        while (true)
        {
            var vertex = new ModelSurfaceVertex();
            var vertexIndex = vertices.Count;
            
            if (vertexIndex > 3 * sideCurves.Count)           
                throw new Exception("what the heck?");
            
            vertices.Add(vertex);

            var oldEdge = (
                    from polygonVertex in polygonVertices
                    let edgesAtThisPolygonVertex = polygonVertex.Item2
                    let unassignedEdge = edgesAtThisPolygonVertex.FirstOrDefault(
                        edge => edge.vertexIndex == -1
                    )
                    select unassignedEdge                  
                ).FirstOrDefault(
                    edge => edge != null
                );
            
            if (oldEdge == null) // all edges have been assigned to a vertex
                break;
            
            while (true)
            {
                bool TryAddEdgeToVertex(ModelSurfaceSide modelSurfaceSide)
                {
                    if (modelSurfaceSide.vertexIndex == vertexIndex)
                    {
                        if (modelSurfaceSide != vertex.boundaryCurves[0])
                            throw new Exception("Check your polygon! The supposed cyclic order of edges closed back in on itself, but not where it should.");
                        return false; // we have come full circle, this vertex is finished
                    }
                
                    if (modelSurfaceSide.vertexIndex != -1)
                        throw new Exception("Check your polygon! Some edges seem to be assigned to more than one vertex.");
            
                    if (vertex.boundaryCurves.Contains(modelSurfaceSide))
                        throw new Exception("what the heck");
                    
                    if (vertex.boundaryCurves.Count > 3 * sideCurves.Count)
                        throw new Exception("what the heck?");
                    
                    modelSurfaceSide.vertexIndex = vertexIndex;
                    vertex.boundaryCurves.Add(modelSurfaceSide);
                    
                    return true;
                }
                
                if (!TryAddEdgeToVertex(oldEdge)) break;

                var edgesAtCurrentPolygonVertex = polygonVertices.First(
                    pair => pair.Item2.Contains(oldEdge)
                ).Item2;
                
                var i = edgesAtCurrentPolygonVertex.IndexOf(oldEdge);
                var turnDirection = oldEdge.rightIsInside ? +1 : -1;
                var j = i + turnDirection;
                if (j < 0)
                    j += edgesAtCurrentPolygonVertex.Count; // modulo in C# is not always positive...
                if (j == edgesAtCurrentPolygonVertex.Count)
                    j = 0;
                var newEdge = edgesAtCurrentPolygonVertex[j];
                
                if (newEdge.rightIsInside == oldEdge.rightIsInside)
                    throw new Exception("Check your polygon! Two edges next to each other on a vertex don't agree on what is the inside of the polygon.");
                
                var angle = newEdge.angle - oldEdge.angle;
                if (angle < 0)
                    angle += tau;
                vertex.angles.Add(angle);

                // we add both edges to the vertex (i.e. this and other, then we continue turning)
                if (!TryAddEdgeToVertex(newEdge)) break; 

                oldEdge = newEdge.other;
                
            }

        }
        
        #endregion
        
        // todo: think about this ordering
        var vert = vertices.OrderByDescending(vertex => Mathf.Abs(tau / vertex.angles.Sum() - 1));
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
    
    public override Curve GetGeodesic(Point startPoint, Point endPoint, string name)
    {
        var start = startPoint.Position;
        var end = endPoint.Position; 
        // todo: Select best fit points! (in part. for points on the boundary)
        // todo: implement going through boundary!
        return BaseGeometrySurfaces[geometryType].GetGeodesic((BasicPoint) start, (BasicPoint) end, name);
    }
    

    public override Point ClampPoint(Vector3? point) // todo: this is extremely inefficient
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
            Vector3 diff = side.curve[t].Position - p; // todo: difference for points
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

    public override Matrix3x3 BasisAt(Vector3 position) => Matrix3x3.Identity;
}


public class ModelSurfaceSide: Curve
{
    public readonly Curve curve;
    public readonly bool rightIsInside;
    public readonly float angle;
    public int vertexIndex = -1;
    public ModelSurfaceSide other;

    public ModelSurfaceSide(Curve curve, bool rightIsInside)
    {
        this.curve = curve;
        this.rightIsInside = rightIsInside;
        angle = Mathf.Atan2(curve.StartVelocity.y, curve.StartVelocity.x);
    }

    public ModelSurfaceSide(ModelSurface.PolygonSide side, GeometryType geometryType, Surface surface) : this(
        ModelSurface.BaseGeometrySurfaces[geometryType]
            .GetGeodesic((BasicPoint) side.start, (BasicPoint) side.end, side.label),
        side.rightIsInside
    )
    {
        Surface = surface;
    }

    [CanBeNull] private string _name;
    public override string Name
    {
        get => _name ?? curve.Name + '\'';
        set => _name = value;
    }
    public override float Length => curve.Length;
    public override Point EndPosition => curve.EndPosition;
    public override Point StartPosition => curve.StartPosition;
    public override Vector3 EndVelocity => curve.EndVelocity;
    public override Vector3 StartVelocity => curve.StartVelocity;
    public override Surface Surface { get; }
    public override Point ValueAt(float t) => new ModelSurfaceBoundaryPoint(this, t);

    public override Vector3 DerivativeAt(float t) => curve.DerivativeAt(t); // todo: TangentVectors with more than one position (and value)

    private ModelSurfaceSide reverseSide;

    public override Curve Reverse() => ReverseModelSide();

    public ModelSurfaceSide ReverseModelSide()
    {
        if (reverseSide != null) // this method will be called in AddOther from below in other.Reverse()!
            return reverseSide;
        reverseSide = new(curve.Reverse(), !rightIsInside);
        reverseSide.reverseSide = this;
        reverseSide.AddOther(other.ReverseModelSide());
        
        return reverseSide;
    }

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        if (homeomorphism.isIdentity)
            return this;
        if (homeomorphism.target is ModelSurface)
            return new ModelSurfaceSide(curve.ApplyHomeomorphism(homeomorphism), rightIsInside);
        
        // todo: think about orientation, other
        return curve.ApplyHomeomorphism(homeomorphism);
    }

    public void AddOther(ModelSurfaceSide newOtherSide)
    {
        if (other != null && other != newOtherSide)
            throw new Exception("Check your polygon! Three sides have the same label.");
        other = newOtherSide;
        other.other = this;
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

public class HyperbolicPlane : GeodesicSurface
{
    public HyperbolicPlane(string name = "Hyperbolic Plane") : base(name, 0, true){}
    public override Point ClampPoint(Vector3? point) => 
        point.HasValue ? (BasicPoint)(Vector2) point : null;

    public override Matrix3x3 BasisAt(Vector3 position) => Matrix3x3.Identity;

    public override Vector3 MinimalPosition { get; } = new(float.NegativeInfinity, float.NegativeInfinity);
    public override Vector3 MaximalPosition { get; } = new(float.PositiveInfinity, float.PositiveInfinity);
    
    public override Curve GetGeodesic(Point start, Point end, string name)
        => new HyperbolicGeodesicSegment(start.Position, end.Position, this, name);
}

public class EuclideanPlane : GeodesicSurface
{
    public EuclideanPlane(string name = "Euclidean Plane") : base(name, 0, true){}
    public override Point ClampPoint(Vector3? point) => 
        point.HasValue ? (BasicPoint)(Vector2) point : null;

    public override Matrix3x3 BasisAt(Vector3 position) => Matrix3x3.Identity;
    
    public override Vector3 MinimalPosition { get; } = new(float.NegativeInfinity, float.NegativeInfinity);
    public override Vector3 MaximalPosition { get; } = new(float.PositiveInfinity, float.PositiveInfinity);

    public override Curve GetGeodesic(Point start, Point end, string name)
        => new FlatGeodesicSegment(start.Position, end.Position, this, name);
}


using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework.Constraints;
using UnityEditor.Analytics;
using UnityEngine;
using Random = UnityEngine.Random;


public partial class ModelSurface: GeodesicSurface
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
    public readonly List<ModelSurfaceSide> sides = new();
    public IEnumerable<ModelSurfaceSide> AllSideCurves => sides.Concat(
        from side in sides
        select side.other
    );
    
    public readonly List<ModelSurfaceVertex> vertices = new();
    public readonly List<PolygonSide> sidesAsParameters;
    private readonly GeodesicSurface geometrySurface; // Euclidean or Hyperbolic plane.
    
    public static readonly Dictionary<GeometryType, GeodesicSurface> BaseGeometrySurfaces = new()
    {
        [GeometryType.Flat] = new EuclideanPlane(),
        [GeometryType.HyperbolicDisk] = new HyperbolicPlane(diskModel: true),
        [GeometryType.HyperbolicPlane] = new HyperbolicPlane(diskModel: false),
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
            var oldSide = this.sides.FirstOrDefault(existingSide => existingSide.Name == newSide.Name);
            if (oldSide != null)
                oldSide?.AddOther(newSide);
            else
                sides.Add(newSide);
        }
        if (sides.Any(side => side.other == null))
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
        
        foreach (var curve in this.AllSideCurves)
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
            var vertexIndex = vertices.Count;
            
            if (vertexIndex > 4 * sides.Count)           
                throw new Exception("what the heck?");
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


            var vertex = new ModelSurfaceVertex();
            vertices.Add(vertex);
            

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
                    
                    if (vertex.boundaryCurves.Count > 4 * sides.Count)
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
    /// This actually doesn't necessarily preserve the locations of the old punctures (if newly added corners have worse
    /// angles than the ones already there)
    /// </summary>
    /// <param name="addedGenus"></param>
    /// <param name="addedPunctures"></param>
    /// <param name="extraBoundaries"></param>
    public ModelSurface WithAddedBoundaries(int addedGenus, int addedPunctures, IEnumerable<PolygonSide> extraBoundaries)
    {
        return new(Name, Genus + addedGenus, punctures.Count + addedPunctures, geometryType,
            sidesAsParameters.Concat(extraBoundaries).ToList());
        
        // todo: implement (this should be basically the same as the constructor and be called from there)
        // todo: check if the inside of the polygon agrees! All rays (from boundary curves) into the "inside" should hit boundary curves on their "inside" side!
        Genus += addedGenus;
        // the corresponding homeomorphisms must be defined elsewhere (where this is called from)
    }
    
    public override Curve GetGeodesic(Point startPoint, Point endPoint, string name)
    {
        GeodesicSurface baseGeometrySurface = BaseGeometrySurfaces[geometryType];
        if (startPoint is not IModelSurfacePoint)
            startPoint = ClampPoint(startPoint.Position);
        if (endPoint is not IModelSurfacePoint)
            endPoint = ClampPoint(endPoint.Position);

        var (_, centerPoint) = DistanceMinimizer(startPoint, endPoint, baseGeometrySurface);
        if (centerPoint == null)
            return baseGeometrySurface.GetGeodesic(startPoint, endPoint, name);
        
        var ((_, firstCenterPosition), _) = startPoint.ClosestPosition(centerPoint);
        var ((_, secondCenterPosition), _) = endPoint.ClosestPosition(centerPoint);
        if (secondCenterPosition == firstCenterPosition)
            Debug.Log("Weird reflection at the boundary. For some reason it thought that going to the boundary is efficient, but we actually don't go through it because that takes longer.");
        var firstSegment = baseGeometrySurface.GetGeodesic(startPoint, firstCenterPosition, name + "pt 1");
        var secondSegment = baseGeometrySurface.GetGeodesic(secondCenterPosition, endPoint, name + "pt 2");
        return new ConcatenatedCurve(new[] { firstSegment, secondSegment }, name); // .Smoothed(); // TODO: this doesn't work as expected
    }

    private (float, Point) DistanceMinimizer(Point startPoint, Point endPoint, GeodesicSurface baseGeometrySurface)
    {
        var shortestLength = baseGeometrySurface.DistanceSquared(startPoint, endPoint);
        Point result = null;
        if (startPoint is not IModelSurfacePoint start || endPoint is not IModelSurfacePoint end)
            throw new Exception("Start and end point should have the type IModelSurfacePoint");
        foreach (var side in sides)
        {
            var (a, b) = start.ClosestBoundaryPoints(side);
            var (c, d) = end.ClosestBoundaryPoints(side);
            if (a == null || b == null || c == null || d == null)
                throw new Exception("Lazy Programmer!");
                
            float LengthVia(ModelSurfaceBoundaryPoint x) =>
                baseGeometrySurface.DistanceSquared(x, startPoint) + baseGeometrySurface.DistanceSquared(x, endPoint); // this minimizes over the positions
                
            ModelSurfaceBoundaryPoint[] points = {
                a, b, c, d,
                new ModelSurfaceBoundaryPoint(side, (a.t + d.t) / 2 ),
                new ModelSurfaceBoundaryPoint(side.other, (b.t + c.t) / 2 )
            };
                
            var (minPoint, distance) = points.ArgMin(LengthVia);

            if (!(distance < shortestLength)) continue;
            shortestLength = distance;
            result = minPoint;
        }
        return (shortestLength, result);
    }

    public override float DistanceSquared(Point startPoint, Point endPoint) => throw new NotImplementedException();

    public override Point ClampPoint(Vector3? point) // todo: this is extremely inefficient
    {
        const float closenessThreshold = 0.01f; // todo: this should vary with the camera scale!
        
        if (point == null) return null;
        var p = point.Value; 
        Point res = p;

        var distances = (
            from side in AllSideCurves 
            let x = side.curve.GetClosestPoint(p) 
            let pt = new ModelSurfaceBoundaryPoint(side, x.Item1)
            select (pt, x.Item2.DistanceSquared(res))
        ).ToArray();
        var closeness = distances.Min(x => x.Item2);
        var closestPoints = from x in distances where x.Item2 < closeness + closenessThreshold select x.Item1;
        
        foreach (var closestPt in closestPoints)
        {
            var closestSide = closestPt.side;
            float time = closestPt.t;
            if (closeness < closenessThreshold)
            {
                if (time.ApproximateEquals(0))
                    res = vertices[closestSide.vertexIndex];
                else if (time.ApproximateEquals(closestSide.curve.Length))
                    res = vertices[closestSide.other.vertexIndex];
                else res = closestPt;
            }
            var (_, forward) = closestSide.curve.DerivativeAt(time);
            var right = new Vector3(forward.y, -forward.x); // orientation!
            var rightness = Vector3.Dot(right, p - closestPt.Position);
            if (rightness < 0 && closestSide.rightIsInside || rightness > 0 && !closestSide.rightIsInside)
            { // this is outside
                return null;
            }
        }

        return new ModelSurfaceInteriorPoint(p, distances.Select(x => x.Item1).ToList());
    }

    /// <summary>
    /// This is the constant basis (e_x, e_y, -e_z) of the model surface. The normal is pointing towards the camera.
    /// This is opposite oriented, i.e. right-handed, because Unity uses a left-handed coordinate system.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public override TangentSpace BasisAt(Point position) => new(position, Matrix3x3.InvertZ);
}

public class ModelSurfaceInteriorPoint : BasicPoint, IModelSurfacePoint
{
    private List<ModelSurfaceBoundaryPoint> closestBoundaryPoints;
    public ModelSurfaceInteriorPoint(Vector3 position, List<ModelSurfaceBoundaryPoint> closestBoundaryPoints): base(position)
    {
        this.closestBoundaryPoints = closestBoundaryPoints;
    }

    public (ModelSurfaceBoundaryPoint, ModelSurfaceBoundaryPoint) ClosestBoundaryPoints(ModelSurfaceSide side)
    {
        ModelSurfaceBoundaryPoint a = null, b = null;
        foreach (var boundaryPoint in closestBoundaryPoints)
        {
            if (boundaryPoint.side == side)
                a = boundaryPoint;
            else if (boundaryPoint.side == side.other)
                b = boundaryPoint;
        }
        return (a, b);
    }
}

public interface IModelSurfacePoint
{
    (ModelSurfaceBoundaryPoint, ModelSurfaceBoundaryPoint) ClosestBoundaryPoints(ModelSurfaceSide side);
}


public class ModelSurfaceSide: Curve
{
    public readonly Curve curve;
    public readonly bool rightIsInside;
    public readonly float angle;
    public int vertexIndex = -1;
    public ModelSurfaceSide other;
    private ModelSurfaceSide reverseSide;

    public ModelSurfaceSide(Curve curve, bool rightIsInside)
    {
        this.curve = curve;
        this.rightIsInside = rightIsInside;
        angle = Mathf.Atan2(curve.StartVelocity.vector.y, curve.StartVelocity.vector.x);
    }

    public ModelSurfaceSide(ModelSurface.PolygonSide side, GeometryType geometryType, Surface surface) :
        this(
        ModelSurface.BaseGeometrySurfaces[geometryType]
            .GetGeodesic( side.start, side.end, side.label),
        side.rightIsInside
    )
    {
        Surface = surface;
    }

    [CanBeNull] private string _name;
    public override string Name
    {
        get => _name ?? curve.Name;// + '\'';
        set => _name = value;
    }
    public override float Length => curve.Length;
    public override Point EndPosition => curve.EndPosition;
    public override Point StartPosition => curve.StartPosition;
    public override TangentVector EndVelocity => curve.EndVelocity;
    public override TangentVector StartVelocity => curve.StartVelocity;
    public override Surface Surface { get; }

    public override Point ValueAt(float t) => new ModelSurfaceBoundaryPoint(this, t);

    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t);

    public void AddOther(ModelSurfaceSide newOtherSide)
    {
        if (other == newOtherSide) return;
        if (other != null)
            throw new Exception("Check your polygon! Three sides have the same label.");
        other = newOtherSide;
        other.other = this;
        other.Color = Color;
        if (other.rightIsInside == rightIsInside)
            Debug.Log("Check your polygon! Two sides with the same label have the surface on the same side." +
                      " This might mean that it is not orientable?");
        Debug.Log($"side {Name} (at {StartVelocity} got other side {other.Name} at {other.StartVelocity} with color {Color}");
    }
    
    public override Curve Reverse() => ReverseModelSide();
    
    /// <summary>
    /// The closest point on either this or the other curve.
    /// The Position (i.e. Positions.First()) will be the on the curve out of the two identified ones that is closest
    /// (this is the SideCurve that is returned inside the Point which is the ModelSurfaceBoundaryPoint).
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public override (float, Point) GetClosestPoint(Vector3 point)
    {
        var (t, closestPoint) = curve.GetClosestPoint(point);
        var (t2, closestPoint2) = other.curve.GetClosestPoint(point);
        if (closestPoint.DistanceSquared(point) < closestPoint2.DistanceSquared(point))
            return (t, this[t]);
        return (t2, other[t2]);
    }

    public ModelSurfaceSide ReverseModelSide()
    {
        if (reverseSide != null) // this method will be called in AddOther from below in other.Reverse()!
            return reverseSide;
        reverseSide = new(curve.Reverse(), !rightIsInside);
        reverseSide.reverseSide = this;
        reverseSide.Color = Color;
        reverseSide.AddOther(other.ReverseModelSide());
        
        return reverseSide;
    }

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        if (homeomorphism.isIdentity)
            return this;
        if (homeomorphism.target is not ModelSurface) 
            return curve.ApplyHomeomorphism(homeomorphism);
        
        bool orientationReversing = homeomorphism.df(StartPosition.Position).Determinant() < 0;
        
        var result = new ModelSurfaceSide(curve.ApplyHomeomorphism(homeomorphism), 
            orientationReversing ? rightIsInside : !rightIsInside);
        result.other = new ModelSurfaceSide(other.curve.ApplyHomeomorphism(homeomorphism),
            orientationReversing ? other.rightIsInside : !other.rightIsInside);
        result.other.other = result;
        return result;
    }
    
}

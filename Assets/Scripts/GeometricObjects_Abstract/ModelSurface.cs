using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Constraints;
using UnityEditor.Analytics;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;


public partial class ModelSurface: GeodesicSurface
{
    const float tau = 2 * Mathf.PI;

    #region local types
    public record PolygonSide
    {
        private static IEnumerator<Color> colors = Curve.colors.EndlessLoop().GetEnumerator();
        
        public readonly string label;
        public readonly Vector2 start, end;
        public readonly bool rightIsInside;
        public readonly Color color;
        public PolygonSide(string label, Vector2 start, Vector2 end, bool rightIsInside, Color color)
        {
            this.label = label;
            this.start = start;
            this.end = end;
            this.rightIsInside = rightIsInside;
            this.color = color;
        }
        
        public static Color NextColor() => colors.MoveNext() ? colors.Current : Color.white;

        public PolygonSide ApplyHomeomorphism(Homeomorphism homeomorphism, string labelAddition = "") =>
            new(
                label + labelAddition,
                homeomorphism.f(start),
                homeomorphism.f(end),
                rightIsInside,
                color
            );
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
    
    public static readonly Dictionary<GeometryType, GeodesicSurface> BaseGeometrySurfaces = new()
    {
        [GeometryType.Flat] = new EuclideanPlane(),
        [GeometryType.HyperbolicDisk] = new HyperbolicPlane(diskModel: true),
        [GeometryType.HyperbolicPlane] = new HyperbolicPlane(diskModel: false),
        [GeometryType.Spherical] = null // todo?
    };
    
    public override Vector3 MinimalPosition { get; }
    public override Vector3 MaximalPosition { get; }

    protected GeodesicSurface GeometrySurface => BaseGeometrySurfaces[geometryType]; // Euclidean or Hyperbolic plane.

    public ModelSurface Copy(string name) => new(name, Genus, punctures.Count, geometryType, sidesAsParameters);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="homeomorphism">A homeomorphism of the base surface. The geometry type is set depending on if this is HyperbolicPlane etc.</param>
    /// <param name="labelAddition"></param>
    /// <returns></returns>
    public ModelSurface Copy(string name, Homeomorphism homeomorphism, string labelAddition = "") =>
        new(name,
            Genus, 
            punctures.Count, 
            geometryType: homeomorphism.target switch
            {
                HyperbolicPlane { diskModel: true } => GeometryType.HyperbolicDisk,
                HyperbolicPlane { diskModel: false } => GeometryType.HyperbolicPlane,
                EuclideanPlane => GeometryType.Flat,
                _ => geometryType
            }, 
            (
                from sideParameter in sidesAsParameters
                select sideParameter.ApplyHomeomorphism(homeomorphism, labelAddition)    
            ).ToList()
    );

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
            newSide.Color = side.color;
            var oldSide = this.sides.FirstOrDefault(existingSide => existingSide.Name == newSide.Name);
            if (oldSide != null)
            {
                oldSide?.AddOther(newSide);
                newSide.Name += "*";
            }
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
            ).Take(punctures));
        if (this.punctures.Count == punctures)
            return;
        var center = polygonVertices.Select(pair => pair.Item1).Aggregate((a, b) => a + b) / polygonVertices.Count;
        var radius = polygonVertices.Select(pair => pair.Item1).Max(pos => Vector3.Distance(pos, center));
        
        this.punctures.AddRange((
                from _ in Enumerable.Range(0, 100 * (punctures - this.punctures.Count)) 
                let randomPuncture = ClampPoint((Vector3) Random.insideUnitCircle * radius + center, 0.01f) 
                // todo: this MUST NOT be disjoint from the surface. Also it should be approximately the size of the surface
                where randomPuncture != null
                select randomPuncture
            ).Take(punctures - this.punctures.Count)
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
        // the corresponding homeomorphisms must be defined elsewhere (where this is called from)
    }
    
    public Curve GetBasicGeodesic(Point startPoint, Point endPoint, string name, GeodesicSurface surface = null) => GeometrySurface.GetGeodesic(startPoint, endPoint, name, surface ?? this);
    public Curve GetBasicGeodesic(TangentVector tangentVector, float length, string name, GeodesicSurface surface = null) => GeometrySurface.GetGeodesic(tangentVector, length, name, surface ?? this);
    
    public override Curve GetGeodesic(Point startPoint, Point endPoint, string name, GeodesicSurface surface = null)
    {
        if (startPoint is not IModelSurfacePoint)
            startPoint = ClampPoint(startPoint.Position, 0.001f);
        if (startPoint is null)
            throw new Exception("The start point is not on the surface.");
        if (endPoint is not IModelSurfacePoint)
            endPoint = ClampPoint(endPoint.Position, 0.001f);
        if (endPoint is null)
            throw new Exception("The end point is not on the surface.");

        var centerPoint = DistanceMinimizer(startPoint, endPoint, GeometrySurface);
        if (centerPoint == null)
            return GetBasicGeodesic(startPoint, endPoint, name, surface);
        
        var ((_, firstCenterPosition), _) = startPoint.ClosestPosition(centerPoint);
        var ((_, secondCenterPosition), _) = endPoint.ClosestPosition(centerPoint);
        if (secondCenterPosition == firstCenterPosition) // else we would go from centerPoint.Position to centerPoint.Position
            Debug.Log("Weird reflection at the boundary. For some reason it thought that going to the boundary is efficient, but we actually don't go through it because that takes longer.");
        if (firstCenterPosition != centerPoint.Position) // else, we actually want to go from centerPoint.Positions[1] to centerPoint.Position[2]
            centerPoint = centerPoint.SwitchSide(); // we want to go from centerPoint.Position[2] to centerPoint.Position[1]
        var firstSegment = GetBasicGeodesic(startPoint, centerPoint, name + "pt 1", surface);
        var secondSegment = GetBasicGeodesic(centerPoint.SwitchSide(), endPoint, name + "pt 2", surface);
        return new ConcatenatedCurve(new[] { firstSegment, secondSegment },
            name); //.Smoothed(); // todo?: this doesn't work as expected. Shouldn't be necessary, bc. there shouldn't really be an angle jump
    }

    public override Curve GetGeodesic(TangentVector startVelocity, float length, string name, GeodesicSurface surface = null)
    {
        if (length < 0) 
            return GetGeodesic(-startVelocity, -length, name, surface);
        List<Curve> segments = new();
        var currentStartVector = startVelocity;
        int i = 0;
        Vector3 lastPos = startVelocity.point.Position;
        while (length > 0)
        {
            var currentSegment = GetBasicGeodesic(currentStartVector, length, name + $"pt {i}", surface);
            float res = 0.1f;
            float t = res; // we shouldn't need to check at 0 (it should be the last start point)
            while (res >= length)
                res /= 2;
            Point p = null;
            bool stillAtTheStartingSide = true;
            while (t < length)
            {
                p = ClampPoint(currentSegment[t], 0.001f);
                switch (p)
                {
                    case null:
                    {
                        res /= 4;
                        if (res < 1e-8f)
                            throw new Exception(
                                "Didn't hit the boundary exactly when running along a geodesic! Aborting.");
                        t -= res * 3;
                        stillAtTheStartingSide = false;
                        continue;
                    }
                    case ModelSurfaceInteriorPoint:
                        stillAtTheStartingSide = false;
                        t += res;
                        continue;
                    case ModelSurfaceVertex:
                    case ModelSurfaceBoundaryPoint:
                        if (!stillAtTheStartingSide) 
                            goto afterWhileLoop; // break does the same as continue
                        t += res;
                        continue;
                    default:
                        throw new Exception($"Weird type of clamped point: {p}");
                }
            }
            afterWhileLoop:
            
            i++;
            if (length < t)
                t = length;
            length -= t;
            if (t > 0)
                segments.Add(currentSegment.Restrict(0, t));
            if (length <= 0)
                break;
            
            var endTangentVec = currentSegment.DerivativeAt(t);
            Vector3 pPos = p.Position;
            Vector3 previousLastPos = lastPos;
            lastPos = pPos;
            if (previousLastPos.ApproximatelyEquals(pPos))
                Debug.LogWarning($"Weird: The boundary point {p} was visited twice in succession by the geodesic starting at {startVelocity}, the second time after time {t:g2}, and both times from the same side!?");
            if (p is ModelSurfaceBoundaryPoint pt)
                p = pt.SwitchSide();
            if (p is ModelSurfaceVertex vertex)
                p = vertex.Positions.ElementAt(vertex.Positions.Count() - 1); // todo? not actually thought through, use angles!
            if (pPos.ApproximatelyEquals(p))
                throw new Exception("Bug: The position wasn't updated to the other side.");
            currentStartVector = new(p, p.PassThrough(0, 1, endTangentVec.vector));
        }
        return new ConcatenatedCurve(segments, name);
    }

    private ModelSurfaceBoundaryPoint DistanceMinimizer(Point startPoint, Point endPoint, GeodesicSurface baseGeometrySurface)
    {
        if (startPoint is not IModelSurfacePoint start || endPoint is not IModelSurfacePoint end)
            throw new("Start and end point should have the type IModelSurfacePoint");
        var (optimalSide, _) = DistanceMinimizingDeckTransformation1Side(startPoint, endPoint);
        var geodesic = baseGeometrySurface.GetGeodesic(startPoint, endPoint.ApplyHomeomorphism(optimalSide.DeckTransformation()), "DistanceMinimizer");
        var a = start.ClosestBoundaryPoints(optimalSide);
        var b = end.ClosestBoundaryPoints(optimalSide.other);
        var t1 = Mathf.Sqrt( a.DistanceSquared(startPoint) );
        var t2 = Mathf.Sqrt(  b.DistanceSquared(endPoint) );
        var t = (t1 + (geodesic.Length - t2)) / 2;
        t = Mathf.Clamp(t, 0, geodesic.Length);
        var intersectionPoint = ClampPoint(geodesic[t], Mathf.Max(t, geodesic.Length - t) / 5f, allowVertices: false);
        if (intersectionPoint is ModelSurfaceBoundaryPoint sidePoint)
            return sidePoint;
        Debug.LogError($"Intersection point {intersectionPoint} is not a ModelSurfaceBoundaryPoint, but {intersectionPoint.GetType()}");
        return null;
    }

    private (ModelSurfaceSide optimalSide, float shortestLength) DistanceMinimizingDeckTransformation1Side(Point startPoint,
        Point endPoint)
    {
        ModelSurfaceSide optimalSide = null;
        float shortestLength = GeometrySurface.DistanceSquared(startPoint, endPoint);
        foreach (var side in AllSideCurves)
        {
            float distance = GeometrySurface.DistanceSquared(startPoint, endPoint.ApplyHomeomorphism(side.DeckTransformation()));

            if (!(distance < shortestLength)) continue;
            shortestLength = distance;
            optimalSide = side;
        }
        return (optimalSide, shortestLength);
    }

    private ModelSurfaceBoundaryPoint DistanceMinimizerOld(Point startPoint, Point endPoint, GeodesicSurface baseGeometrySurface)
    {
        // removed because of TODO: Take the deck transformation φ on the base surface that belongs to crossing this side and return the (projection) of the geodesic on the base surface from start to φ(end).

        var shortestLength = baseGeometrySurface.DistanceSquared(startPoint, endPoint);
        ModelSurfaceBoundaryPoint result = null;
        if (startPoint is not IModelSurfacePoint start || endPoint is not IModelSurfacePoint end)
            throw new Exception("Start and end point should have the type IModelSurfacePoint");
        foreach (var side in sides)
        {
            var a = start.ClosestBoundaryPoints(side);
            var b = start.ClosestBoundaryPoints(side.other);
            var c = end.ClosestBoundaryPoints(side);
            var d = end.ClosestBoundaryPoints(side.other);
            if (a == null || b == null || c == null || d == null)
                throw new Exception("Lazy Programmer!");

            ModelSurfaceBoundaryPoint[] points = {
                a, b, c, d,
                new ModelSurfaceBoundaryPoint(side, (a.t + d.t) / 2 ),
                new ModelSurfaceBoundaryPoint(side.other, (b.t + c.t) / 2 )
            };
                
            var (minPoint, distance) = points.ArgMin(LengthVia);

            if (!(distance < shortestLength)) continue;
            shortestLength = distance;
            result = minPoint;
            continue;

            float LengthVia(ModelSurfaceBoundaryPoint x) =>
                baseGeometrySurface.DistanceSquared(x, startPoint) + baseGeometrySurface.DistanceSquared(x, endPoint); // this minimizes over the positions
        }
        return result;
    }

    public override float DistanceSquared(Point startPoint, Point endPoint) => 
        DistanceMinimizingDeckTransformation1Side(startPoint, endPoint).Item2;
    // sped up by 50 times from using GetGeodesic(....).Length ^ 2

    public override Point ClampPoint(Vector3? point, float closenessThreshold) => 
        ClampPoint(point, closenessThreshold, allowVertices: true);

    private Point ClampPoint(Vector3? pos, float closenessThreshold, bool allowVertices) 
    {
        // todo: Performance. This is extremely inefficient and actually takes most of the time of the whole program.
        const float secondaryClosestSideSquareDistanceFactor = 1.5f;
        if (pos == null) return null;
        var p = pos.Value; 
        Point res = null;
        Point point = p;
        
        var distances = (
            from side in AllSideCurves 
            let t = side.curve.GetClosestPoint(p) // this is where the most time is spent!
            let pt = new ModelSurfaceBoundaryPoint(side, t) // = side[t]
            select (pt, side.curve[t].DistanceSquared(point)) // here pt.DistanceSquared(point) would minimize over this side and the other side, so it would give the same result for both connected sides (which would be wrong)
        ).ToArray();
        var bestCloseness = distances.Min(x => x.Item2);
        var closestPoints = from x in distances where x.Item2 <= bestCloseness * secondaryClosestSideSquareDistanceFactor select x;
        foreach (var (closestPt, closeness) in closestPoints)
        {
            var closestSide = closestPt.side;
            float time = closestPt.t;
            
            if (closeness < closenessThreshold)
            {
                if (res is ModelSurfaceVertex) continue;
                res = allowVertices switch
                {
                    true when time.ApproximateEquals(0) => vertices[closestSide.vertexIndex],
                    true when time.ApproximateEquals(closestSide.curve.Length) => vertices[
                        closestSide.other.vertexIndex],
                    _ => closestPt
                };
                continue;
            }
            // closestPtPosition == closestPt (but as a BasicPoint probably, so it is already calculated)
            var (closestPtPosition, forward) = closestSide.curve.DerivativeAt(time); 
            var right = new Vector3(forward.y, -forward.x); // orientation!
            var rightness = Vector3.Dot(right, p - closestPtPosition.Position);
            if (rightness < 0 && closestSide.rightIsInside || rightness > 0 && !closestSide.rightIsInside)
            { // this is outside
                return null;
            }
        }

        return res ?? new ModelSurfaceInteriorPoint(p, distances.Select(x => x.Item1).ToList());
    }

    /// <summary>
    /// This is the constant basis (e_x, e_y, -e_z) of the model surface. The normal is pointing towards the camera.
    /// This is opposite oriented, i.e. right-handed, because Unity uses a left-handed coordinate system.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public override TangentSpace BasisAt(Point position) => new(position, Matrix3x3.InvertZ);
    
    public Homeomorphism SwitchHyperbolicModel()
    {
        Homeomorphism baseHomeomorphism;
        string nameAddition;
        switch (geometryType)
        {
            case GeometryType.HyperbolicDisk:
                baseHomeomorphism = HyperbolicPlane.CayleyTransform.Inverse;
                nameAddition = " [halfplane]";
                break;
            case GeometryType.HyperbolicPlane:
                baseHomeomorphism = HyperbolicPlane.CayleyTransform;
                nameAddition = " [disk]";
                break;
            default:
                return null;
        }
        var newSurface = Copy(Name + nameAddition, baseHomeomorphism, nameAddition);
        return new Homeomorphism(this, newSurface, baseHomeomorphism.f, baseHomeomorphism.fInv, baseHomeomorphism.df,
            baseHomeomorphism.dfInv, "Cayley transform");
    }

    public Homeomorphism ToKleinModel()
    {
        if (geometryType != GeometryType.HyperbolicDisk)
            return null;
        Homeomorphism baseHomeomorphism = HyperbolicPlane.ToKleinModel;
        var nameAddition = " [Klein]";
        var newSurface = Copy(Name + nameAddition, baseHomeomorphism, nameAddition);
        return new Homeomorphism(this, newSurface, baseHomeomorphism.f, baseHomeomorphism.fInv, baseHomeomorphism.df,
            baseHomeomorphism.dfInv, "Disk To Klein model");
    }
}

public class ModelSurfaceInteriorPoint : BasicPoint, IModelSurfacePoint
{
    private List<ModelSurfaceBoundaryPoint> closestBoundaryPoints;
    public ModelSurfaceInteriorPoint(Vector3 position, List<ModelSurfaceBoundaryPoint> closestBoundaryPoints): base(position)
    {
        this.closestBoundaryPoints = closestBoundaryPoints;
    }

    public ModelSurfaceBoundaryPoint ClosestBoundaryPoints(ModelSurfaceSide side) => 
        closestBoundaryPoints.First(boundaryPoint => boundaryPoint.side == side);
}

public interface IModelSurfacePoint
{
    ModelSurfaceBoundaryPoint ClosestBoundaryPoints(ModelSurfaceSide side);
}
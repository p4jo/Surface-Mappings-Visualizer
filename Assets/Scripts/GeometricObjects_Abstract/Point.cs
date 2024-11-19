using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public interface ITransformable
{
    public ITransformable ApplyHomeomorphism(Homeomorphism homeomorphism);
    
    public static ITransformable operator *(Homeomorphism homeo, ITransformable input) =>
        input.ApplyHomeomorphism(homeo);
}

public interface ITransformable<T>: ITransformable where T : ITransformable
{
    public new T ApplyHomeomorphism(Homeomorphism homeomorphism);
    ITransformable ITransformable.ApplyHomeomorphism(Homeomorphism homeomorphism) => ApplyHomeomorphism(homeomorphism);

    public static T operator *(Homeomorphism homeo, ITransformable<T> input) =>
        input.ApplyHomeomorphism(homeo);
}

public abstract class Point : IEquatable<Point>, ITransformable<Point>
{
    public virtual Vector3 Position => Positions.First();
    public abstract IEnumerable<Vector3> Positions { get; }
    public virtual Vector3 PassThrough(int fromPositionIndex, int toPositionIndex, Vector3 direction) => direction;

    public abstract Point ApplyHomeomorphism(Homeomorphism homeomorphism);

    public virtual bool Equals(Point other) => 
        other != null && 
        Positions.Any(
            position => 
                other.Positions.Any(
                    otherPosition => otherPosition.ApproximatelyEquals(position)
                ));
    
    public (int, int, float) ClosestPositionIndices(Point other)
    {
        var (n, dist) = Positions.CartesianProduct(other.Positions)
            .ArgMinIndex(positions => (positions.Item1 - positions.Item2).sqrMagnitude);
        int count = other.Positions.Count();
        return (n / count, n % count, dist);
    }

    public virtual ((Vector3, Vector3), float) ClosestPosition(Point other) =>
        Positions.CartesianProduct(other.Positions).ArgMin(positions => (positions.Item1 - positions.Item2).sqrMagnitude);

    /// <summary>
    /// This is not really useful at the moment, as secondary positions are not used in curves (is the number of positions constant along the curve ...?)
    /// </summary>
    public virtual float Distance(Point other) => 
        Positions.CartesianProduct(other.Positions).Min(positions => (positions.Item1 - positions.Item2).sqrMagnitude);
    
    public static implicit operator Point(Vector3 v) => new BasicPoint(v);
    public static implicit operator Point(Vector2 v) => new BasicPoint(v);
    public static implicit operator Vector3(Point p) => p.Position;
    

}

public class BasicPoint : Point
{
    public override Vector3 Position { get; }
    public override IEnumerable<Vector3> Positions => new[] { Position };

    public BasicPoint(Vector3 position)
    {
        Position = position;
    }

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        var result = homeomorphism.F(Position);
        if (!result.HasValue)
            throw new Exception("Homeomorphism is not defined on this point");
        return new BasicPoint(result.Value);
    }

    public static implicit operator BasicPoint(Vector3 v) => new(v);
    public static implicit operator BasicPoint(Vector2 v) => new(v);

}

/// <summary>
/// This is a point that is where two curves don't meet, but are concatenated... This shouldn't actually happen.
/// </summary>
public class ConcatenationSingularPoint: Point
{
    public Curve incomingCurve, outgoingCurve;
    public int incomingPosIndex, outgoingPosIndex;

    public override IEnumerable<Vector3> Positions =>
        incomingCurve.EndPosition.Positions.Concat(outgoingCurve.StartPosition.Positions);

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        return new ConcatenationSingularPoint()
        {
            incomingCurve = incomingCurve.ApplyHomeomorphism(homeomorphism),
            outgoingCurve = outgoingCurve.ApplyHomeomorphism(homeomorphism)
        };
    }
}


public class ModelSurfaceVertex : Point
{
    /// <summary>
    /// a, b, b.other, c, c.other, ..., a.other. Every 2i and 2i+1 have the same StartPosition, Every 2i+1 and 2i+2 are "this and other" (i.e. the same curve after identification).
    /// </summary>
    public readonly List<ModelSurfaceSide> boundaryCurves = new();

    /// <summary>
    /// These are the internal angles of the polygon, i.e. angles[i] is the angle between boundaryCurves[2i] and boundaryCurves[2i+1]
    /// </summary>
    public readonly List<float> angles = new();
    

    private Vector3[] positions = null;

    public override IEnumerable<Vector3> Positions => positions ??= (
        from i in Enumerable.Range(0, boundaryCurves.Count / 2) 
        select boundaryCurves[2 * i].curve.StartPosition.Position
    ).ToArray();  // .SelectMany(bdy => bdy.curve.StartPosition.Positions)
    // there should only be one position per pair of curves because the internal curve of the boundary curve is a "normal" curve, actually a "Geodesic". More than one position would mess up the position indices.

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        // this should not depend on the choice of boundary curve
        var position = homeomorphism.F(Position);
        if (position.HasValue)
            return new BasicPoint( position.Value );
        throw new ArgumentException("Homeomorphism is not defined on this point");
    }

    private float RotationAngle(int fromIndex, int toIndex)
    {
        if (fromIndex > toIndex)
            return - RotationAngle(toIndex, fromIndex);
        return angles.Skip(fromIndex).Take(toIndex - fromIndex).Sum();
        // todo: This is wrong! This rotates about the internal angles, but we need to rotate wrt to the angles between each edge and its other edge.
    }
    
    public override Vector3 PassThrough(int fromPositionIndex, int toPositionIndex, Vector3 direction)
    {
        Complex startDir = new Complex(direction.x, direction.y);
        startDir *= Complex.FromPolarCoordinates(1, RotationAngle(fromPositionIndex, toPositionIndex));
        return new Vector3((float) startDir.Real, (float) startDir.Imaginary, direction.z); 
    }
}

public class ModelSurfaceBoundaryPoint : Point, IModelSurfacePoint
{
    const float ε = 1e-3f;
    public readonly ModelSurfaceSide side;
    public readonly float t;
    private List<Vector3> positions;
    private int positionCountOfFirstCurve;

    public ModelSurfaceBoundaryPoint(ModelSurfaceSide side, float t)
    {
        this.side = side;
        this.t = t;
    }

    public override bool Equals(Point other)
    {
        if (other is ModelSurfaceBoundaryPoint otherPoint)
            return (side == otherPoint.side && t.ApproximateEquals(otherPoint.t)) || 
                   (side == otherPoint.side.other && (side.curve.Length - t).ApproximateEquals(otherPoint.t));
        return base.Equals(other);
    }

    public override Vector3 Position => positions?.First() ?? side.curve[t].Position; // slightly more efficient than Positions.First() because it doesn't calculate side.other.curve[t]

    public override IEnumerable<Vector3> Positions
    {
        get
        {
            if (positions != null) return positions;
            positions = side.curve[t].Positions.ToList();
            positionCountOfFirstCurve = positions.Count;
            positions.AddRange(side.other.curve[t].Positions);
            return positions;
        }
    }

    public override Vector3 PassThrough(int fromPositionIndex, int toPositionIndex, Vector3 direction)
    {
        if (fromPositionIndex >= positionCountOfFirstCurve && toPositionIndex >= positionCountOfFirstCurve)
            return side.other.curve[t].PassThrough(fromPositionIndex - positionCountOfFirstCurve,
                toPositionIndex - positionCountOfFirstCurve, direction);
        if (fromPositionIndex < positionCountOfFirstCurve && toPositionIndex < positionCountOfFirstCurve)
            return side.curve[t].PassThrough(fromPositionIndex, toPositionIndex, direction);
        bool forward = fromPositionIndex < positionCountOfFirstCurve;
        if (forward && fromPositionIndex != 0)
            direction = side.curve[t].PassThrough(fromPositionIndex, 0, direction);

        var firstBasis = side.curve.BasisAt(t);
        var secondBasis = side.other.curve.BasisAt(t);
        Matrix3x3 transformation;
        if (forward)
            transformation = secondBasis.basis * firstBasis.basis.Inverse();
        else
            transformation = firstBasis.basis * secondBasis.basis.Inverse();
        Vector3 result = transformation * direction;
        
        if (toPositionIndex != positionCountOfFirstCurve)
            result = side.other.curve[t].PassThrough(0, toPositionIndex - positionCountOfFirstCurve, result);
        return result;

    }

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new ModelSurfaceBoundaryPoint(side.ApplyHomeomorphism(homeomorphism) as ModelSurfaceSide, t);

    public (ModelSurfaceBoundaryPoint, ModelSurfaceBoundaryPoint) ClosestBoundaryPoints(ModelSurfaceSide side)
    {
        ModelSurfaceBoundaryPoint a = null, b = null;
        if (side == this.side)
            a = this;
        else if (side == this.side.other)
            b = this;
        a ??= side.GetClosestPoint(this).Item2 as ModelSurfaceBoundaryPoint;
        b ??= side.other.GetClosestPoint(this).Item2 as ModelSurfaceBoundaryPoint;
        return (a, b);
    }
}
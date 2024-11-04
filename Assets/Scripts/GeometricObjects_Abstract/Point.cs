using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Vector3Extensions
{
    private const float εSquared = 1e-6f;

    public static bool ApproximatelyEquals(this Vector3 v, Vector3 w) => 
        (v - w).sqrMagnitude < εSquared;

    public static bool ApproximateEquals(this float t, float s) => 
        (t - s) * (t - s) < εSquared;
}

public interface IPoint : IEquatable<IPoint>
{
    public virtual Vector3 Position => Positions.First();
    IEnumerable<Vector3> Positions { get; }
    virtual Vector3 PassThrough(Vector3 position, Vector3 direction) => direction;
    IPoint ApplyHomeomorphism(Homeomorphism homeomorphism);
}

public class BasicPoint : IPoint
{
    public Vector3 Position { get; }
    public IEnumerable<Vector3> Positions => new[] { Position };

    public BasicPoint(Vector3 position)
    {
        Position = position;
    }

    public IPoint ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        var result = homeomorphism.F(Position);
        if (!result.HasValue)
            throw new Exception("Homeomorphism is not defined on this point");
        return new BasicPoint(result.Value);
    }

    public static implicit operator BasicPoint(Vector3 v) => new(v);

    public bool Equals(IPoint other)
    {
        return other != null && (
            from otherPosition in other.Positions
            select otherPosition.ApproximatelyEquals(Position)
        ).Any();
    }
}

public class ConcatenationJumpPoint: IPoint
{
    public ICurve incomingCurve, outgoingCurve;

    public IEnumerable<Vector3> Positions =>
        incomingCurve.EndPosition.Positions.Concat(outgoingCurve.StartPosition.Positions);

    public IPoint ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        return new ConcatenationJumpPoint()
        {
            incomingCurve = incomingCurve.ApplyHomeomorphism(homeomorphism),
            outgoingCurve = outgoingCurve.ApplyHomeomorphism(homeomorphism)
        };
    }

    public Vector3 PassThrough(Vector3 direction)
    {
        throw new NotImplementedException();
    }

    public bool Equals(IPoint other)
    {
        throw new NotImplementedException();
    }
}


public record ModelSurfaceVertex : IPoint
{
    public readonly List<ModelSurfaceSide> boundaryCurves = new(); 
    public float angle;


    public IEnumerable<Vector3> Positions => boundaryCurves.SelectMany(bdy => bdy.curve.StartPosition.Positions);  

    public IPoint ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        // this should not depend on the choice of boundary curve
        var position = homeomorphism.F(Position);
        if (position.HasValue)
            return new BasicPoint( position.Value );
        throw new ArgumentException("Homeomorphism is not defined on this point");
    }

    public Vector3 Position => Positions.First();

    public virtual bool Equals(IPoint other)
    {
        return other != null && (
            from otherPosition in other.Positions 
            select (
                from position in Positions 
                select other.Position.ApproximatelyEquals(position)
            ).Any()
        ).Any();
    }
}

public record ModelSurfaceBoundaryPoint : IPoint
{
    const float ε = 1e-3f;
    public readonly ModelSurfaceSide side;
    public readonly float t;

    public ModelSurfaceBoundaryPoint(ModelSurfaceSide side, float t)
    {
        this.side = side;
        this.t = t;
    }

    public bool Equals(IPoint other)
    {
        if (other is ModelSurfaceBoundaryPoint otherPoint)
            return (side == otherPoint.side && t.ApproximateEquals(otherPoint.t)) || 
                   (side == otherPoint.side.other && (side.curve.Length - t).ApproximateEquals(otherPoint.t));
        throw new NotImplementedException();
    }

    public IEnumerable<Vector3> Positions => new[]
    {
        side.curve[t],
        side.other.curve[t]
    };

    public IPoint ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new ModelSurfaceBoundaryPoint(side.ApplyHomeomorphism(homeomorphism) as ModelSurfaceSide, t);
}
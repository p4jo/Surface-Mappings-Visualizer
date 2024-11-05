using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ITransformable<out T>
{
    public T ApplyHomeomorphism(Homeomorphism homeomorphism);
}

public abstract class Point : IEquatable<Point>, ITransformable<Point>
{
    public virtual Vector3 Position => Positions.First();
    public abstract IEnumerable<Vector3> Positions { get; }
    public virtual Vector3 PassThrough(Vector3 position, Vector3 direction) => direction;

    public abstract Point ApplyHomeomorphism(Homeomorphism homeomorphism);

    public virtual bool Equals(Point other) => 
        other != null && 
        Positions.Any(
            position => 
                other.Positions.Any(
                    otherPosition => otherPosition.ApproximatelyEquals(position)
                ));


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

}

public class ConcatenationJumpPoint: Point
{
    public Curve incomingCurve, outgoingCurve;

    public override IEnumerable<Vector3> Positions =>
        incomingCurve.EndPosition.Positions.Concat(outgoingCurve.StartPosition.Positions);

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        return new ConcatenationJumpPoint()
        {
            incomingCurve = incomingCurve.ApplyHomeomorphism(homeomorphism),
            outgoingCurve = outgoingCurve.ApplyHomeomorphism(homeomorphism)
        };
    }

    public override Vector3 PassThrough(Vector3 position, Vector3 direction)
    {
        return base.PassThrough(position, direction); // todo
    }
}


public class ModelSurfaceVertex : Point
{
    public readonly List<ModelSurfaceSide> boundaryCurves = new(); 
    public readonly List<float> angles = new();


    public override IEnumerable<Vector3> Positions => boundaryCurves.SelectMany(bdy => bdy.curve.StartPosition.Positions);  

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        // this should not depend on the choice of boundary curve
        var position = homeomorphism.F(Position);
        if (position.HasValue)
            return new BasicPoint( position.Value );
        throw new ArgumentException("Homeomorphism is not defined on this point");
    }


}

public class ModelSurfaceBoundaryPoint : Point
{
    const float ε = 1e-3f;
    public readonly ModelSurfaceSide side;
    public readonly float t;

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

    public override IEnumerable<Vector3> Positions => new[]
    {
        side.curve[t],
        side.other.curve[t]
    };

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new ModelSurfaceBoundaryPoint(side.ApplyHomeomorphism(homeomorphism) as ModelSurfaceSide, t);
}
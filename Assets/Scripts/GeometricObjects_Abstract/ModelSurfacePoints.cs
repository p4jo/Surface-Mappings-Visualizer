using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

public partial class ModelSurfaceBoundaryPoint : Point, IModelSurfacePoint
{
    const float ε = 1e-3f;
    public readonly ModelSurfaceSide side;
    public readonly float t;
    private List<Vector3> positions;
    int _positionCountOfFirstCurve = -1;
    public int PositionCountOfFirstCurve => _positionCountOfFirstCurve >= 0 || Positions.Any() ? _positionCountOfFirstCurve : throw new Exception("Programmer is stupid and forgot that his code is hard to maintain. Evaluating Positions should set _positionCountOfFirst.");

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
            _positionCountOfFirstCurve = positions.Count;
            positions.AddRange(side.other.curve[t].Positions);
            return positions;
        }
    }

    public override Vector3 PassThrough(int fromPositionIndex, int toPositionIndex, Vector3 direction)
    {
        if (fromPositionIndex == toPositionIndex)
            return direction;
        if (fromPositionIndex >= PositionCountOfFirstCurve && toPositionIndex >= PositionCountOfFirstCurve)
            return side.other.curve[t].PassThrough(fromPositionIndex - PositionCountOfFirstCurve,
                toPositionIndex - PositionCountOfFirstCurve, direction);
        if (fromPositionIndex < PositionCountOfFirstCurve && toPositionIndex < PositionCountOfFirstCurve)
            return side.curve[t].PassThrough(fromPositionIndex, toPositionIndex, direction);
        bool forward = fromPositionIndex < PositionCountOfFirstCurve;
        if (forward && fromPositionIndex != 0)
            direction = side.curve[t].PassThrough(fromPositionIndex, 0, direction);
        if (!forward && fromPositionIndex != PositionCountOfFirstCurve)
            direction = side.other.curve[t].PassThrough(fromPositionIndex - PositionCountOfFirstCurve, 0, direction);

        var firstBasis = side.curve.BasisAt(t);
        var secondBasis = side.other.curve.BasisAt(t);
        Matrix3x3 transformation;
        if (forward)
            transformation = secondBasis.basis * firstBasis.basis.Inverse();
        else
            transformation = firstBasis.basis * secondBasis.basis.Inverse();
        Vector3 result = transformation * direction;
        
        if (forward && toPositionIndex != PositionCountOfFirstCurve)
            result = side.other.curve[t].PassThrough(0, toPositionIndex - PositionCountOfFirstCurve, result);
        else if (!forward && toPositionIndex != 0)
            result = side.curve[t].PassThrough(0, toPositionIndex, result);
        return result;

    }

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        Curve transformedCurve = side.ApplyHomeomorphism(homeomorphism);
        if (transformedCurve is ModelSurfaceSide modelSurfaceSide)
            return new ModelSurfaceBoundaryPoint(modelSurfaceSide, t);
        return transformedCurve[t];
    }

    public ModelSurfaceBoundaryPoint ClosestBoundaryPoints(ModelSurfaceSide side) => 
        side == this.side ? this : new(side, side.curve.GetClosestPoint(this));

    public ModelSurfaceBoundaryPoint SwitchSide() => new(side.other, t);

    public override string ToString() => $"{side.Name}({t:0.00})";
}

public class ModelSurfaceInteriorPoint : BasicPoint, IModelSurfacePoint
{
    public ModelSurfaceBoundaryPoint ClosestBoundaryPoints(ModelSurfaceSide side) =>
        new ModelSurfaceBoundaryPoint(side, side.GetClosestPoint(this));

    public ModelSurfaceInteriorPoint(Vector3 position) : base(position)
    {
    }
}

public interface IModelSurfacePoint
{
    ModelSurfaceBoundaryPoint ClosestBoundaryPoints(ModelSurfaceSide side);
}

public partial class ModelSurfaceVertex : Point
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

    public ModelSurfaceVertex()
    {
    }

    private ModelSurfaceVertex(List<ModelSurfaceSide> boundaryCurves)
    {
        this.boundaryCurves = boundaryCurves;
    }

    public override IEnumerable<Vector3> Positions => positions ??= (
        from i in Enumerable.Range(0, boundaryCurves.Count / 2)
        select boundaryCurves[2 * i].curve.StartPosition.Position
    ).ToArray(); // .SelectMany(bdy => bdy.curve.StartPosition.Positions)
    // there should only be one position per pair of curves because the internal curve of the boundary curve is a "normal" curve, actually a "Geodesic". More than one position would mess up the position indices.

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        if (homeomorphism.isIdentity) return this;
        // this should not depend on the choice of boundary curve
        var position = homeomorphism.F(Position);
        if (position.HasValue)
            return new BasicPoint(position.Value);
        throw new ArgumentException("Homeomorphism is not defined on this point");
    }

    private float RotationAngle(int fromIndex, int toIndex)
    {
        if (fromIndex > toIndex)
            return -RotationAngle(toIndex, fromIndex);
        return angles.Skip(fromIndex).Take(toIndex - fromIndex).Sum();
    }

    public override Vector3 PassThrough(int fromPositionIndex, int toPositionIndex, Vector3 direction)
    {
        // this is actually not really well-defined, because "passing through" would assume "goedesicness" which could be defined as having total angle >= π at both sides. So, not all position indices work, and some cone points (with total angle < τ) cannot have any geodesics pass through them.
        Complex startDir = new Complex(direction.x, direction.y);
        var leftAngleBefore = boundaryCurves[2 * fromPositionIndex].angle;
        var leftAngleAfter = boundaryCurves[2 * toPositionIndex].angle;
        startDir *= Complex.FromPolarCoordinates(1, leftAngleAfter - leftAngleBefore);
        return new Vector3((float)startDir.Real, (float)startDir.Imaginary, direction.z);
    }

    public Curve GeodesicCircleAround(float radius = 1f, bool startBetweenEdges = true)
    {
        if (boundaryCurves.FirstOrDefault()?.Surface is not ModelSurface surface)
            throw new InvalidOperationException(
                $"Model surface vertex {this}'s first curve {boundaryCurves.FirstOrDefault()} is not on a model surface, but on {boundaryCurves.FirstOrDefault()?.Surface}!?");

        var segments = Segments().ToList();
        if (!startBetweenEdges)
            return new ConcatenatedCurve(segments,
                $"puncture circle starting at {boundaryCurves[0].curve.Name}"
            );
        var firstSegment = segments[0];
        segments[0] = firstSegment.Restrict(firstSegment.Length / 2);
        segments.Add(firstSegment.Restrict(0, firstSegment.Length / 2));
        return new ConcatenatedCurve(segments,
            $"puncture circle starting between {boundaryCurves[0].curve.Name} and {boundaryCurves[1].curve.Name}",
            smoothed: true
        );

        IEnumerable<Curve> Segments()
        {
            for (int i = 0; i < boundaryCurves.Count / 2; i++)
            {
                var a = boundaryCurves[2 * i];
                var b = boundaryCurves[2 * i + 1];
                yield return surface.GetBasicGeodesic(a[radius], b[radius],
                    $"puncture circle segment between {a.Name} and {b.Name}");
            }
        }
    }
}
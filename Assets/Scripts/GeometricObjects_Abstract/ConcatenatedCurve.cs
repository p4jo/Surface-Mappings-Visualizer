using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

/// <summary>
/// This is a point that is where two curves don't meet, but are concatenated... This shouldn't actually happen.
/// </summary>
public partial class ConcatenationSingularPoint: Point
{
    public float time = -1f;
    public Curve incomingCurve, outgoingCurve;
    public int incomingPosIndex, outgoingPosIndex;
    public bool visualJump, actualJump, angleJump;

    public override IEnumerable<Vector3> Positions =>
        incomingCurve.EndPosition.Positions.Concat(outgoingCurve.StartPosition.Positions);

    public override Point ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        return new ConcatenationSingularPoint()
        {
            incomingCurve = incomingCurve.ApplyHomeomorphism(homeomorphism),
            outgoingCurve = outgoingCurve.ApplyHomeomorphism(homeomorphism),
            incomingPosIndex = incomingPosIndex,
            outgoingPosIndex = outgoingPosIndex,
            visualJump = visualJump,
            actualJump = actualJump,
            angleJump = angleJump,
            time = time
        };
    }

    public ConcatenationSingularPoint ApplyTimeOffset(float delta) =>
        new()
        {
            incomingCurve = incomingCurve,
            outgoingCurve = outgoingCurve,
            incomingPosIndex = incomingPosIndex,
            outgoingPosIndex = outgoingPosIndex,
            visualJump = visualJump,
            actualJump = actualJump,
            angleJump = angleJump,
            time = time + delta
        };
}

public partial class ConcatenatedCurve : Curve
{
    private readonly Curve[] segments;
    private List<ConcatenationSingularPoint> nonDifferentiablePoints;


    public override string Name { get; set; }

    public override Surface Surface => segments.First().Surface;

    public override float Length { get; }

    public override IEnumerable<float> VisualJumpTimes => (
        from singularPoint in NonDifferentiablePoints
        where singularPoint.visualJump
        select singularPoint.time
    ).Concat(
        from segment in segments
        from t in segment.VisualJumpTimes
        select t + segments.TakeWhile(s => s != segment).Sum(s => s.Length)
    ).OrderBy(t => t);

    public override IEnumerable<(float, ModelSurfaceBoundaryPoint)> VisualJumpPoints
    {
        get
        {
            return SingularPointsOfConcatenation().Concat(
                from segment in segments
                from t in segment.VisualJumpPoints
                select (t.Item1 + segments.TakeWhile(s => s != segment).Sum(s => s.Length), t.Item2)
            ).OrderBy(t => t.Item1);

            IEnumerable<(float, ModelSurfaceBoundaryPoint)> SingularPointsOfConcatenation()
            {
                if (Surface is not ModelSurface modelSurface)
                    yield
                        break; // shouldn't happen unless VisualJumpTimes is already empty (atm this could happen in TransformedCurve, but there it doesn't matter)
                foreach (var singularPoint in NonDifferentiablePoints)
                {
                    if (!singularPoint.visualJump) continue;
                    if (singularPoint.incomingCurve.EndPosition is ModelSurfaceBoundaryPoint boundaryPoint)
                        yield return (singularPoint.time, boundaryPoint);
                    else
                    {
                        if (singularPoint.outgoingCurve.StartPosition is ModelSurfaceBoundaryPoint boundaryPoint2)
                            yield return (singularPoint.time, boundaryPoint2.SwitchSide());
                        else
                        {
                            // todo: Performance. Can we avoid the expensive calls to ClampPoint?
                            var pt1 = modelSurface.ClampPoint(singularPoint.incomingCurve.EndPosition, 1e-2f);
                            var pt2 = modelSurface.ClampPoint(singularPoint.outgoingCurve.StartPosition, 1e-2f);
                            if (pt1 is ModelSurfaceBoundaryPoint p1 && pt2 is ModelSurfaceBoundaryPoint p2 &&
                                p1.side == p2.side.other)
                                yield return (singularPoint.time, p1);
                            else
                                Debug.LogWarning(
                                    "Something went wrong with the visual jump points in ConcatenatedCurve");
                        }
                    }
                }
            }
        }
    }

    private List<ConcatenationSingularPoint> NonDifferentiablePoints =>
        nonDifferentiablePoints ??= CalculateSingularPoints(segments);

    public ConcatenatedCurve(IEnumerable<Curve> curves, string name = null, bool smoothed = false)
    {
        if (smoothed)
            curves = Smoothed(curves);

        segments = curves.SelectMany(
            curve => curve is ConcatenatedCurve concatenatedCurve ? concatenatedCurve.segments : new[] { curve }
        ).ToArray();
        if (segments.Any(segment => segment.Length == 0))
            Debug.LogWarning("One of the segments of the concatenated curve has zero length.");
        float length = (from segment in segments select segment.Length).Sum();
        if (length == 0)
            throw new Exception("Length of curve is zero");
        Length = length;

        Name = name ?? string.Join(" -> ", from segment in segments select segment.Name);
    }

    /// <summary>
    /// ATM, this assumes that the curves are in 2D subspace! 
    /// </summary>
    /// <param name="segments"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    static List<Curve> Smoothed(IEnumerable<Curve> segments)
    {
        var curves = segments.ToList();

        var singularPoints = CalculateSingularPoints(curves, ignoreSubConcatenatedCurves: true)
            .Where(sp => sp.angleJump).ToList();

        List<int> singularIndices = singularPoints.Select(singularPoint => curves.IndexOf(singularPoint.incomingCurve))
            .ToList();
        if (singularIndices.Any(index => index == -1))
            throw new Exception("Something went wrong");

        for (int i = 0; i < singularPoints.Count; i++)
        {
            var singularPoint = singularPoints[i];

            var incomingCurve = singularPoint.incomingCurve;
            var outgoingCurve = singularPoint.outgoingCurve;

            var index = singularIndices[i] + 2 * i;
            // curves[index] == incomingCurve or a last segment of it.
            if (curves[index + 1] != outgoingCurve)
                throw new Exception("Something went wrong");

            var inVector = incomingCurve.EndVelocity.VectorAtPositionIndex(singularPoint.incomingPosIndex);
            var outVector = outgoingCurve.StartVelocity.VectorAtPositionIndex(singularPoint.outgoingPosIndex);


            var (incomingCurveReversedTimeFromArclengthFunction, incomingArclength) =
                GeodesicSurface.TimeFromArclengthParametrization(incomingCurve.Reversed());
            var (outgoingCurveTimeFromArclengthFunction, outgoingArclength) =
                GeodesicSurface.TimeFromArclengthParametrization(outgoingCurve);
            inVector *= incomingCurveReversedTimeFromArclengthFunction(0).Item2; // dc/ds = dc/dt * dt/ds
            outVector *= outgoingCurveTimeFromArclengthFunction(0).Item2;

            var inAngle = inVector.Angle();
            var outAngle = outVector.Angle();
            var angle = (inAngle + outAngle) / 2;
            if (MathF.Abs(inAngle - outAngle) > MathF.PI)
                angle += MathF.PI;
            var length = MathF.Pow(inVector.sqrMagnitude * outVector.sqrMagnitude, 0.25f);


            var incomingDeductedArclength =
                0.3f * incomingArclength * (1 - MathF.Exp(-length / incomingArclength / 0.3f));
            var outgoingDeductedArclength =
                0.3f * outgoingArclength * (1 - MathF.Exp(-length / outgoingArclength / 0.3f));

            var (incomingDeductedTime, incomingTimeByArclengthDerivative) =
                incomingCurveReversedTimeFromArclengthFunction(incomingDeductedArclength);
            var (outgoingDeductedLength, outgoingTimeByArclengthDerivative) =
                outgoingCurveTimeFromArclengthFunction(outgoingDeductedArclength);

            var restrictedIncomingCurve =
                curves[index] = incomingCurve.Restrict(0, incomingCurve.Length - incomingDeductedTime);
            var restrictedOutgoingCurve = curves[index + 1] = outgoingCurve.Restrict(outgoingDeductedLength);

            var startOfFirstInterpolated =
                incomingTimeByArclengthDerivative * restrictedIncomingCurve.EndVelocity; // dc/ds = dc/dt * dt/ds
            var endOfLastInterpolated = outgoingTimeByArclengthDerivative * restrictedOutgoingCurve.StartVelocity;

            var centerVector = Complex.FromPolarCoordinates(length, angle).ToVector3();
            // centerVector is the abstract vector in the shared tangent space (corresponding to the incomingPosIndex'th position of incomingCurve and the outgoingPosIndex'th position of outgoingCurve)
            var ingoingCenterVector =
                new TangentVector(incomingCurve.EndPosition, centerVector, singularPoint.incomingPosIndex);
            var outgoingCenterVector = new TangentVector(outgoingCurve.StartPosition,
                centerVector, singularPoint.outgoingPosIndex);

            var firstInterpolated = new SplineSegment(
                startOfFirstInterpolated, ingoingCenterVector, incomingDeductedArclength, incomingCurve.Surface,
                incomingCurve.Name + " interp. segment"
            );

            var secondInterpolated = new SplineSegment(
                outgoingCenterVector, endOfLastInterpolated, outgoingDeductedArclength, outgoingCurve.Surface,
                outgoingCurve.Name + " interp. segment"
            );

            curves.Insert(index + 1, firstInterpolated);
            curves.Insert(index + 2, secondInterpolated);
        }

        return curves;
    }

    public ConcatenatedCurve Smoothed() => new(segments, Name + " smoothed", smoothed: true) { Color = Color };

    public override Point EndPosition => segments.Last().EndPosition;
    public override Point StartPosition => segments.First().StartPosition;
    public override TangentVector EndVelocity => segments.Last().EndVelocity;
    public override TangentVector StartVelocity => segments.First().StartVelocity;

    public override Point ValueAt(float t)
    {
        if (t >= Length) // bc. Length % Length = 0 ...
            return EndPosition;
        t %= Length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.ValueAt(t);
            t -= segment.Length;
        }

        throw new Exception("What the heck");
    }

    public override TangentVector DerivativeAt(float t)
    {
        t %= Length;
        foreach (var segment in segments)
        {
            if (t < segment.Length)
                return segment.DerivativeAt(t);
            t -= segment.Length;
        }

        throw new Exception("What the heck");
    }

    public override Curve Reversed() => reverseCurve ??=
        new ConcatenatedCurve(from segment in segments.Reverse() select segment.Reversed(),
            Name.EndsWith("'") ? Name : Name + "'") { Color = Color, reverseCurve = this };

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        if (homeomorphism.isIdentity)
            return this;
        return new ConcatenatedCurve(from segment in segments select segment.ApplyHomeomorphism(homeomorphism),
            Name + " --> " + homeomorphism.target.Name
        ) { Color = Color };
    }

    public override Curve Copy() => new ConcatenatedCurve(from segment in segments select segment.Copy(), Name)
        { Color = Color };


    private static List<ConcatenationSingularPoint> CalculateSingularPoints(IReadOnlyList<Curve> segments,
        bool expectClosedCurve = false, bool ignoreSubConcatenatedCurves = false)
    {
        List<ConcatenationSingularPoint> res = new();
        var timeA = 0f;
        Curve curve, nextCurve;
        for (int i = 0; i < segments.Count; i++)
        {
            curve = segments[i];
            if (!ignoreSubConcatenatedCurves && curve is ConcatenatedCurve curveAsConcat)
                res.AddRange(from internalJumpPoint in curveAsConcat.NonDifferentiablePoints
                    select internalJumpPoint.ApplyTimeOffset(timeA));
            if (i < segments.Count - 1)
                nextCurve = segments[i + 1];
            else if (expectClosedCurve)
                nextCurve = segments[0];
            else
                break;
            timeA += curve
                .Length; // todo: Bug. this seems to not work correctly in some cases (the time is the 0.1f*Length off in the smoothed curves) and this compounds.

            var baseGeodesicSurface = curve.Surface is ModelSurface modelSurface1
                ? modelSurface1.GeometrySurface
                : curve.Surface as GeodesicSurface ?? new EuclideanPlane();
            var (herePosIndex, therePosIndex, distanceSquared) =
                baseGeodesicSurface.ClosestPositionIndices(curve.EndPosition, nextCurve.StartPosition);
            // if (!curve.EndPosition.Equals(nextCurve.StartPosition))
            bool angleJump = !curve.EndVelocity.VectorAtPositionIndex(herePosIndex).ApproximatelyEquals(
                nextCurve.StartVelocity.VectorAtPositionIndex(therePosIndex));
            bool actualJump = distanceSquared > 1e-3f;
            // if distance is too large, this means, these points are actually different; even considering multiple positions.
            // for drawing, if there are multiple positions at the concatenation point, we should be wary, because the different segments might be far apart (converging to the different positions).
            bool visualJump = curve.Surface is ModelSurface modelSurface &&
                              modelSurface.ClampPoint(curve.EndPosition, 1e-3f) is ModelSurfaceBoundaryPoint;
            // bool visualJump = curve[curve.Length - 1e-6f].DistanceSquared(nextCurve[1e-6f]) > 1e-3f;
            if (visualJump && (curve.EndPosition is not ModelSurfaceBoundaryPoint ||
                               nextCurve.StartPosition is not ModelSurfaceBoundaryPoint))
            {
                Debug.LogWarning(
                    $"Visual jump between {curve.Name} and {nextCurve.Name} but these are not saved with boundary points as endpoints.");
            }

            if (actualJump || angleJump || visualJump)
            {
                // todo: Performance. Save the corresponding model surface boundary points in case of a visual jump. This might (or might not) improve performance in the calls to VisualJumpPoints, which is called in the property FibredSurface.MovementForFolding.Badness which is supposed to be completely combinatorial (i.e. without calls to GetClosestPoint or similar geometric functions). 
                res.Add(new ConcatenationSingularPoint
                {
                    incomingCurve = curve,
                    outgoingCurve = nextCurve,
                    incomingPosIndex = herePosIndex,
                    outgoingPosIndex = therePosIndex,
                    visualJump = visualJump,
                    actualJump = actualJump,
                    angleJump = angleJump,
                    time = timeA
                }); // save angle?
            }
        }

        return res;
    }

    public override Curve Restrict(float start, float? end = null)
    {
        var stop = end ?? Length;
        if (start < -restrictTolerance || stop > Length + restrictTolerance || start > stop + restrictTolerance)
            throw new ArgumentOutOfRangeException(
                $"Invalid restriction in curve {this}: {start} to {end} for length {Length}");
        if (stop > Length)
            stop = Length;
        if (start < 0)
            start = 0;
        if (start < restrictTolerance && stop > Length - restrictTolerance)
            return this;


        var movedStartTime = start;
        var movedEndTime = stop;
        int startSegmentIndex = -1, endSegmentIndex = segments.Length;
        for (int i = 0; i < segments.Length; i++)
        {
            var segmentLength = segments[i].Length;
            if (movedStartTime <= segmentLength - restrictTolerance)
            {
                startSegmentIndex = i;
                break;
            }

            movedStartTime -= segmentLength; // > - restrictTolerance
        }

        for (int i = 0; i < segments.Length; i++)
        {
            var segmentLength = segments[i].Length;
            if (movedEndTime <= segmentLength + restrictTolerance)
            {
                endSegmentIndex = i;
                break;
            }

            movedEndTime -= segmentLength; // > restrictTolerance
        }

        if (startSegmentIndex == endSegmentIndex)
        {
            var restrictedSegment = segments[startSegmentIndex].Restrict(movedStartTime, movedEndTime);
            restrictedSegment.Color = Color;
            return restrictedSegment;
        }

        if (startSegmentIndex ==
            -1) // start - sum(segmentsLength) > 0, i.e. start > Length - (calculation error from movedStartTime), so probably start == Length
        {
            var restrictedSegment = segments[^1].Restrict(segments[^1].Length);
            restrictedSegment.Color = Color;
            return restrictedSegment; // will do the same and return the standing path.
        }

        var firstSegment = segments[startSegmentIndex].Restrict(movedStartTime);
        var curves = segments[(startSegmentIndex + 1)..endSegmentIndex].Prepend(firstSegment);

        if (endSegmentIndex < segments.Length)
            curves = curves.Append(
                segments[endSegmentIndex].Restrict(0, movedEndTime)
            );
        return new ConcatenatedCurve(curves, Name + $"[{start:g2}, {end:g2}]") { Color = Color };
        // else, the color of the restricted curve is the color of the first segment that remains after the restriction,
        // which might not be the first segment of the original ConcatenatedCurve. Also the ConcatenatedCurve might have a changed color.
    }
}
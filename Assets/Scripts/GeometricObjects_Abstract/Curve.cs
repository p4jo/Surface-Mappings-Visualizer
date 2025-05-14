using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;


public abstract partial class Curve: ITransformable<Curve> // even IDrawnsformable in other part.
{
    public abstract string Name { get; set; }
    
    public abstract float Length { get; }

    public virtual Point StartPosition => ValueAt(0);
    public virtual Point EndPosition => ValueAt(Length);
    public virtual TangentVector StartVelocity => DerivativeAt(0);
    public virtual TangentVector EndVelocity => DerivativeAt(Length);

    public abstract Surface Surface { get; }
    public virtual IEnumerable<float> VisualJumpTimes => Enumerable.Empty<float>();

    public virtual IEnumerable<(float, ModelSurfaceBoundaryPoint)> VisualJumpPoints
    {
        get
        {
            if (Surface is not ModelSurface modelSurface)
                yield break; // shouldn't happen unless VisualJumpTimes is already empty (atm this could happen in TransformedCurve, but there it doesn't matter)
            foreach (float t in VisualJumpTimes)
            {
                if (this[t] is ModelSurfaceBoundaryPoint boundaryPoint)
                    yield return (t, boundaryPoint);
                else
                {
                    var pt1 = modelSurface.ClampPoint(this[t - 1e-4f], 1e-3f);
                    var pt2 = modelSurface.ClampPoint(this[t + 1e-4f], 1e-3f);
                    if (pt1 is ModelSurfaceBoundaryPoint p1 && pt2 is ModelSurfaceBoundaryPoint p2 && p1.side == p2.side.other)
                        yield return (t, p1);
                    else 
                        Debug.LogWarning("Something went wrong with the visual jump points in ConcatenatedCurve");
                }
            }
        }
    }

    public virtual IEnumerable<(string, bool)> SideCrossingWord =>
        from p in VisualJumpPoints
        let side = p.Item2.side
        select (side.Name, side.Surface is ModelSurface surface && !surface.sides.Contains(side));

    public virtual Point this[float t] => ValueAt(t);

    public abstract Point ValueAt(float t);
    public abstract TangentVector DerivativeAt(float t);
    
    public virtual Curve Concatenate(Curve curve) => new ConcatenatedCurve(new Curve[] { this, curve });

    protected Curve reverseCurve;

    public virtual Curve Reversed() => reverseCurve ??= new ReverseCurve(this);

    /// <summary>
    /// 
    /// </summary>
    /// <returns>if this is a ModelSurfaceSide, the returned Point is a ModelSurfaceBoundaryPoint</returns>
    public virtual (float, Point) GetClosestPoint(Vector3 point, float precision = 1e-5f)
    {
        // If curve has more than one position at any time, we should optimize over all of them? This is not clear from the curve.
        // Thus this has to be implemented in the subclasses that have multiple positions. Done for the ModelSurfaceSide.
        (float, Point) f(float t)
        {
            var pointOnCurve = this[t]; 
            return ((pointOnCurve.Position - point).sqrMagnitude, pointOnCurve);
            // return (pointOnCurve.Distance(point), pointOnCurve);
        }

        float fDeriv(float t)
        {
            var (pointOnCurve, vector) = DerivativeAt(t);
            return 2 * Vector3.Dot(vector, pointOnCurve - point);
        }

        float learningRate = 0.1f;
        float t = Length / 2;
        for (var i = 0; i < 1000; i++)
        {
            float gradient = fDeriv(t);
            var (dist, pos) = f(t);
            float change = learningRate * gradient * Mathf.Clamp(dist, 1, 100);
            t -= change;
            if (t < 0) return (0, StartPosition);
            if (t > Length) return (Length, EndPosition);

            if (Mathf.Abs(change) < precision)
                return (t, pos);
            if (dist < 1e-4)
                return (t, pos);
        }
        Debug.Log("Warning: Curve.GetClosestPoint did not converge in 1000 steps");
        return (t, this[t]);
    }

    public virtual Curve ApplyHomeomorphism(Homeomorphism homeomorphism) => homeomorphism.isIdentity ? this : new TransformedCurve(this, homeomorphism);

    /// <summary>
    /// A right-handed basis at the point on the curve at time t, with the tangent vector as the first vector and the normal vector of the surface as the third vector. Thus, the second vector points "to the right" of the curve.
    /// </summary>
    public virtual TangentSpace BasisAt(float t)
    {
        var (point, tangent) = DerivativeAt(t);
        var (_, basis) = Surface.BasisAt(point);
        return new TangentSpace(
            point,
            new Matrix3x3(
                tangent,
                Vector3.Cross(basis.c, tangent), 
                basis.c
            )
        );
    }

    public virtual Curve Restrict(float start, float? end = null)
    {
        var stop = end ?? Length;
        if (stop > Length)
            stop = Length;
        if (start == 0 && stop == Length)
            return this;
        return new RestrictedCurve(this, start, stop);
    }

    public abstract Curve Copy();
}

public partial class TransformedCurve : Curve
{
    private readonly Curve curve;
    private readonly Homeomorphism homeomorphism;

    public TransformedCurve(Curve curve, Homeomorphism homeomorphism)
    {
        this.curve = curve;
        this.homeomorphism = homeomorphism;
        // if (curve.Surface != homeomorphism.source)
        //     throw new Exception("Homeomorphism does not match surface");
    }

    [CanBeNull] public string _name;
    public override string Name
    {
        get => _name ?? curve.Name + " --> " + homeomorphism.target.Name;
        set => _name = value;
    }

    public override float Length => curve.Length;
    public override Point EndPosition => curve.EndPosition.ApplyHomeomorphism(homeomorphism);
    public override Point StartPosition => curve.StartPosition.ApplyHomeomorphism(homeomorphism);
    public override TangentVector EndVelocity => curve.EndVelocity.ApplyHomeomorphism(homeomorphism); 
    public override TangentVector StartVelocity => curve.StartVelocity.ApplyHomeomorphism(homeomorphism);
    public override Surface Surface => homeomorphism.target;

    public override IEnumerable<float> VisualJumpTimes => curve.VisualJumpTimes;
    // TODO: If this maps to a model surface, we need to check for visual jumps in the transformed curve!
    // If this maps to a parametrized surface, from a model surface, we could forget these

    public override Point ValueAt(float t) => curve.ValueAt(t).ApplyHomeomorphism(homeomorphism);

    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t).ApplyHomeomorphism(homeomorphism);
    // we should have a TangentVector class that is transformable. 

    
    public override TangentSpace BasisAt(float t) => curve.BasisAt(t).ApplyHomeomorphism(homeomorphism);
    public override Curve Copy() => new TransformedCurve(curve.Copy(), homeomorphism) { Name = Name, Color = Color } ;

    public override Curve Reversed() => reverseCurve ??= new TransformedCurve(curve.Reversed(), homeomorphism);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism) =>
        new TransformedCurve(curve, homeomorphism * this.homeomorphism);
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
    
    public override IEnumerable<(float, ModelSurfaceBoundaryPoint)> VisualJumpPoints { 
        get {
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
                            yield return (singularPoint.time, boundaryPoint2);
                        else
                        {
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

    private List<ConcatenationSingularPoint> NonDifferentiablePoints => nonDifferentiablePoints ??= CalculateSingularPoints(segments);

    public ConcatenatedCurve(IEnumerable<Curve> curves, string name = null, bool smoothed = false)
    {
        if (smoothed)
            curves = Smoothed(curves);
        
        segments = curves.SelectMany(
            curve => curve is ConcatenatedCurve concatenatedCurve ? concatenatedCurve.segments : new []{ curve }
        ).ToArray();

        float length = (from segment in segments select segment.Length).Sum();
        if (length == 0) 
            throw new Exception("Length of curve is zero");
        Length = length;
        
        Name = name ?? string.Join(" -> ", from segment in segments select segment.Name);
    }

    static List<Curve> Smoothed(IEnumerable<Curve> segments)
    {
        var curves = segments.ToList();
        
        var singularPoints = CalculateSingularPoints(curves, ignoreSubConcatenatedCurves: true).Where(sp => sp.angleJump).ToList();

        List<int> singularIndices = singularPoints.Select(singularPoint => curves.IndexOf(singularPoint.incomingCurve)).ToList();
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
            var inAngle = inVector.Angle();
            var outVector = outgoingCurve.StartVelocity.VectorAtPositionIndex(singularPoint.outgoingPosIndex);
            var outAngle = outVector.Angle();
            var angle = (inAngle + outAngle) / 2;
            var length = Mathf.Pow(inVector.sqrMagnitude * outVector.sqrMagnitude, 0.25f);
            
            var restrictedIncomingCurve = curves[index] = incomingCurve.Restrict(0, incomingCurve.Length * 0.9f);
            var restrictedOutgoingCurve = curves[index + 1] = outgoingCurve.Restrict(outgoingCurve.Length * 0.1f, outgoingCurve.Length);
            var startOfFirstInterpolated = restrictedIncomingCurve.EndVelocity;
            var endOfLastInterpolated = restrictedOutgoingCurve.StartVelocity;
            
            var centerVector = Complex.FromPolarCoordinates(length, angle).ToVector3(); 
            // centerVector is the abstract vector in the shared tangent space (corresponding to the incomingPosIndex'th position of incomingCurve and the outgoingPosIndex'th position of outgoingCurve)
            var ingoingCenterVector = new TangentVector(incomingCurve.EndPosition, incomingCurve.EndPosition.PassThrough(singularPoint.incomingPosIndex, 0, centerVector));  
            var outgoingCenterVector = new TangentVector(outgoingCurve.StartPosition, outgoingCurve.StartPosition.PassThrough(singularPoint.outgoingPosIndex, 0, centerVector));

            var firstInterpolated = new SplineSegment(
                startOfFirstInterpolated, ingoingCenterVector,incomingCurve.Length * 0.1f,  incomingCurve.Surface,  incomingCurve.Name + " interp. segment"             
            );

            var secondInterpolated = new SplineSegment(
                outgoingCenterVector, endOfLastInterpolated, outgoingCurve.Length * 0.1f, outgoingCurve.Surface, outgoingCurve.Name + " interp. segment"
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

    public override Curve Reversed() => reverseCurve ??= new ConcatenatedCurve(from segment in segments.Reverse() select segment.Reversed(), Name + "'") { Color = Color };

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        if (homeomorphism.isIdentity)
            return this;
        return new ConcatenatedCurve(from segment in segments select segment.ApplyHomeomorphism(homeomorphism),
            Name + " --> " + homeomorphism.target.Name
        ) { Color = Color };
    }

    public override Curve Copy() => new ConcatenatedCurve(from segment in segments select segment.Copy(), Name) { Color = Color };


    private static List<ConcatenationSingularPoint> CalculateSingularPoints(IReadOnlyList<Curve> segments, bool expectClosedCurve = false, bool ignoreSubConcatenatedCurves = false)
    {
        List<ConcatenationSingularPoint> res = new(); 
        var timeA = 0f;
        Curve curve, nextCurve;
        for (int i = 0; i < segments.Count; i++)
        {
            curve = segments[i];
            if (!ignoreSubConcatenatedCurves && curve is ConcatenatedCurve curveAsConcat)
                res.AddRange(from internalJumpPoint in curveAsConcat.NonDifferentiablePoints select internalJumpPoint.ApplyTimeOffset(timeA));
            if (i < segments.Count - 1)
                nextCurve = segments[i + 1];
            else if (expectClosedCurve)
                nextCurve = segments[0];
            else
                break;
            timeA += curve.Length; // todo: this seems to not work correctly in some cases (the time is the 0.1f*Length off in the smoothed curves) and this compounds.
            
            var (herePosIndex, therePosIndex, distanceSquared) = curve.EndPosition.ClosestPositionIndices(nextCurve.StartPosition);
            // if (!curve.EndPosition.Equals(nextCurve.StartPosition))
            bool angleJump = !curve.EndVelocity.VectorAtPositionIndex(herePosIndex).ApproximatelyEquals(
                nextCurve.StartVelocity.VectorAtPositionIndex(therePosIndex));
            bool actualJump = distanceSquared > 1e-6;
            // if distance is too large, this means, these points are actually different; even considering multiple positions.
            // for drawing, if there are multiple positions at the concatenation point, we should be wary, because the different segments might me far apart (converging to the different positions).
            bool visualJump = curve[curve.Length - 1e-6f].DistanceSquared(nextCurve[1e-6f]) > 1e-3f;
            if (actualJump || angleJump || visualJump)
            {
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
        end ??= Length;
        if (start < 0 || end > Length || start > end)
            throw new Exception("Invalid restriction");
        var movedStartTime = start;
        var movedEndTime = end.Value;
        int startSegmentIndex = -1, endSegmentIndex = segments.Length;
        for (int i = 0; i < segments.Length; i++)
        {
            var segmentLength = segments[i].Length;
            if (startSegmentIndex == -1)
            {
                if (movedStartTime > segmentLength)
                {
                    movedStartTime -= segmentLength;
                    movedEndTime -= segmentLength;
                    continue;
                }
                startSegmentIndex = i;
            }

            if (movedEndTime > segmentLength)
            {
                movedEndTime -= segmentLength;
                continue;
            }
            endSegmentIndex = i;
            break;
        }
        if (startSegmentIndex == endSegmentIndex)
            return segments[startSegmentIndex].Restrict(movedStartTime, movedEndTime);
        
        var firstSegment = segments[startSegmentIndex].Restrict(movedStartTime, segments[startSegmentIndex].Length);
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

public partial class ReverseCurve : Curve
{
    private readonly Curve curve;

    public ReverseCurve(Curve curve)
    {
        this.curve = curve;
    }

    [CanBeNull] private string _name;
    public override string Name
    {
        get => _name ?? curve.Name + '\'';
        set => _name = value;
    }

    public override float Length => curve.Length;
    public override Point EndPosition => curve.StartPosition;
    public override Point StartPosition => curve.EndPosition;
    public override TangentVector EndVelocity => - curve.StartVelocity;
    public override TangentVector StartVelocity => - curve.EndVelocity;
    public override Surface Surface => curve.Surface;

    public override IEnumerable<float> VisualJumpTimes => from t in curve.VisualJumpTimes select Length - t;

    public override Point ValueAt(float t) => curve.ValueAt(Length - t);
    public override TangentVector DerivativeAt(float t) => - curve.DerivativeAt(Length - t);

    public override Curve Reversed() => curve;

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Reversed();

    public override Curve Copy() => new ReverseCurve(curve.Copy()) { Color = Color }; // in case it was changed
}

public class RestrictedCurve : Curve
{
    
    private readonly Curve curve;
    private readonly float start;
    private readonly float end;

    public RestrictedCurve(Curve curve, float start, float end, string name = null)
    {
        if (start < 0 && start.ApproximateEquals(0))
            start = 0;
        if (end > curve.Length && end.ApproximateEquals(curve.Length))
            end = curve.Length;
        if (start > end && start.ApproximateEquals(end))
            end = start;
        if (start < 0 || end > curve.Length || start > end)
            throw new Exception("Invalid restriction");
        this.curve = curve;
        this.start = start;
        this.end = end;
        _name = name;
    }

    [CanBeNull] private string _name;
    public override string Name
    {
        get => _name ?? curve.Name + $"[{start:g2}, {end:g2}]";
        set => _name = value;
    }

    public override float Length => end - start;
    public override Point EndPosition => curve[end];
    public override Point StartPosition => curve[start];
    public override TangentVector EndVelocity => curve.DerivativeAt(end);
    public override TangentVector StartVelocity => curve.DerivativeAt(start);
    public override Surface Surface => curve.Surface;

    public override IEnumerable<float> VisualJumpTimes => from t in curve.VisualJumpTimes where t > start && t < end select t - start;

    protected override Color DefaultColor => curve.Color;

    public override Point ValueAt(float t) => curve.ValueAt(t + start);
    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t + start);

    public override Curve Reversed() => reverseCurve ??=
        new RestrictedCurve(curve.Reversed(), curve.Length - end, curve.Length - start) { Name = Name + "'", Color = Color };

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Restrict(start, end);

    public override Curve Restrict(float start, float? end = null) => new RestrictedCurve(curve, this.start + start, this.start + (end ?? Length));

    public override Curve Copy() =>
        new RestrictedCurve(curve.Copy(), start, end)
        { Name = Name, Color = Color };
}


public class BasicParametrizedCurve : Curve
{
    private readonly Func<float, Vector3> value;
    private readonly Func<float, Vector3> derivative;
    public BasicParametrizedCurve(string name, float length, Surface surface, Func<float, Vector3> value, Func<float, Vector3> derivative, IEnumerable<float> visualJumpTimes = null)
    {
        Length = length;
        Surface = surface;
        this.value = value;
        this.derivative = derivative;
        VisualJumpTimes = visualJumpTimes ?? Enumerable.Empty<float>();
        Name = name;
    }

    public override string Name { get; set; }
    public override float Length { get; }

    public override Surface Surface { get; }
    public override Point ValueAt(float t) => value(t);

    public override TangentVector DerivativeAt(float t) => new TangentVector(value(t), derivative(t));
    public override Curve Copy() => new BasicParametrizedCurve(Name, Length, Surface, value, derivative, VisualJumpTimes) { Color = Color };

    public override IEnumerable<float> VisualJumpTimes { get; }
}

public class ParametrizedCurve : Curve
{
    
    private readonly Func<float, TangentVector> tangent;
    public ParametrizedCurve(string name, float length, Surface surface, Func<float, TangentVector> tangent,
        IEnumerable<float> visualJumpTimes = null)
    {
        Length = length;
        Surface = surface;
        this.tangent = tangent;
        Name = name;
        VisualJumpTimes = visualJumpTimes ?? Enumerable.Empty<float>();
    }

    public override string Name { get; set; }
    public override float Length { get; }

    public override Surface Surface { get; }
    public override Point ValueAt(float t) => tangent(t).point;

    public override TangentVector DerivativeAt(float t) => tangent(t);
    public override Curve Copy() => new ParametrizedCurve(Name, Length, Surface, tangent, VisualJumpTimes) { Color = Color };

    public override IEnumerable<float> VisualJumpTimes { get; }
}

public class SplineSegment : InterpolatingCurve
{
    public SplineSegment(TangentVector startVelocity, TangentVector endVelocity, float length, Surface surface, string name) : base(startVelocity, endVelocity, length, surface, name)
    {
    }

    public override Point ValueAt(float t)
    {
        float s = t / Length;
        float s2 = s * s;
        float s3 = s2 * s;
        var (start, vStart) = Length * StartVelocity;
        var (end, vEnd) = Length * EndVelocity;
        var d = start.Position;
        var δ = end.Position - d;
        var c = vStart;
        var b = 3 * δ - vEnd;
        var a = vEnd - vStart - 2 * δ;
        
        return a * s3 + b * s2 + c * s + d;
    }

    public override TangentVector DerivativeAt(float t)
    {
        float s = t / Length;
        float s2 = s * s;
        float s3 = s2 * s;
        var (start, vStart) = Length * StartVelocity;
        var (end, vEnd) = Length * EndVelocity;
        var d = start.Position;
        var δ = end.Position - d;
        var c = vStart;
        var b = 3 * δ - vEnd;
        var a = vEnd - vStart - 2 * δ;

        var pos = a * s3 + b * s2 + c * s + d;
        var velocity = a * (3 * s2) + b * (2 * s) + c;

        return new TangentVector(pos, velocity / Length);
    }

    public override Curve Copy() => new SplineSegment(StartVelocity, EndVelocity, Length, Surface, Name) { Color = Color };
}
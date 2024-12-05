using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;


public abstract class Curve: ITransformable<Curve>
{
    public readonly int id;
    private static int _lastId;
    protected Curve()
    {
        id = _lastId++;
    }
    
    public abstract string Name { get; set; }
    
    public abstract float Length { get; }

    public virtual Point StartPosition => ValueAt(0);
    public virtual Point EndPosition => ValueAt(Length);
    public virtual TangentVector StartVelocity => DerivativeAt(0);
    public virtual TangentVector EndVelocity => DerivativeAt(Length);

    public abstract Surface Surface { get; }

    public virtual Color Color => colors[id % colors.Count];
    public virtual IEnumerable<float> VisualJumpTimes => Enumerable.Empty<float>();

    public virtual Point this[float t] => ValueAt(t);

    public abstract Point ValueAt(float t);
    public abstract TangentVector DerivativeAt(float t);
    
    public virtual Curve Concatenate(Curve curve) => new ConcatenatedCurve(new Curve[] { this, curve });

    private Curve reverseCurve;
    private static readonly List<Color> colors = new()
    {
        Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, new Color(1, 0.5f, 0), new Color(0.5f, 0, 1)
    };

    public virtual Curve Reverse() => reverseCurve ??= new ReverseCurve(this);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="point"></param>
    /// <returns>the returned Point is a ModelSurfaceBoundaryPoint</returns>
    public virtual (float, Point) GetClosestPoint(Vector3 point)
    {
        // If curve has more than one position at any time, we should optimize over all of them? This is not clear from the curve. Thus this has to be implemented in the subclasses that have multiple positions. Done for the ModelSurfaceSide.
        (float, Point) f(float t)
        {
            var pointOnCurve = this[t]; // is ModelSurfaceBoundaryPoint
            return ((pointOnCurve.Position - point).sqrMagnitude, pointOnCurve);
            // return (pointOnCurve.Distance(point), pointOnCurve);
        }

        float fDeriv(float t) => 2 * Vector3.Dot(DerivativeAt(t).vector, this[t].Position - point);

        float learningRate = 0.01f;
        float t = Length / 2;
        for (var i = 0; i < 1000; i++)
        {
            float gradient = fDeriv(t);
            float change = learningRate * gradient;
            t -= change;
            if (t < 0) return (0, StartPosition);
            if (t > Length) return (Length, EndPosition);

            var (dist, pos) = f(t);
            if (Mathf.Abs(change) < 1e-6)
                return (t, pos);
            if (dist < 1e-6)
                return (t, pos);
        }

        return (t, this[t]);
    }

    public virtual Curve ApplyHomeomorphism(Homeomorphism homeomorphism) => homeomorphism.isIdentity ? this : new TransformedCurve(this, homeomorphism);

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

    public virtual Curve Restrict(float start, float end)
    {
        if (start < 0 || end > Length || start > end)
            throw new Exception("Invalid restriction");
        return new RestrictedCurve(this, start, end);
    }
}

public class TransformedCurve : Curve
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
    public override Color Color => curve.Color;
    public override Point ValueAt(float t) => curve.ValueAt(t).ApplyHomeomorphism(homeomorphism);

    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t).ApplyHomeomorphism(homeomorphism);
    // we should have a TangentVector class that is transformable. 

    public override TangentSpace BasisAt(float t) => curve.BasisAt(t).ApplyHomeomorphism(homeomorphism);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism) =>
        new TransformedCurve(curve, homeomorphism * this.homeomorphism);
}

public class ConcatenatedCurve : Curve
{
    private readonly Curve[] segments;
    private List<ConcatenationSingularPoint> nonDifferentiablePoints;


    public override string Name { get; set; }

    public override Surface Surface => segments.First().Surface;

    public override float Length { get; }

    public override IEnumerable<float> VisualJumpTimes => from singularPoint in NonDifferentiablePoints where singularPoint.visualJump select singularPoint.time;

    private List<ConcatenationSingularPoint> NonDifferentiablePoints => nonDifferentiablePoints ??= CalculateSingularPoints(segments);
    public override Color Color => segments.First().Color;

    public ConcatenatedCurve(IEnumerable<Curve> curves, string name = null)
    {
        segments = curves.SelectMany(
            curve => curve is ConcatenatedCurve concatenatedCurve ? concatenatedCurve.segments : new []{ curve }
        ).ToArray();

        float length = (from segment in segments select segment.Length).Sum();
        if (length == 0) throw new Exception("Length of curve is zero");
        Length = length;
        
        Name = name ?? string.Join(" -> ", from segment in segments select segment.Name);
    }

    public ConcatenatedCurve Smoothed()
    {
        var curves = segments.ToList();
        
        var singularPoints = CalculateSingularPoints(curves, ignoreSubConcatenatedCurves: true);

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
            var centerVector = new TangentVector(incomingCurve.EndPosition.Positions.ElementAt(singularPoint.incomingPosIndex), Complex.FromPolarCoordinates(length, angle).ToVector3());          
            var endOfLastInterpolated = restrictedOutgoingCurve.StartVelocity;

            var firstInterpolated = new SplineSegment(
                startOfFirstInterpolated, centerVector,incomingCurve.Length * 0.1f,  Surface,  Name + " interp. segment 1"             
            );

            var secondInterpolated = new SplineSegment(
                centerVector, endOfLastInterpolated, incomingCurve.Length * 0.1f, Surface, Name + " interp. segment 2"
            );
            
            curves.Insert(index + 1, firstInterpolated);
            curves.Insert(index + 2, secondInterpolated);
        }

        var result = new ConcatenatedCurve(curves, Name + " (smooth)");
        // result.nonDifferentiablePoints = ...
        return result; 
    }

    public override Point EndPosition => segments.Last().EndPosition;
    public override Point StartPosition => segments.First().StartPosition;
    public override TangentVector EndVelocity => segments.Last().EndVelocity;
    public override TangentVector StartVelocity => segments.First().StartVelocity;

    public override Point ValueAt(float t)
    {
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

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        if (homeomorphism.isIdentity)
            return this;
        return new ConcatenatedCurve(from segment in segments select segment.ApplyHomeomorphism(homeomorphism),
            Name + " --> " + homeomorphism.target.Name
        );
    }


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
            // if distance is too l°arge, this means, these points are actually different; even considering multiple positions.
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
}

public class ReverseCurve : Curve
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
    public override Color Color => curve.Color;

    public override Point ValueAt(float t) => curve.ValueAt(Length - t);
    public override TangentVector DerivativeAt(float t) => - curve.DerivativeAt(Length - t);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Reverse();
}

public class RestrictedCurve : Curve
{
    
    private readonly Curve curve;
    private readonly float start;
    private readonly float end;

    public RestrictedCurve(Curve curve, float start, float end)
    {
        this.curve = curve;
        this.start = start;
        this.end = end;
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
    public override Color Color => curve.Color;

    public override Point ValueAt(float t) => curve.ValueAt(t + start);
    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t + start);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Restrict(start, end);
}


public class BasicParametrizedCurve : Curve
{
    private readonly Func<float, Vector3> value;
    private readonly Func<float, Vector3> derivative;
    public BasicParametrizedCurve(string name, float length, Surface surface, Func<float, Vector3> value, Func<float, Vector3> derivative)
    {
        Length = length;
        Surface = surface;
        this.value = value;
        this.derivative = derivative;
        Name = name;
    }

    public override string Name { get; set; }
    public override float Length { get; }

    public override Surface Surface { get; }
    public override Point ValueAt(float t) => value(t);

    public override TangentVector DerivativeAt(float t) => new TangentVector(value(t), derivative(t));
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
}
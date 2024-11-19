using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using Unity.VisualScripting;
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

    public virtual Point StartPosition => this[0];
    public virtual Point EndPosition => this[Length];
    public virtual TangentVector StartVelocity => DerivativeAt(0);
    public virtual TangentVector EndVelocity => DerivativeAt(Length);

    public abstract Surface Surface { get; }

    public virtual IEnumerable<Point> NonDifferentiablePoints => Enumerable.Empty<Point>();
    public virtual Color Color => colors[id % colors.Count];

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
        // todo? If curve has more than one position at any time, we should optimize over all of them?
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

            var res = f(t);
            if (Mathf.Abs(change) < 1e-6)
                return res;
            if (Mathf.Abs(res.Item1) < 1e-6)
                return res;
        }

        return f(t);
    }

    public virtual Curve ApplyHomeomorphism(Homeomorphism homeomorphism) => new TransformedCurve(this, homeomorphism);

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
    private IEnumerable<Point> nonDifferentiablePoints;


    public override string Name { get; set; }

    public override Surface Surface => segments.First().Surface;

    public override float Length { get; }

    public override IEnumerable<Point> NonDifferentiablePoints => nonDifferentiablePoints ??= CalculateSingularPoints(segments);
    public override Color Color => segments.First().Color;

    public ConcatenatedCurve(IEnumerable<Curve> curves, string name = null)
    {
        segments = curves.ToArray();

        float length = (from segment in segments select segment.Length).Sum();
        if (length == 0) throw new Exception("Length of curve is zero");
        Length = length;
        
        Name = name ?? string.Join(" -> ", from segment in segments select segment.Name);
    }

    public ConcatenatedCurve Smoothed()
    {
        var curves = segments.ToList();
        
        var singularPoints = CalculateSingularPoints(curves);

        List<int> singularIndices = singularPoints.Select(singularPoint => curves.IndexOf(singularPoint.incomingCurve)).ToList();


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

            var firstInterpolated = new SplineSegment(Name + "segment", incomingCurve.Length * 0.1f, 
                startOfFirstInterpolated.point.Position, centerVector.point.Position,
                startOfFirstInterpolated.vector, centerVector.vector, Surface                    
            );
            
            var secondInterpolated = new SplineSegment(Name + "segment", incomingCurve.Length * 0.1f, 
                centerVector.point.Position, endOfLastInterpolated.point.Position,
                centerVector.vector, endOfLastInterpolated.vector, Surface                    
            );
            
            curves.Insert(index + 1, firstInterpolated);
            curves.Insert(index + 2, secondInterpolated);
        }

        return new ConcatenatedCurve(curves, Name + " (smooth)");
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
        return new ConcatenatedCurve(from segment in segments select segment.ApplyHomeomorphism(homeomorphism),
            Name + " --> " + homeomorphism.target.Name
        );
    }


    private static List<ConcatenationSingularPoint> CalculateSingularPoints(IReadOnlyList<Curve> segments)
    {
        Curve nextCurve;
        Curve lastCurve = nextCurve = segments[^1];
        List<ConcatenationSingularPoint> res = new();
        for (int i = 0; i <= segments.Count - 2; i++)
        {
            var curve = nextCurve;
            res.AddRange(curve.NonDifferentiablePoints);
            
            nextCurve = segments[i];
            var (herePosIndex, therePosIndex, distanceSquared) = curve.EndPosition.ClosestPositionIndices(nextCurve.StartPosition);
            // if (!curve.EndPosition.Equals(nextCurve.StartPosition))
            if (distanceSquared > 1e-6 || 
                !curve.EndVelocity.VectorAtPositionIndex(herePosIndex).ApproximatelyEquals(
                    nextCurve.StartVelocity.VectorAtPositionIndex(therePosIndex))
            )
                res.Add(new ConcatenationSingularPoint()
                {
                    incomingCurve = curve,
                    outgoingCurve = nextCurve, 
                    incomingPosIndex = herePosIndex,
                    outgoingPosIndex = therePosIndex
                }); // save angle?
        }
        res.AddRange(lastCurve.NonDifferentiablePoints);
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

    public override Point ValueAt(float t) => curve.ValueAt(t);
    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(Length - t);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Restrict(start, end);
}


public class BasicCurve : Curve
{
    private readonly Func<float, Vector3> value;
    private readonly Func<float, Vector3> derivative;
    public BasicCurve(string name, float length, Surface surface, Func<float, Vector3> value, Func<float, Vector3> derivative)
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

public class SplineSegment : GeodesicSegment
{
    public SplineSegment(string name, float length, Vector3 start, Vector3 end, Vector3 startVelocity, Vector3 endVelocity, Surface surface): 
        base(start, end, startVelocity, endVelocity, length, surface, name)
    {}

    public override Point ValueAt(float t) => throw new NotImplementedException();

    public override TangentVector DerivativeAt(float t) => throw new NotImplementedException();
}
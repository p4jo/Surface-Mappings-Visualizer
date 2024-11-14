using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;


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

    public abstract Point EndPosition { get; }
    public abstract Point StartPosition { get; }
    public abstract Vector3 EndVelocity { get; }
    public abstract Vector3 StartVelocity { get; }

    public abstract Surface Surface { get; }

    public virtual IEnumerable<Point> NonDifferentiablePoints => Enumerable.Empty<Point>();
    public virtual Color Color => colors[id % colors.Count];

    public virtual Point this[float t] => ValueAt(t);

    public abstract Point ValueAt(float t);
    public abstract TangentVector DerivativeAt(float t);
    
    public virtual Curve Concatenate(Curve curve) => new ConcatenatedCurve(new Curve[] { this, curve });

    private Curve reverseCurve;
    private static List<Color> colors = new()
    {
        Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, new Color(1, 0.5f, 0), new Color(0.5f, 0, 1)
    };

    public virtual Curve Reverse() => reverseCurve ??= new ReverseCurve(this);

    public virtual float GetClosestPoint(Vector3 point)
    {
        // todo: difference for points
        float f(float t) => (point - this[t].Position).sqrMagnitude;
        float fDeriv(float t) => 2 * Vector3.Dot(DerivativeAt(t).vector, this[t].Position - point);

        float learningRate = 0.01f;
        float t = Length / 2;
        for (var i = 0; i < 1000; i++)
        {
            float gradient = fDeriv(t);
            float change = learningRate * gradient;
            t -= change;
            if (t < 0) return 0;
            if (t > Length) return Length;

            if (Mathf.Abs(change) < 1e-6 || Mathf.Abs(f(t)) < 1e-6)
                break;
        }
        return t;
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
    public override Vector3 EndVelocity => homeomorphism.df(EndPosition.Position) * curve.EndVelocity; 
    public override Vector3 StartVelocity => homeomorphism.df(StartPosition.Position) * curve.StartVelocity;
    public override Surface Surface => homeomorphism.target;
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

    public override IEnumerable<Point> NonDifferentiablePoints => nonDifferentiablePoints ??= CalculateSingularPoints();

    public ConcatenatedCurve(IEnumerable<Curve> curves, string name = null)
    {
        segments = curves.ToArray();

        float length = (from segment in segments select segment.Length).Sum();
        if (length == 0) throw new Exception("Length of curve is zero");
        Length = length;
        
        Name = name ?? string.Join(" -> ", from segment in segments select segment.Name);
    }

    public override Point EndPosition => segments.Last().EndPosition;
    public override Point StartPosition => segments.First().StartPosition;
    public override Vector3 EndVelocity => segments.Last().EndVelocity;
    public override Vector3 StartVelocity => segments.First().StartVelocity;

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


    private IEnumerable<Point> CalculateSingularPoints()
    {
        Curve nextCurve = segments.Last();
        List<Point> res = new();
        for (int i = 0; i <= segments.Length - 2; i++)
        {
            var curve = nextCurve;
            nextCurve = segments[i];
            if (curve.EndPosition != nextCurve.StartPosition)
                res.Add(new ConcatenationJumpPoint() { incomingCurve = curve, outgoingCurve = nextCurve });
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
    public override Vector3 EndVelocity => - curve.StartVelocity;
    public override Vector3 StartVelocity => - curve.EndVelocity;
    public override Surface Surface => curve.Surface;

    public override Point ValueAt(float t) => curve.ValueAt(Length - t);
    public override TangentVector DerivativeAt(float t) => - curve.DerivativeAt(Length - t);

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Reverse();
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class InterpolatingCurve: Curve
{
    protected InterpolatingCurve(TangentVector startVelocity, TangentVector endVelocity, float length, Surface surface, string name)
    {
        StartVelocity = startVelocity;
        EndVelocity = endVelocity;
        Length = length;
        Surface = surface;
        Name = name;
    }
    
    protected InterpolatingCurve((TangentVector, TangentVector, float) data, Surface surface, string name)
        : this(data.Item1, data.Item2, data.Item3, surface, name) { }
    public override string Name { get; set; }
    public override Point StartPosition => StartVelocity.point;
    public override Point EndPosition => EndVelocity.point;
    public override TangentVector StartVelocity { get; }
    public override TangentVector EndVelocity { get; }
    public override Surface Surface { get; }
    public override float Length { get; }
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

    public override Curve Copy() => new ParametrizedCurve(Name, Length, Surface, tangent, VisualJumpTimes)
        { Color = Color };

    public override IEnumerable<float> VisualJumpTimes { get; }
}

public class BasicParametrizedCurve : Curve
{
    private readonly Func<float, Vector3> value;
    private readonly Func<float, Vector3> derivative;

    public BasicParametrizedCurve(string name, float length, Surface surface, Func<float, Vector3> value,
        Func<float, Vector3> derivative, IEnumerable<float> visualJumpTimes = null)
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

    public override Curve Copy() =>
        new BasicParametrizedCurve(Name, Length, Surface, value, derivative, VisualJumpTimes) { Color = Color };

    public override IEnumerable<float> VisualJumpTimes { get; }
}
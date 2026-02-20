using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

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
    // ReSharper disable once CompareOfFloatsByEqualityOperator
    public override Point EndPosition => end == curve.Length ? curve.EndPosition : curve[end];
    public override Point StartPosition => start == 0 ? curve.StartPosition : curve[start];
    public override TangentVector EndVelocity => end == curve.Length ? curve.EndVelocity : curve.DerivativeAt(end);
    public override TangentVector StartVelocity => start == 0 ? curve.StartVelocity : curve.DerivativeAt(start);
    public override Surface Surface => curve.Surface;

    public override IEnumerable<float> VisualJumpTimes => from t in curve.VisualJumpTimes where t > start && t < end select t - start;

    protected override Color DefaultColor => curve.Color;

    public override Point ValueAt(float t) => curve.ValueAt(t + start);
    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t + start);

    public override Curve Reversed() => reverseCurve ??=
        new RestrictedCurve(curve.Reversed(), curve.Length - end, curve.Length - start) { Name = Name.EndsWith("'") ? Name : Name + "'", Color = Color, reverseCurve = this };

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
        => curve.ApplyHomeomorphism(homeomorphism).Restrict(start, end);

    public override Curve Restrict(float start, float? end = null)
    {
        var stop = end ?? Length;
        if (start < -restrictTolerance || stop > Length + restrictTolerance || start > stop + restrictTolerance)
            throw new Exception($"Invalid restriction in curve {this}: {start} to {end} for length {Length}");
        if (stop > Length) 
            stop = Length;
        if (start < 0) 
            start = 0;
        if (start < restrictTolerance && stop > Length - restrictTolerance)
            return this;
        return new RestrictedCurve(curve, this.start + start,  end.HasValue ? this.start + end.Value : this.end) { Color = Color };
    }

    public override Curve Copy() =>
        new RestrictedCurve(curve.Copy(), start, end)
            { Name = Name, Color = Color };
}
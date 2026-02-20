using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

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
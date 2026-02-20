using System.Collections.Generic;
using JetBrains.Annotations;

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

    [CanBeNull] private string _name;
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
    // TODO: Feature / Bug. Implement this! If this maps to a model surface, we need to check for visual jumps in the transformed curve!
    // If this maps to a parametrized surface, from a model surface, we could forget these

    public override Point ValueAt(float t) => curve.ValueAt(t).ApplyHomeomorphism(homeomorphism);

    public override TangentVector DerivativeAt(float t) => curve.DerivativeAt(t).ApplyHomeomorphism(homeomorphism);
    
    public override TangentSpace BasisAt(float t) => curve.BasisAt(t).ApplyHomeomorphism(homeomorphism);
    
    public override Curve Copy() => new TransformedCurve(curve.Copy(), homeomorphism) { Name = Name, Color = Color } ;

    public override Curve Reversed() => reverseCurve ??= new TransformedCurve(curve.Reversed(), homeomorphism) { Color = Color, reverseCurve = this };

    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism) =>
        new TransformedCurve(curve, homeomorphism * this.homeomorphism);
}
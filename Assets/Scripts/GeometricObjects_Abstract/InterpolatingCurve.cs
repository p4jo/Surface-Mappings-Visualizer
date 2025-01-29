using System.Numerics;
using UnityEngine;
using Math = System.Math;
using Vector3 = UnityEngine.Vector3;

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

public class FlatGeodesicSegment : InterpolatingCurve
{
    public FlatGeodesicSegment(Point start, Point end, Surface surface, string name) : base(
            ComputeData(start, end),
            surface,
            name
        ) {  }
    
    private static (TangentVector, TangentVector, float) ComputeData(Point start, Point end)
    {
        var direction = end.Position - start.Position;
        float length = direction.magnitude;
        direction /= length;
        return (new TangentVector(start, direction), new TangentVector(end, direction), length);
    }

    public override Point ValueAt(float t) => StartPosition.Position + StartVelocity.vector * t;
    public override TangentVector DerivativeAt(float t) => new(ValueAt(t), StartVelocity.vector);
    public override Curve Copy() => new FlatGeodesicSegment(StartPosition, EndPosition, Surface, Name) {Color = Color};
}

public class HyperbolicGeodesicSegment : Curve
{
    /// These are the coefficients of the Möbius transformation that sends i to StartPosition and i e^Length to EndPosition. It is an isometry from the upper half plane to either the upper half plane or the unit disk, depending on the bool diskModel.
    private Complex α, β, γ, δ;

    public override string Name { get; set; }
    
    private float length;
    public override float Length => length;
    public override Surface Surface { get; }

    public override Point StartPosition { get; }
    public override Point EndPosition { get; }

    private readonly bool diskModel;

    /// <summary>
    /// A geodesic segment in the hyperbolic plane. We use either the Poincaré disk model or the upper half plane model.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="surface"></param>
    /// <param name="name"></param>
    public HyperbolicGeodesicSegment(Point start, Point end, Surface surface, string name, bool diskModel = true) 
    {
        Name = name;
        Surface = surface;
        StartPosition = start;
        EndPosition = end;
        this.diskModel = diskModel;
        Initialize(start, end);
    }

    private void Initialize(Point start, Point end)
    {
        double a = 0, b = 0, c = 0, d = 0;
        Complex p = start.Position.ToComplex();
        Complex q = end.Position.ToComplex();
        if (diskModel)
        {
            // multiply by the inverse of the Cayley transform (1  -i \\ 1  i); this maps the unit disk isometrically to the upper half plane.
            p = - Complex.ImaginaryOne * (p + 1) / (p - 1);
            q = - Complex.ImaginaryOne * (q + 1) / (q - 1);
        }
        // p, q are in the upper half plane model 
        if (p.Imaginary < 0 || q.Imaginary < 0)
            Debug.LogError("Calculation error: Points are outside the hyperbolic plane.");
        if (p.Imaginary < 1e-6)
            p = new Complex(p.Real, 1e-6);
        if (q.Imaginary < 1e-6)
            q = new Complex(q.Real, 1e-6);

        Complex w = (p - q) * (p - Complex.Conjugate(q));
        if (!AssignIsometry(true) && !AssignIsometry(false))
            Debug.LogError("Calculation error: The Möbius transformation didn't send q to the preferred half-axis.");

        if (diskModel)
        {
            // invert and multiply by the Cayley transform (1  -i \\ 1  i) which isometrically maps the upper half plane to the unit disk.
            α = new Complex(d, c); 
            β = new Complex(-b, -a);
            γ = new Complex(d, -c); 
            δ = new Complex(-b, a);
        }
        else
        {
            // invert
            α = d;
            β = -b;
            γ = -c;
            δ = a;
        }
        
        return;

        // This is the Möbius transformation that sends p to i and q to the imaginary axis.
        bool AssignIsometry(bool alternative)
        {
            if (w.Magnitude < 1e-6)
            {
                if (alternative)
                {
                    a = 0;
                    b = -p.Imaginary;
                    c = 1;
                    d = -p.Real;
                }
                else
                {
                    a = 1;
                    b = -p.Real;
                    c = 0;
                    d = p.Imaginary;
                }
            }
            else
            {
                a = alternative ? - w.Real + w.Magnitude : - w.Real - w.Magnitude;
                c = 2 * p.Imaginary * (q.Real - p.Real);
                b = - a * p.Real - c * p.Imaginary;
                d = a * p.Imaginary - c * p.Real;

                // The simplest and most well-known geodesic is the one starting at i running upwards at unit speed given by γ(t) = i * e^t.

            }
            Complex φq = (a * q + b) / (c * q + d); 
            // as noted; in the half-plane model this should be on the imaginary axis. This is equivalent to being in the real axis in the disk model.

            
            if (φq.Real / φq.Imaginary is > 2e-4  or < -2e-4) 
                Debug.LogError("Calculation error: The Möbius transformation didn't send q to the preferred axis");
            if (φq.Imaginary >= 1)
            {
                length = Mathf.Log((float) φq.Imaginary);
                return true;
            }
            return false;
        }
    }


    public override Point ValueAt(float t)
    {
        var x = new Complex(0, Math.Exp(t));
        return ((α * x + β) / (γ * x + δ)).ToVector3();
    }

    public override TangentVector DerivativeAt(float t)
    {       
        var x = new Complex(0, Math.Exp(t));
        var numerator = (α * x + β);
        var denominator = (γ * x + δ);
        var value = numerator / denominator;
        var derivative = (α * x * denominator - numerator * γ * x) / denominator / denominator;
        return new TangentVector(value.ToVector3(), derivative.ToVector3());
    }

    public override Curve Reversed() => reverseCurve ??= new HyperbolicGeodesicSegment(EndPosition, StartPosition, Surface, Name, diskModel);
    public override Curve Copy() => new HyperbolicGeodesicSegment(StartPosition, EndPosition, Surface, Name, diskModel) {Color = Color};
}

public class SphericalGeodesicSegment : InterpolatingCurve
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end, Surface surface, string name)
        : base(null, null, 0, null, null)
    {  throw new System.NotImplementedException(); }

    public override Point ValueAt(float t) => throw new System.NotImplementedException();

    public override TangentVector DerivativeAt(float t) => throw new System.NotImplementedException();
    public override Curve Copy() => new SphericalGeodesicSegment(StartPosition.Position, EndPosition.Position, Surface, Name);
}
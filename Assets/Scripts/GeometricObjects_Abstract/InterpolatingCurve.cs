using System.Numerics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.UIElements;
using Math = System.Math;
using Vector3 = UnityEngine.Vector3;

public abstract class InterpolatingCurve: Curve
{
    public override string Name { get; set; }
    public override Point StartPosition => StartVelocity.point;
    public override Point EndPosition => EndVelocity.point;
    public override TangentVector StartVelocity { get; }
    public override TangentVector EndVelocity { get; }
    public override Surface Surface { get; }
    public override float Length { get; }

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
}

public class HyperbolicGeodesicSegment : Curve
{
    private Complex a, b, c, d;

    public override string Name { get; set; }
    public override float Length { get; }
    public override Surface Surface { get; }

    public override Point StartPosition { get; }
    public override Point EndPosition { get; }

    public readonly bool diskModel = true;

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
        Complex p = start.Position.ToComplex();
        Complex q = end.Position.ToComplex();
        if (diskModel)
        {
            p = - Complex.ImaginaryOne * (p + 1) / (p - 1);
            q = - Complex.ImaginaryOne * (q + 1) / (q - 1);
        }
        // p, q are in the upper half plane model 
        
        Complex w = (p - q) * (p - Complex.Conjugate(q));
        if (AssignVariables(w.Real + w.Magnitude)) return;
        if (!AssignVariables(w.Real - w.Magnitude))
            Debug.LogError("Calculation error: The Möbius transformation didn't send q to the preferred half-axis.");
        return;

        bool AssignVariables(double α)
        {
            Complex ζ = new Complex(α, 2);
            Complex ξ = - p * ζ;

            if (!diskModel)
            {
                a = -α;
                b = ξ.Real;
                c = 2;
                d = -ξ.Imaginary;
                // this is the isometry of the upper half plane that sends p to i and q to the imaginary axis (for the values of α above).
                // The simplest and most well-known geodesic is the one starting at i running upwards at unit speed given by γ(t) = i * e^t.
                
                Complex f_q = (a * q + b) / (c * q + d); // as noted; in the half-plane model this should be on the imaginary axis. This is equivalent to being in the real axis in the disk model.
                if (f_q.Real is > 1e-6 or < -1e-6) 
                    Debug.LogError("Calculation error: The Möbius transformation didn't send q to the preferred axis");
                return f_q.Imaginary > 1;
            }
            else
            {
                // afterwards transform back to the disk model
                a = Complex.Conjugate(ζ);
                b = ξ;
                c = ζ;
                d = Complex.Conjugate(ξ);
                
                Complex f_q = (a * q + b) / (c * q + d);
                if (f_q.Imaginary is > 1e-6 or < -1e-6) 
                    Debug.LogError("Calculation error: The Möbius transformation didn't send q to the preferred axis");
                return f_q.Real > 0;
            }

            
        }
    }


    public override Point ValueAt(float t)
    {
        Complex x = Math.Exp(t);
        return ((a * x + b) / (c * x + d)).ToVector3();
    }

    public override TangentVector DerivativeAt(float t)
    {
        Complex x = Math.Exp(t);
        Complex denominator = c * x + d;
        Complex numerator = a * x + b;
        Complex value = numerator / denominator;
        Complex derivative = (a * x * denominator - numerator * c * x) / denominator / denominator;
        return new TangentVector(value.ToVector3(), derivative.ToVector3());
    }
}

public class SphericalGeodesicSegment : InterpolatingCurve
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end, Surface surface, string name)
        : base(null, null, 0, null, null)
    {  throw new System.NotImplementedException(); }

    public override Point ValueAt(float t) => throw new System.NotImplementedException();

    public override TangentVector DerivativeAt(float t) => throw new System.NotImplementedException();
}
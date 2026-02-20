using UnityEngine;

public class SplineSegment : InterpolatingCurve
{
    private Vector3 a, b, c, d;
    public SplineSegment(TangentVector startVelocity, TangentVector endVelocity, float length, Surface surface, string name) : base(startVelocity, endVelocity, length, surface, name)
    {
        var (start, vStart) = length * startVelocity;
        var (end, vEnd) = length * endVelocity;
        c = vStart;
        d = start.Position;
        var δ = end.Position - d;
        a = vEnd + c - 2 * δ;
        b = - vEnd - 2 * c + 3 * δ; 
    }

    public override Point ValueAt(float t)
    {
        float s = t / Length;
        float s2 = s * s;
        float s3 = s2 * s;
        return a * s3 + b * s2 + c * s + d;
    }

    public override TangentVector DerivativeAt(float t)
    {
        float s = t / Length;
        float s2 = s * s;
        float s3 = s2 * s;

        var pos = a * s3 + b * s2 + c * s + d;
        var velocity = a * (3 * s2) + b * (2 * s) + c;

        return new TangentVector(pos, velocity / Length);
    }

    public override Curve Copy() => new SplineSegment(StartVelocity, EndVelocity, Length, Surface, Name) { Color = Color };
}
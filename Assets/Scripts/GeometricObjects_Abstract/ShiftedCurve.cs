using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShiftedCurve : Curve
{
    private readonly Curve curve;
    private readonly Func<float, float> shift;

    public enum ShiftType
    {
        /// <summary>
        /// shift(t) = const.
        /// </summary>
        Uniform,
        /// <summary>
        /// shift(t) = const. * 1.69f * x / (0.15f + x) * (1 - x) / (1.15f - x) where x = t / curve.Length
        /// </summary>
        SymmetricFixedEndpoints,
        FixedEndpoint,
        FixedStartpoint
    }

    /// <summary>
    /// A curve that is shifted to the left using geodesics on the surface at every point of the curve.
    /// These short geodesics start at curve[t] into the direction curve.BasisAt(t).B.Normalized with length shift(t).
    /// Shifted curves compound: If you shift a shifted curve, the shift is added to the original shift.
    /// </summary>
    /// <param name="curve"></param>
    /// <param name="shift"></param>
    /// <param name="name"></param>
    /// <exception cref="ArgumentException"></exception>
    public ShiftedCurve(Curve curve, Func<float, float> shift, string name = null)
    {
        if (curve.Surface is not GeodesicSurface) 
            throw new ArgumentException($"To shift the curve {curve}, the underlying surface must be a GeodesicSurface.");
        if (curve is ShiftedCurve shiftedCurve)
        {
            this.curve = shiftedCurve.curve;
            this.shift = t => shiftedCurve.shift(t) + shift(t);    
        }
        else
        {
            this.curve = curve;
            this.shift = shift;
        }

        this.Name = name ?? curve.Name + " shifted by " + shift(curve.Length / 2);
    }

    public ShiftedCurve(Curve curve, float constant, ShiftType type = ShiftType.Uniform, string name = null) : 
        this(curve, shift: type switch
        {
            ShiftType.Uniform =>  t => constant,
            ShiftType.SymmetricFixedEndpoints => t =>
            {
                var x = t / curve.Length;
                return constant * 1.69f * x / (0.15f + x) * (1f - x) / (1.15f - x);
            },
            ShiftType.FixedEndpoint => t =>
            {
                var x = (1f + t / curve.Length) / 2f;
                return constant * 1.69f * x / (0.15f + x) * (1f - x) / (1.15f - x);
            },
            ShiftType.FixedStartpoint => t =>
            {
                var x = (t / curve.Length) / 2f;
                return constant * 1.69f * x / (0.15f + x) * (1f - x) / (1.15f - x);
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        }, name: name)
    {  }

    
    public sealed override string Name { get; set; }
    
    public override Curve Copy() => new ShiftedCurve(curve, shift, Name) {Color = Color};

    public override float Length => curve.Length;
    public override Surface Surface => curve.Surface;

    private List<float> _visualJumpTimes;
    public override IEnumerable<float> VisualJumpTimes
    {
        get
        {
            if (_visualJumpTimes != null) 
                return _visualJumpTimes;
            _visualJumpTimes = new List<float>();

            var res = 0.04f;
            const float goalRes = 0.0001f;
            foreach (var (t, sidePoint) in curve.VisualJumpPoints)
            {
                var lastJump = (_visualJumpTimes.Count == 0 ? 0 : _visualJumpTimes[^1]);
                
                var crossedVector = sidePoint.side.DerivativeAt(sidePoint.t).vector;
                var thisVector = curve.DerivativeAt( MathF.Max(t - res, (lastJump + 2f * t) / 3f )).vector;
                var angle = Vector3.Angle(crossedVector, thisVector) * Mathf.Deg2Rad;
                
                var guess = t + shift(t) / MathF.Tan(angle); 
                var time = guess;
                var lastGuess = guess;

                while (goalRes < res)
                {
                    bool haveHit = false;
                    float localCurveJump;
                    while (time >= lastGuess - 3 * res && time > lastJump)
                    {
                        localCurveJump = LocalCurveJump(time);
                        haveHit = haveHit || localCurveJump > -1;
                        if (localCurveJump >= MathF.Abs(shift(time)))
                            break;
                        time -= res;
                    }

                    res /= 2;
                    if (haveHit)
                        lastGuess = time;
                    else
                        time = lastGuess;
                    haveHit = false;

                    while (time <= lastGuess + 3 * res && time < curve.Length)
                    {
                        localCurveJump = LocalCurveJump(time);
                        haveHit = haveHit || localCurveJump > -1;
                        if (localCurveJump <= MathF.Abs(shift(time)))
                            break;
                        time += res;
                    } 
                    res /= 4;
                    if (haveHit)
                        lastGuess = time;
                    else
                        time = lastGuess;
                }
                if (time < 0f || time > curve.Length)
                    continue;
                _visualJumpTimes.Add(time);
            }
            
            return _visualJumpTimes;

            float LocalCurveJump(float t)
            {
                if (shift(t) == 0f)
                    return 0f;
                var visualJumpTime = LocalCurve(t, shift(t) * 3).VisualJumpTimes.FirstOrDefault();
                return visualJumpTime == 0f ? -1f : visualJumpTime;
            }
            // todo: Performance. Make more efficient (do it like in the definition of geodesic with start vector)
            
        }
    }

    private GeodesicSurface geodesicSurface => (GeodesicSurface) Surface;
    
    public override Point ValueAt(float t)
    {
        var s = shift(t);
        if (MathF.Abs(s) < 1e-5f)
            return curve.ValueAt(t);
        return LocalCurve(t, s).EndPosition;
    }

    private Curve LocalCurve(float t, float s) => CurveToRight(curve, t, s);

    public static Curve CurveToRight(Curve curve, float t, float s)
    {
        if (curve.Surface is not GeodesicSurface geodesicSurface)
            return null;
        var basis = curve.BasisAt(t);
        var localCurve = geodesicSurface.GetGeodesic(basis.B.Normalized, s, $"shift geodesic for {curve.Name} at time {t}");
        return localCurve;
    }

    public override TangentVector DerivativeAt(float t)
    {
        var s = shift(t);
        if (MathF.Abs(s) < 1e-5f)
            return curve.DerivativeAt(t);
        var basis = curve.BasisAt(t);
        var localCurve = geodesicSurface.GetGeodesic(basis.B.Normalized, s, $"shift geodesic for {curve.Name} at time {t}");
        return new (localCurve.EndPosition, basis.basis.a); // this is not really correct, but for small shifts it is good enough. It is unfeasible to calculate this correctly (derivative of exponential map...)
    }

    public override Curve Reversed() => reverseCurve ??=
        new ShiftedCurve(curve.Reversed(), t => - shift(Length - t), Name.EndsWith("'") ? Name : Name + "'") { Color = Color, reverseCurve =  this, _visualJumpTimes = (from jumpTime in VisualJumpTimes select Length - jumpTime).ToList() };

    public override Curve Restrict(float start, float? end = null)
    {
        var stop = end ?? Length;
        if (stop > Length - restrictTolerance)
            stop = Length;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (start == 0 && stop == Length)
            return this;
        return new ShiftedCurve(curve.Restrict(start, end), t => shift(t + start), Name + $"[{start:g2}, {end:g2}]")
            { Color = Color };
    }
}
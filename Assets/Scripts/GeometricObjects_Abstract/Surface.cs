using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Surface 
{
    public string Name { get; set; }
    public int Genus { get; protected set; }
    public readonly List<Point> punctures = new();
    public readonly bool is2D;

    protected Surface(string name, int genus, bool is2D)
    {
        this.Name = name;
        this.Genus = genus;
        this.is2D = is2D;
    }


    /// <summary>
    /// Bring the point into the boundary / significant point if it is close. Return null if too far from the surface.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="closenessThreshold"></param>
    /// <returns></returns>
    public abstract Point ClampPoint(Vector3? point, float closenessThreshold);

    
    /// <summary>
    /// A right-handed basis with the normal vector of the surface as the third vector.
    /// </summary>
    public abstract TangentSpace BasisAt(Point position);
    
    public abstract Vector3 MinimalPosition { get; }
    public abstract Vector3 MaximalPosition { get; }

    public virtual Homeomorphism GetAutomorphism(AutomorphismType type, IDrawnsformable[] parameters) => null;
}

public abstract class GeodesicSurface: Surface
{
    protected GeodesicSurface(string name, int genus, bool is2D) : base(name, genus, is2D){}

    public abstract Curve GetGeodesic(Point start, Point end, string name, GeodesicSurface surface = null);
    public abstract Curve GetGeodesic(TangentVector startVelocity, float length, string name, GeodesicSurface surface = null);

    public virtual Curve GetPathFromWaypoints(IEnumerable<Point> points, string name)
    {
        var pointArray = points.ToArray();
        var geodesicSegments = 
            from i in Enumerable.Range(0, pointArray.Length - 1)
            let start = pointArray[i]
            let end = pointArray[i+1]
            select GetGeodesic(start, end, name);
        
        return new ConcatenatedCurve(geodesicSegments, name, smoothed: true);
        // done: shouldn't smooth at the visual jump points, only at the actual concatenation points above. This could be done with "ignoreSubConcatenatedCurves", as we do, but only if we reintroduce nested concatenated curves. Currently, there is no distinction between the kinds of concatenations; Apart from the "angle jump" property of singular points, if it works
    }

    public virtual float Distance(Point startPoint, Point endPoint) => MathF.Sqrt(DistanceSquared(startPoint, endPoint));

    public abstract float DistanceSquared(Point startPoint, Point endPoint);

    public virtual float CurveLength(Curve curve)
    {
        var res = 0.1f;
        while (res > curve.Length / 4) res /= 2;
        var (lengths, lastLength, lastTimeInterval) = ArclengthFromTimeList(curve, res);
        return lastLength + lengths[^1];
    }

    static (List<float>, float, float) ArclengthFromTimeList(Curve curve, float res)
    {
        if (curve.Surface is not GeodesicSurface surface)
        {
            Debug.LogError("The curve must be on a GeodesicSurface to calculate the arclength parameterization.");
            int normalIntervals = Mathf.FloorToInt(curve.Length / res);
            float remainingLength = curve.Length - normalIntervals * res;
            return (Enumerable.Range(0, normalIntervals).Select(i => i * res).ToList(), remainingLength, remainingLength);

        }
        var lengths = new List<float> { 0f }; 
        // l -> t: times[i] = l[i * res]

        float T = curve.Length;
        var l = 0f;
        var lastPoint = curve.StartPosition;
        for (float t = res; t < T; t += res)
        {
            var p = curve[t];
            l += MathF.Sqrt(surface.DistanceSquared(p, lastPoint));
            lengths.Add(l);
        }
        var lastLength = MathF.Sqrt(surface.DistanceSquared(curve.EndPosition, lastPoint));
        var lastTimeInterval = curve.Length - (lengths.Count - 1) * res;
        return (lengths, lastLength, lastTimeInterval);
    }
    
    public static Func<float, float> ArclengthFromTime(Curve curve, float res = 0.05f)
    {
        var (lengths, lastLength, lastTimeInterval) = ArclengthFromTimeList(curve, res);
        return ListToFunction(lengths, lastLength, lastTimeInterval, res);
    }

    public static Func<float, float> TimeFromArclength(Curve curve, float res = 0.05f)
    {
        var (lengths, lastLength, lastTimeInterval) = ArclengthFromTimeList(curve, res);
        var f = ListToInverseDerivative(lengths, lastLength, lastTimeInterval, res);
        return x => f(x).Item1;
    }

    static Func<float, float> ListToFunction(List<float> lengths, float lastLength, float lastTimeInterval, float res) =>
        time =>
        {
            // todo: check
            int n = Mathf.FloorToInt(time / res);
            float deltaT = time - n * res;
            if (n < 0)
                return 0;
            if (n >= 0 && n < lengths.Count - 1)
                return lengths[n] + (lengths[n + 1] - lengths[n]) * deltaT / res;
            if (n == lengths.Count - 1)
                return lengths[n] + lastLength * deltaT / lastTimeInterval;
            return lastLength + lengths[^1];
        };

    static Func<float, (float, float)> ListToInverseDerivative(List<float> lengths, float lastLength,
        float lastTimeInterval, float res) =>
        length =>
        { // todo: check
            if (length <= 0) return (0f, 0f);
            float dtdl = lastTimeInterval / lastLength;
            float t = lengths[^1] + lastLength;
            
            if (length > lengths[^1] + lastLength)
                return (t, dtdl );
            
            int i = 0;
            while (i < lengths.Count && lengths[i] < length) 
                i++;
            if (i < lengths.Count)
                dtdl = res / (lengths[i] - lengths[i - 1]);
            t = res * (i - 1) + (length - lengths[i - 1]) * dtdl;
            return (t, dtdl);
        };

    public static Curve ByArclength(Curve curve)
    {
        if (curve.Surface is not GeodesicSurface surface)
        {
            Debug.LogError("The curve must be on a GeodesicSurface to calculate the arclength parameterization.");
            return curve;
        }

        var res = 0.1f;
        while (res > curve.Length / 4) res /= 2;
        var (lengths, lastLength, lastTimeInterval) = ArclengthFromTimeList(curve, res);
        var ArclengthFromTime = ListToFunction(lengths, lastLength, lastTimeInterval, res);
        // t -> l
        var TimeFromArclength = ListToInverseDerivative(lengths, lastLength, lastTimeInterval, res);

        return new ParametrizedCurve(curve.Name + " by arclength", lastLength + lengths[^1], surface, DerivativeAt, 
            from jumpTime in curve.VisualJumpTimes select ArclengthFromTime(jumpTime)) { 
            // todo: Feature / Bug. These jump times are not accurate enough for displaying the curve probably...
            // When displaying the curve, one could display the original curve?
            Color = curve.Color
        };

        
        TangentVector DerivativeAt(float length)
        {
            var (t, dtdl) = TimeFromArclength(length);
            return dtdl * curve.DerivativeAt(t);
        }
    }
}

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
                var thisVector = curve.DerivativeAt( Mathf.Max(t - res, (lastJump + 2f * t) / 3f )).vector;
                var angle = Vector3.Angle(crossedVector, thisVector) * Mathf.Deg2Rad;
                
                var guess = t + shift(t) / Mathf.Tan(angle); 
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

    private Curve LocalCurve(float t, float s)
    {
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

    public override Curve Restrict(float start, float? end = null) => new ShiftedCurve(curve.Restrict(start, end), t => shift(t + start), Name + $"[{start:g2}, {end:g2}]") {Color = Color};
}

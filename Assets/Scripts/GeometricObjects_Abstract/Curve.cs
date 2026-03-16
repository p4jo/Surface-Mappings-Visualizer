using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;


public abstract partial class Curve : ITransformable<Curve> // even IDrawnsformable in other part.
{
    public abstract string Name { get; set; }

    public abstract float Length { get; }

    public virtual Point StartPosition => ValueAt(0);
    public virtual Point EndPosition => ValueAt(Length);
    public virtual TangentVector StartVelocity => DerivativeAt(0);
    public virtual TangentVector EndVelocity => DerivativeAt(Length);

    public abstract Surface Surface { get; }
    public virtual IEnumerable<float> VisualJumpTimes => Enumerable.Empty<float>();

    public virtual IEnumerable<(float, ModelSurfaceBoundaryPoint)> VisualJumpPoints
    {
        get
        {
            if (Surface is not ModelSurface modelSurface)
                yield
                    break; // shouldn't happen unless VisualJumpTimes is already empty (atm this could happen in TransformedCurve, but there it doesn't matter)
            foreach (float t in VisualJumpTimes)
            {
                if (this[t] is ModelSurfaceBoundaryPoint boundaryPoint)
                    yield return (t, boundaryPoint);
                else
                {
                    const float jumpTimeTolerance = 1e-3f;
                    var pt1 = modelSurface.ClampPoint(this[t - jumpTimeTolerance], jumpTimeTolerance * 10);
                    var pt2 = modelSurface.ClampPoint(this[t + jumpTimeTolerance], jumpTimeTolerance * 10);
                    if (pt1 is not ModelSurfaceBoundaryPoint p1 || pt2 is not ModelSurfaceBoundaryPoint p2)
                    {
                        Debug.LogError($"In the curve {this} of type {GetType()}, the point at time {t}, {this[t]} is saved as a visual jump point, but the points just before and after are not clamped to model surface boundary points, but to {pt1} and {pt2} respectively!");
                        continue;
                    }
                    if (p1.side != p2.side.other)
                        Debug.LogWarning($"In the curve {this} of type {GetType()}, the point at time {t}, {this[t]} is saved as a visual jump point, but the points just before and after are not clamped to opposite side of the same model surface side, but {p1.side} and {p2.side}! This might be the case if the visual jump point is not calculated with enough precision.");
                    yield return (t, p1);
                }
            }
        }
    }

    public virtual IEnumerable<(string, bool)> SideCrossingWord =>
        from p in VisualJumpPoints
        let side = p.Item2.side
        select (((IDrawable) side).ColorfulName, side.Surface is ModelSurface surface && !surface.sides.Contains(side)); // this is a bit hacky. One should rather save this in form of the (inverse) marking into the strips, i.e. for each strip save the word in the "original" graph that its embedding isotopes to, which is equivalent to the Side crossing word, because the "original" graph is the dual of the model surface sides (not exactly -> todo) 

    public virtual Point this[float t] => ValueAt(t);

    public abstract Point ValueAt(float t);
    public abstract TangentVector DerivativeAt(float t);

    public virtual Curve Concatenate(Curve curve) => new ConcatenatedCurve(new Curve[] { this, curve });

    protected Curve reverseCurve;

    public virtual Curve Reversed() => reverseCurve ??= new ReverseCurve(this);

    /// <summary>
    /// Get the closest point in spline segment. Used for projection
    /// Copied and slightly modified from Dreamteck Splines
    /// </summary>
    private float GetClosestTimeInternal(int iterations, Point point, float start, float end, int slices)
    {
        // TODO: Performance. This is responsible for most of the time when the program freezes. DistanceSquared optimizes over all positions, this creates extra overhead
        if (start >= end)
            throw new ArgumentException("Start time must be before the end time");
        while (--iterations >= 0)
        {
            var closestTime = 0.0f;
            var closestDistance = Mathf.Infinity;
            var tick = (end - start) / slices;
            var t = start;
            while (true)
            {
                float dist = point.DistanceSquared(ValueAt(t));
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestTime = t;
                }

                if (t >= end) break;
                t = Mathf.Clamp(t + tick, start, end);
            }

            start = Mathf.Clamp(closestTime - tick, start, end);
            end = Mathf.Clamp(closestTime + tick, start, end);
        }

        float startDist = point.DistanceSquared(ValueAt(start));
        float endDist = point.DistanceSquared(ValueAt(end));
        if (startDist < endDist) return start;
        if (endDist < startDist) return end;
        return (start + end) / 2;
    }

    private const int GetClosestPointIterations = 4;

    public virtual float GetClosestPoint(Vector3 point) =>
        GetClosestTimeInternal(GetClosestPointIterations, point, 0, Length, 10);

    public virtual Curve ApplyHomeomorphism(Homeomorphism homeomorphism) =>
        homeomorphism.isIdentity ? this : new TransformedCurve(this, homeomorphism);

    /// <summary>
    /// A right-handed basis at the point on the curve at time t, with the tangent vector as the first vector and the normal vector of the surface as the third vector. Thus, the second vector points "to the right" of the curve.
    /// </summary>
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

    protected const float restrictTolerance = 0.0001f;

    /// <summary>
    /// Restrict the curve to a subinterval. This is computed with a tolerance: If start > -ε and end &lt; Length + ε and start &lt; end + ε, the restriction is valid. Else, an ArgumentOutOfRangeException is thrown.
    /// If start &lt; ε and end > Length - ε, the original curve is returned.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public virtual Curve Restrict(float start, float? end = null)
    {
        var stop = end ?? Length;
        if (start < -restrictTolerance || stop > Length + restrictTolerance || start > stop + restrictTolerance)
            throw new ArgumentOutOfRangeException(
                $"Invalid restriction in curve {this}: {start} to {end} for length {Length}");
        if (stop > Length)
            stop = Length;
        if (start < 0)
            start = 0;
        if (start < restrictTolerance && stop > Length - restrictTolerance)
            return this;
        return new RestrictedCurve(this, start, stop) { Color = Color };
    }

    public abstract Curve Copy();

    public Curve AdjustStartVector(TangentVector newStartVector, float cutoff)
    {
        var newCurve = Restrict(cutoff);
        var end = newCurve.StartVelocity;
        var interpolatingSegment = new SplineSegment(
            newStartVector,
            end,
            cutoff,
            Surface,
            Name + " adjusted start vector"
        );
        var result = interpolatingSegment.Concatenate(newCurve);
        result.Color = Color;
        result.Name = Name;
        return result;
    }
}
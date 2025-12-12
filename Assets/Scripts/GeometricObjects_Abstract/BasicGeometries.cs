using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public enum GeometryType
{
    /// <summary>
    /// This represents the manifolds as flat polygons with geodesics as straight lines
    /// - curvature is concentrated at the punctures.
    /// </summary>
    Flat,
    
    /// <summary>
    /// This represents the manifolds as polygons in the Poincaré disk model of the hyperbolic plane.
    /// All sides and geodesics are circular arcs (or straight lines). 
    /// </summary>
    HyperbolicDisk,
    
    /// <summary>
    /// This represents the manifolds as polygons in the Poincaré disk model of the hyperbolic plane.
    /// All sides and geodesics are circular arcs (or straight lines). 
    /// </summary>
    HyperbolicPlane,
    
    /// <summary>
    /// this is probably not necessary:
    /// punctured spheres can be displayed as the open disk (either flat or hyperbolic)
    /// and the non-punctured sphere has completely trivial mapping class group
    /// </summary>
    Spherical,
}

public abstract class Plane : GeodesicSurface
{

    public override Point ClampPoint(Vector3? point, float closenessThreshold) =>
        point.HasValue ? (Vector2)point : null;

    public override TangentSpace BasisAt(Point position) => new (position, Matrix3x3.InvertZ);

    public override Vector3 MinimalPosition { get; } = new(float.NegativeInfinity, float.NegativeInfinity);
    public override Vector3 MaximalPosition { get; } = new(float.PositiveInfinity, float.PositiveInfinity);

    protected Plane(string name, int genus, IEnumerable<Point> punctures = null,
        Vector2? minimalPosition = null, Vector2? maximalPosition = null) :
        base(name, genus, true, punctures)
    {
        if (minimalPosition.HasValue) 
            MinimalPosition = minimalPosition.Value;
        if (maximalPosition.HasValue)
            MaximalPosition = maximalPosition.Value;
    }

}

public class HyperbolicPlane : Plane
{
    public readonly bool diskModel;
    public HyperbolicPlane(bool diskModel, string name = "Hyperbolic Plane", IEnumerable<Point> punctures = null) :
        base(
            name, 0, punctures, 
            minimalPosition: diskModel ? new Vector2(-1, -1) : new Vector2(-100f,0f),
            maximalPosition: diskModel ? new Vector2(1, 1) : new Vector2(100f, 100f)
        )
    {
        this.diskModel = diskModel;
    }
    
    public override Curve GetGeodesic(Point start, Point end, string name, GeodesicSurface surface = null)
        => new HyperbolicGeodesicSegment(start, end, surface ?? this, name, diskModel);

    public override Curve GetGeodesic(TangentVector startVelocity, float length, string name, GeodesicSurface surface = null)
    {
        if (length < 0)
        {
            length = -length;
            startVelocity = -startVelocity;
        }
        return new HyperbolicGeodesicSegment(startVelocity, length, surface ?? this, name, diskModel);
    }

    public override float Distance(Point startPoint, Point endPoint)
    {
        return startPoint.Positions.CartesianProduct(endPoint.Positions).ArgMin(Distance).Item2;
        float Distance((Vector3, Vector3) vectors)
        {
            var (u, v) = vectors;
            if (diskModel)
                return (float) Math.Acosh(1 + 2 * (u - v).sqrMagnitude / (1 - u.sqrMagnitude) / (1 - v.sqrMagnitude));
            var vBar = new Vector3(v.x, -v.y);
            return 2 * (float) Math.Atanh((u - v).magnitude / (u - vBar).magnitude);
        }
    }

    public override float DistanceSquared(Point startPoint, Point endPoint)
    {
        var distance = Distance(startPoint, endPoint);
        return distance * distance;
    }


    public static Complex Möbius(Complex z, Complex a, Complex b, Complex c, Complex d) => (a * z + b) / (c * z + d);
    public static Complex MöbiusDerivative(Complex z, Complex a, Complex b, Complex c, Complex d) => (a * d - b * c) / ((c * z + d) * (c * z + d));
    
    public static Homeomorphism MöbiusTransformation(Complex a, Complex b, Complex c, Complex d, Surface source, Surface target) =>
        new(
            source,
            target, 
            z => Möbius(z.ToComplex(), a, b, c, d).ToVector3(),
            z => Möbius(z.ToComplex(), d, -b, -c, a).ToVector3(),
            z => MöbiusDerivative(z.ToComplex(), a, b, c, d).ToMatrix3x3(),
            z => MöbiusDerivative(z.ToComplex(), d, -b, -c, a).ToMatrix3x3(),
            "Möbius(" + a + ", " + b + ", " + c + ", " + d + ")"
        );
    
    /// <summary>
    /// The Cayley transform is a homeomorphism from the Poincaré half plane to the Poincaré disk.
    /// </summary>
    public static readonly Homeomorphism CayleyTransform = MöbiusTransformation(
        -Complex.One, Complex.ImaginaryOne, Complex.One, Complex.ImaginaryOne,
        new HyperbolicPlane(false, "Poincaré Half Plane"),
        new HyperbolicPlane(true, "Poincaré Disk")
    );

    public static readonly Homeomorphism ToKleinModel = new(
        new HyperbolicPlane(true, "Poincaré Disk"),
        new EuclideanPlane( "Klein Disk", minimalPosition: new Vector2(-1, -1), maximalPosition: new Vector2(1, 1)), // todo? add the possibility to the class
        z => 2f / (1 + z.sqrMagnitude) * z, // we assume that z.z == 0
        z => 1f / (1 + MathF.Sqrt(1 - z.sqrMagnitude)) * z, // we assume that z.z == 0
        z =>
        {
            float scale = 1f / (1 + z.sqrMagnitude);
            return 2 * scale * scale * new Matrix3x3(
                1 + z.y * z.y - z.x * z.x, -2 * z.x * z.y,
                -2 * z.x * z.y, 1 - z.y * z.y + z.x * z.x
            );
        },
        z =>
        {
            float sqrt = MathF.Sqrt(1 - z.sqrMagnitude);
            float scale = 1 + sqrt;
            return (1 / scale / scale / sqrt) * new Matrix3x3(
                scale - z.y * z.y, z.x * z.y,
                z.x * z.y, scale - z.x * z.x);
        },
         "Poincaré Disk to Klein Disk"
    );


    public override (float t1, float t2)? GetGeodesicIntersection(Curve geodesic1, Curve geodesic2)
    {
        if (geodesic1 is not HyperbolicGeodesicSegment line1 || geodesic2 is not HyperbolicGeodesicSegment line2)
            return null;
        var a = (line1.δ * line2.α - line1.β * line2.γ);
        var b = (line1.δ * line2.β - line1.β * line2.δ);
        var c = (-line1.γ * line2.α + line1.α * line2.γ);
        var d = (-line1.γ * line2.β + line1.α * line2.δ);
        
        // φ = (a b \\ c d) = A^-1 * B ∈ GL+(2,R) where line1 = A * γ_0 and line2 = B * γ_0 for the standard geodesic γ_0(t) = i e^t in the half plane model
        // Then the intersection is at t1, t2 with φ(i e^t2) = i e^t1
        
        var det = a * d - b * c;
        var expOfTwiceT2 = - b * d / (a * c);
        if (Math.Abs(expOfTwiceT2.Imaginary) > 1e-6 * expOfTwiceT2.Magnitude) throw new Exception($@"The Möbius transformation between the two geodesics {line1} and {line2} is not (a multiple of) a real matrix: ({a}, {b} \\ {c}, {d})");
        if (expOfTwiceT2.Real <= 0 || !double.IsFinite(expOfTwiceT2.Real)) return null; // no intersection
        var t2 = 0.5 * Math.Log(expOfTwiceT2.Real);
        var intersectionPointImaginaryPart = det * Math.Exp(t2) / (c * c * expOfTwiceT2.Real + d * d);  
        if (Math.Abs(intersectionPointImaginaryPart.Imaginary) > 1e-6 * intersectionPointImaginaryPart.Magnitude) throw new Exception($@"The Möbius transformation between the two geodesics {line1} and {line2} is not (a multiple of) a real matrix: ({a}, {b} \\ {c}, {d})");
        
        var t1 = (float) Math.Log(intersectionPointImaginaryPart.Real);
        if (t1 < -1e-6f || t1 > line1.Length + 1e-6f || t2 < -1e-6f || t2 > line2.Length + 1e-6f) return null;
        if (t1 < 0) t1 = 0;
        if (t2 < 0) t2 = 0;
        if (t1 > line1.Length) t1 = line1.Length;
        if (t2 > line2.Length) t2 = line2.Length;
        return (t1, (float) t2);
    }

}

public class EuclideanPlane : Plane
{
    public EuclideanPlane(string name = "Euclidean Plane", IEnumerable<Point> punctures = null, Vector2? minimalPosition = null, Vector2? maximalPosition = null) : base(name, 0, punctures, minimalPosition, maximalPosition){}

    public override Curve GetGeodesic(Point start, Point end, string name, GeodesicSurface surface = null)
        => new FlatGeodesicSegment(start, end, surface ?? this, name);

    public override Curve GetGeodesic(TangentVector tangentVector, float length, string name, GeodesicSurface surface = null)
        => new FlatGeodesicSegment(tangentVector, length, surface ?? this, name);

    public override float DistanceSquared(Point startPoint, Point endPoint) => startPoint.DistanceSquared(endPoint); // this minimizes over the positions

    const float intersectionTolerance = 1e-6f;
    public override (float t1, float t2)? GetGeodesicIntersection(Curve geodesic1, Curve geodesic2)
    {
        if (geodesic1 is not FlatGeodesicSegment line1 || geodesic2 is not FlatGeodesicSegment line2)
            return null;
        var (p,v) = line1.StartVelocity;
        var (q,w) = line2.StartVelocity;
        var P = p.Position;
        var Q = q.Position;
        var det = v.x * w.y - v.y * w.x;
        if (Mathf.Abs(det) == 0f) return null; // parallel lines
        // solve P + t1 v = Q + t2 w <=> (-v | w) (t1 t2)^T = Q - P <=> (t1 t2)^T = (-v | w)^-1 (Q - P)
        var t1 = (w.y * (Q.x - P.x) - w.x * (Q.y - P.y)) / det;
        var t2 = (v.y * (Q.x - P.x) - v.x * (Q.y - P.y)) / det;
        if (t1 < -intersectionTolerance || t1 > line1.Length + intersectionTolerance || t2 < intersectionTolerance || t2 > line2.Length + intersectionTolerance) return null;
        if (t1 < 0) t1 = 0;
        if (t2 < 0) t2 = 0;
        if (t1 > line1.Length) t1 = line1.Length;
        if (t2 > line2.Length) t2 = line2.Length;
        return (t1, t2);
    }

    public static Homeomorphism Isometry(TangentVector lineStartVelocity, TangentVector line2StartVelocity, ModelSurface source, ModelSurface target)
    {
        var a = lineStartVelocity.point.Position.ToComplex();
        var b = line2StartVelocity.point.Position.ToComplex();
        var c = line2StartVelocity.vector.ToComplex() / lineStartVelocity.vector.ToComplex();
        var cMatrix = c.ToMatrix3x3();
        var cInv = 1 / c;
        var cInvMatrix = cInv.ToMatrix3x3();
        // this maps lineStartVelocity to the line2StartVelocity
        return new(
            source,
            target,
            input => ((input.ToComplex() - a) * c + b).ToVector3(),
            input => ((input.ToComplex() - b) * cInv + a).ToVector3(),
            _ => cMatrix,
            _ => cInvMatrix,
            "Isometry from " + lineStartVelocity + " to " + line2StartVelocity
        );
    }
}

public class Rectangle : EuclideanPlane
{    
    public readonly float width, height;
    public Rectangle(float width, float height, string name = null, Vector2 minimalPosition = default): base(
        name ?? $"Rectangle at ({minimalPosition.x:g2}, {minimalPosition.y:g2}) with width {width:g2} and height {height:g2})",
        minimalPosition: minimalPosition,
        maximalPosition: minimalPosition + new Vector2(width, height)
    )
    {
        this.width = width;
        this.height = height;
    }

    public override Point ClampPoint(Vector3? point, float closenessThreshold) => 
        point?.Clamp(MinimalPosition, MaximalPosition);
}

public class Cylinder : Rectangle
{
    public static readonly Cylinder defaultCylinder = new Cylinder(1, 1, "Default Cylinder", Vector3.zero);

    /// <summary>
    /// A cylinder which is a rectangle and we interpret the y-direction as a circle.
    /// </summary>
    public Cylinder(float width, float height, string name = null, Vector3 minimalPosition = default) : base(width,
        height, name ?? $"Cylinder with width {width:g2} and height {height:g2}", minimalPosition)
    { }

    private Homeomorphism dehnTwist;
    /// <summary>
    /// This Dehn twist fixes the sides of the cylinder (x = x_min and x = x_max).
    /// </summary>
    public Homeomorphism DehnTwist
    {
        get
        {
            if (dehnTwist != null) return dehnTwist;
            dehnTwist = FromDefaultCylinder * DefaultCylinderDehnTwist * FromDefaultCylinder.Inverse;
            dehnTwist.name = "Dehn twist on " + Name;
            return dehnTwist;
        }
    }

    public override Point ClampPoint(Vector3? point, float closenessThreshold)
    {
        if (!point.HasValue) return null;
        var y = (point.Value.y - MinimalPosition.y) % height;
        if (y < 0) y += height;
        return new Vector2(
            Mathf.Clamp(point.Value.x, MinimalPosition.x, MaximalPosition.x), 
            y + MinimalPosition.y
        );
    }

    private Homeomorphism fromDefaultCylinder;

    public Homeomorphism FromDefaultCylinder
    {
        get
        {
             return fromDefaultCylinder ??= new Homeomorphism(defaultCylinder,
                 this,
                 Forward,
                 Backwards,
                 DForward,
                 DBackwards,
                "Default Cylinder to " + Name
            );

            Vector3 Backwards(Vector3 input) => new(
                (input.x - MinimalPosition.x) / width,
                (input.y - MinimalPosition.y) / height,
                input.z);

            Vector3 Forward(Vector3 input) => new(
                input.x * width + MinimalPosition.x,
                input.y * height + MinimalPosition.y,
                input.z);

            Matrix3x3 DBackwards(Vector3 input) => new(1 / width, 1 / height);

            Matrix3x3 DForward(Vector3 input) => new(width, height);
        }
    }

    private static Homeomorphism _defaultCylinderDehnTwist;
    public static Homeomorphism DefaultCylinderDehnTwist
    {
        get
        {
            return _defaultCylinderDehnTwist ??= new Homeomorphism(defaultCylinder,
                defaultCylinder,
                forward,
                backward,
                dForward,
                dBackward,
                "Dehn Twist"
            );
            Vector3 forward(Vector3 input) => new(input.x, (input.y + input.x) % 1f, input.z);
            Vector3 backward(Vector3 input) => new(input.x, (input.y + 1 - input.x) % 1f, input.z);
            Matrix3x3 dForward(Vector3 input) => new(new Vector2(1, 1), new Vector2(0, 1));
            Matrix3x3 dBackward(Vector3 input) => new(new Vector2(1, -1), new Vector2(0, 1));
        }
    }

    public override Curve GetGeodesic(Point start, Point end, string name, GeodesicSurface surface = null)
    {
        return base.GetGeodesic(start, end, name, surface ?? this);
        // TODO
    }

    public override Curve GetGeodesic(TangentVector tangentVector, float length, string name, GeodesicSurface surface = null)
    {
        return base.GetGeodesic(tangentVector, length, name, surface ?? this);
        // TODO
    }
}


public class CurveStrip : ParametricSurface
{
    protected readonly Curve curve;
    protected readonly Rectangle baseSurface;
    
    /// <summary>
    /// Give a regular neighborhood of the curve with a homeomorphism to the rectangle or cylinder.
    /// This can be interpreted as a strip in the sense of Bestvina-Handel and be used to perform automorphisms like Dehn twists and point pushes.
    /// </summary>
    public CurveStrip(Curve curve, float start = 0f, float? end = null, bool closed = false) :
        base(ConstructorArgs(curve, start, end, closed))
    {
        this.curve = curve;
        baseSurface = embedding.source as Rectangle;
    }

    public Homeomorphism DehnTwist => (embedding.source as Cylinder)!.DehnTwist;

    private static (string, Homeomorphism, IEnumerable<Rect>, Vector3, Vector3) ConstructorArgs(
        Curve curve, float start = 0f, float? end = null, bool closed = false
    ) {
        var stripEmbedding = StripEmbedding(curve, start, end, closed);
        var pos = stripEmbedding.source.MinimalPosition;
        var endPos = stripEmbedding.source.MaximalPosition;
        return new(
            curve.Name + " Strip",
            stripEmbedding,
            new[] { new Rect(pos, endPos - pos) },
            curve.Surface.MinimalPosition,
            curve.Surface.MaximalPosition
        );
    }

    private static Homeomorphism StripEmbedding(Curve curve, float start, float? end, bool closed)
    {
        float width = 1f;// 0.1f;
        // todo: if this intersects itself, punctures or other obstacles, we need to reduce the width

        float? end1 = end ?? curve.Length;
        var source = closed ? new Cylinder(width, end1.Value - start) : new Rectangle(width, end1.Value - start);

        return new Homeomorphism(source,
            curve.Surface,
            StripEmbedding,
            StripEmbeddingInverse,
            dStripEmbedding,
            null,
            curve.Name + " strip embedding"
        );

        Vector3 StripEmbedding(Vector3 pos)
        {
            var (s, t) = (pos.x, pos.y);
            var (pt, basis) = curve.BasisAt(t);
            return pt.Position + basis.b.normalized * (s * width);
            // todo: Feature: Reuse the code from ShiftedCurve!
        }

        Vector3 StripEmbeddingInverse(Vector3 pos)
        { // copilot!
            var t = curve.GetClosestPoint(pos);
            var (pt, basis) = curve.BasisAt(t);
            var s = Vector3.Dot(pos - pt.Position, basis.b.normalized) / width;
            if (s > width || s < 0)
                Debug.Log("tried to apply inverse strip embedding outside of the strip");
            return new Vector3(s, t);
        }

        Matrix3x3 dStripEmbedding(Vector3 pos)
        {
            var (s, t) = (pos.x, pos.y);
            var (pt, basis) = curve.BasisAt(t);
            return new Matrix3x3(
                basis.a,
                basis.b.normalized * width,
                basis.c
            );
        }
    }

    public override Point ClampPoint(Vector3? point, float closenessThreshold)
    {
        if (!point.HasValue) return null;
        float t = curve.GetClosestPoint(point.Value);
        var pt = curve.ValueAt(t);
        
        if ((point.Value - pt.Position).sqrMagnitude > MathF.Pow(baseSurface.width, 2))
            return null;
        var (_, basis) = curve.BasisAt(t);
        var b = basis.b.normalized;
        float sTimesWidth = Vector3.Dot(point.Value - pt.Position, b);
        if (sTimesWidth < 0 || sTimesWidth > baseSurface.width)
            return null;
        return pt.Position + b * sTimesWidth;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

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

    public override Point ClampPoint(Vector3? point) =>
        point.HasValue ? (Vector2)point : null;

    public override TangentSpace BasisAt(Point position) => new (position, Matrix3x3.InvertZ);

    public override Vector3 MinimalPosition { get; } = new(float.NegativeInfinity, float.NegativeInfinity);
    public override Vector3 MaximalPosition { get; } = new(float.PositiveInfinity, float.PositiveInfinity);

    protected Plane(string name, int genus, bool is2D) : base(name, genus, is2D) { }
    
}

public class HyperbolicPlane : Plane
{
    private readonly bool diskModel;
    public HyperbolicPlane(bool diskModel, string name = "Hyperbolic Plane") : base(name, 0, true)
    {
        this.diskModel = diskModel;
    }
    
    public override Curve GetGeodesic(Point start, Point end, string name)
        => new HyperbolicGeodesicSegment(start, end, this, name, diskModel);

    public override float DistanceSquared(Point startPoint, Point endPoint)
    {
        return startPoint.Positions.CartesianProduct(endPoint.Positions).ArgMin(DistanceSquared).Item2;
        float DistanceSquared((Vector3, Vector3) vectors)
        {
            var (u, v) = vectors;
            if (diskModel)
                return (float) Math.Acosh(1 + 2 * (u - v).sqrMagnitude / (1 - u.sqrMagnitude) / (1 - v.sqrMagnitude));
            var vBar = new Vector3(v.x, -v.y);
            return 2 * (float) Math.Atanh((u - v).sqrMagnitude / (u - vBar).sqrMagnitude);
        }
    }

}

public class EuclideanPlane : Plane
{
    public EuclideanPlane(string name = "Euclidean Plane") : base(name, 0, true){}

    public override Curve GetGeodesic(Point start, Point end, string name)
        => new FlatGeodesicSegment(start, end, this, name);

    public override float DistanceSquared(Point startPoint, Point endPoint) => startPoint.DistanceSquared(endPoint); // this minimizes over the positions
}

public class Rectangle : EuclideanPlane
{    
    public readonly float width, height;
    public Rectangle(float width, float height, string name = null, Vector3 minimalPosition = default): base(name ??
        $"Rectangle at ({minimalPosition.x:g2}, {minimalPosition.y:g2}) with width {width:g2} and height {height:g2})")
    {
        MinimalPosition = minimalPosition;
        MaximalPosition = minimalPosition + new Vector3(width, height);
        
        this.width = width;
        this.height = height;
    }

    public override Vector3 MinimalPosition { get; }
    public override Vector3 MaximalPosition { get; }

    public override Point ClampPoint(Vector3? point) => 
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

    public override Point ClampPoint(Vector3? point)
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
        }

        Vector3 StripEmbeddingInverse(Vector3 pos)
        { // copilot!
            (float t, Point pt)  = curve.GetClosestPoint(pos);
            var (_, basis) = curve.BasisAt(t);
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

    public override Point ClampPoint(Vector3? point)
    {
        if (!point.HasValue) return null;
        var (t, pt) = curve.GetClosestPoint(point.Value);
        
        if ((point.Value - pt.Position).sqrMagnitude > Mathf.Pow(baseSurface.width, 2))
            return null;
        var (_, basis) = curve.BasisAt(t);
        var b = basis.b.normalized;
        float sTimesWidth = Vector3.Dot(point.Value - pt.Position, b);
        if (sTimesWidth < 0 || sTimesWidth > baseSurface.width)
            return null;
        return pt.Position + b * sTimesWidth;
    }
}

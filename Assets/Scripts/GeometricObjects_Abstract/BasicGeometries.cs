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
    Hyperbolic,
    
    /// <summary>
    /// this is probably not necessary:
    /// punctured spheres will be displayed as the open disk (either flat or hyperbolic)
    /// and the non-punctured sphere has completely trivial mapping class group
    /// </summary>
    Spherical,
}

public abstract class Plane : GeodesicSurface
{

    public override Point ClampPoint(Vector3? point) =>
        point.HasValue ? (Vector2)point : null;

    public override TangentSpace BasisAt(Point position) => new (position, Matrix3x3.Identity);

    public override Vector3 MinimalPosition { get; } = new(float.NegativeInfinity, float.NegativeInfinity);
    public override Vector3 MaximalPosition { get; } = new(float.PositiveInfinity, float.PositiveInfinity);

    protected Plane(string name, int genus, bool is2D) : base(name, genus, is2D) { }
    
}

public class HyperbolicPlane : Plane
{
    public HyperbolicPlane(string name = "Hyperbolic Plane") : base(name, 0, true){}
    
    public override Curve GetGeodesic(Point start, Point end, string name)
        => new HyperbolicGeodesicSegment(start.Position, end.Position, this, name);
}

public class EuclideanPlane : Plane
{
    public EuclideanPlane(string name = "Euclidean Plane") : base(name, 0, true){}

    public override Curve GetGeodesic(Point start, Point end, string name)
        => new FlatGeodesicSegment(start.Position, end.Position, this, name);
}

public class Rectangle : EuclideanPlane
{    
    protected readonly float width, height;
    public Rectangle(float width, float height, string name = null, Vector3 minimalPosition = default): base(name ??
        $"Rectangle at ({minimalPosition.x}, {minimalPosition.y}) with width {width} and height {height})")
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
        height, name, minimalPosition)
    { }

    /// <summary>
    /// This Dehn twist fixes the left side of the cylinder (x=0).
    /// </summary>
    public Homeomorphism DehnTwist => FromDefaultCylinder * DefaultCylinderDehnTwist * FromDefaultCylinder.Inverse;

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
             return fromDefaultCylinder ??= new Homeomorphism(defaultCylinder, this, forward, backward, dForward, dBackward);

            Vector3 forward(Vector3 input) => new(
                (input.x - MinimalPosition.x) / width,
                (input.y - MinimalPosition.y) / height,
                input.z);

            Vector3 backward(Vector3 input) => new(
                input.x * width + MinimalPosition.x,
                input.y * height + MinimalPosition.y,
                input.z);

            Matrix3x3 dForward(Vector3 input) => new(1 / width, 1 / height);

            Matrix3x3 dBackward(Vector3 input) => new(width, height);
        }
    }

    private static Homeomorphism _defaultCylinderDehnTwist;
    public static Homeomorphism DefaultCylinderDehnTwist
    {
        get
        {
            return _defaultCylinderDehnTwist ??= new Homeomorphism(defaultCylinder, defaultCylinder, forward, backward, dForward, dBackward);
            Vector3 forward(Vector3 input) => new(input.x, (input.y + input.x) % 1f, input.z);
            Vector3 backward(Vector3 input) => new(input.x, (input.y + 1 - input.x) % 1f, input.z);
            Matrix3x3 dForward(Vector3 input) => new(new Vector2(1, 1), new Vector2(0, 1));
            Matrix3x3 dBackward(Vector3 input) => new(new Vector2(1, -1), new Vector2(0, 1));
        }
    }
}


public class Strip : ParametricSurface
{
    private readonly Curve curve;

    /// <summary>
    /// Give a regular neighborhood of the curve with a homeomorphism to the rectangle or cylinder.
    /// This can be interpreted as a strip in the sense of Bestvina-Handel and be used to perform automorphisms like Dehn twists and point pushes.
    /// </summary>
    public Strip(Curve curve, float start = 0f, float? end = null, bool closed = false) :
        base(ConstructorArgs(curve, start, end, closed))
    {
        this.curve = curve;
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
        float width = 0.1f;
        // todo: if this intersects itself, punctures or other obstacles, we need to reduce the width

        float? end1 = end ?? curve.Length;
        var source = closed ? new Cylinder(width, end1.Value - start) : new Rectangle(width, end1.Value - start);

        return new Homeomorphism(source,
            curve.Surface,
            StripEmbedding,
            StripEmbeddingInverse,
            dStripEmbedding,
            null
        );

        Vector3 StripEmbedding(Vector3 pos)
        {
            var (t, s) = (pos.x, pos.y);
            var (pt, basis) = curve.BasisAt(t);
            return pt.Position + basis.b.normalized * (s * width);
        }

        Vector3 StripEmbeddingInverse(Vector3 pos)
        { // copilot!
            float t = curve.GetClosestPoint(pos);
            var (pt, basis) = curve.BasisAt(t);
            var s = Vector3.Dot(pos - pt.Position, basis.b.normalized) / width;
            return new Vector3(t, s);
        }

        Matrix3x3 dStripEmbedding(Vector3 pos)
        {
            var (t, s) = (pos.x, pos.y);
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
        float t = curve.GetClosestPoint(point.Value);
        if ((point.Value - curve[t].Position).sqrMagnitude > Mathf.Pow(chartRects[0].width, 2))
            return null;
        var (pt, basis) = curve.BasisAt(t);
        var sTimesWidthTimesNormOfB = Vector3.Dot(point.Value - pt.Position, basis.b);
        return pt.Position + basis.b * sTimesWidthTimesNormOfB / basis.b.sqrMagnitude;
    }
}

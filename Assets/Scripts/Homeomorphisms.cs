using System;
using UnityEngine;

public readonly struct Homeomorphism
{
    public readonly DrawingSurface source, target; // relatively unnecessary
    private readonly Func<Vector3, Vector3> f;
    public Vector3? F(Vector3? pos) => pos == null ? null : f((Vector3)pos);
    

    public Homeomorphism(DrawingSurface source, DrawingSurface target, Func<Vector3, Vector3> f)
    {
        this.source = source;
        this.target = target;
        this.f = f;
    }

    public static Homeomorphism operator *(Homeomorphism f, Homeomorphism g)
    {
        return new Homeomorphism(g.source, f.target, x => f.f(g.f(x)));
    }

    
    public static Vector3 FlatTorusEmbedding(Vector2 angles, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (φ, θ) = (angles.x, angles.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        return new(r * Mathf.Cos(φ), r * Mathf.Sin(φ), smallRadius * Mathf.Cos(θ));
    }

    public Homeomorphism ConnectSumWithFlatTorus(
        Vector2 imageCenter,
        float imageHalfWidth,
        float imageHalfHeight,
        Vector2 cutoutCenter,
        float cutoutRadius,
        float pufferZone = 0.15f,
        float largeRadius = 1.5f,
        float smallRadius = 1f,
        Vector3 center = new())
    {
        if (source is not ModelSurface surface)
            throw new ArgumentException("source is not a ModelSurface");
        
        var original = this.f;
        Vector3 Torus(Vector2 p) => FlatTorusEmbedding(p, largeRadius, smallRadius) + center;

        Vector3 NewEmbedding(Vector2 p)
        {
            var centered = p - imageCenter;
            var normalized = new Vector2(centered.x / imageHalfWidth, centered.y / imageHalfHeight);
            var puffered = 1 + pufferZone;
            var (x, y) = (normalized.x * puffered, normalized.y * puffered);
            var r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            if (r > puffered)
                return original(p);
            if (r < 1)
            {
                var factor = cutoutRadius * Mathf.PI / r / r;
                var result = new Vector2(x * factor, y * factor) + cutoutCenter;
                return Torus(result);
            }
            // todo: interpolate (e.g. using a spline surface, e.g. the NURBS
            var t = (r - 1) / puffered;
            var dir = normalized / r;
            var closestPointOnOriginal = imageCenter + new Vector2(imageHalfWidth * dir.x, imageHalfHeight * dir.y);
            var start = original(closestPointOnOriginal);
            var closestPointOnNew = cutoutCenter + new Vector2( cutoutRadius * Mathf.PI  * dir.x,  cutoutRadius * Mathf.PI * dir.y);
            var end = Torus(closestPointOnNew);
            return Vector3.Lerp(start, end, t);
        }
        
        return new(new ModelSurface(surface));
    }
}
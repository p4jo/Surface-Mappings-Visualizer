
using System;
using System.Collections.Generic;
using UnityEngine;

public static class SurfaceGenerator
{
    public static AbstractSurface Torus(SurfaceMenu surfaceMenu, float smallRadius = 1f, float largeRadius = 1.5f, int punctures = 0)
    {
        var surface = new AbstractSurface(1, surfaceMenu);
        
        var parName = $"Torus with {punctures} punctures (embedded)";
        var parametrizedSurface = new ParametricSurface(parName, 1);
        surface.AddDrawingSurface(parametrizedSurface);

        var modelName = $"Torus with {punctures} punctures (identified flat square)";
        var sides = new List<ModelSurface.PolygonSide>
        {
            new("a", Vector2.zero, Vector2.right, false),
            new("a", Vector2.up, Vector2.up + Vector2.right, true),
            new("b", Vector2.zero, Vector2.up, true),
            new("b", Vector2.right, Vector2.right + Vector2.up, false),
        };
        var modelSurface = new ModelSurface(modelName, 1, punctures, GeometryType.Flat, sides);
        surface.AddDrawingSurface(modelSurface);

        var parametrization = new Homeomorphism(modelSurface, parametrizedSurface,
            v => FlatTorusEmbedding((Vector2)v, largeRadius, smallRadius));
        surface.AddHomeomorphism(parametrization);
        
        return surface;
    }
    
    
    public static Vector3 FlatTorusEmbedding(Vector2 angles, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (φ, θ) = (angles.x, angles.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        return new(r * Mathf.Cos(φ), r * Mathf.Sin(φ), smallRadius * Mathf.Cos(θ));
    }

    public static Homeomorphism ConnectSumWithFlatTorus(
        Homeomorphism homeomorphism,
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
        if (homeomorphism.source is not ModelSurface surface)
            throw new ArgumentException("source is not a ModelSurface");
        if (homeomorphism.target is not ParametricSurface targetSurface)
            throw new ArgumentException("target is not a ParametricSurface");
        
        var original = homeomorphism.f;
        Vector3 Torus(Vector2 p) => FlatTorusEmbedding(p, largeRadius, smallRadius) + center;

        Vector3 NewEmbedding(Vector3 pt)
        {
            Vector2 p = pt;
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
        
        var halfWidth = new Vector2(imageHalfWidth, 0);
        var halfHeight = new Vector2(0, imageHalfHeight);
        var boundaries = new ModelSurface.PolygonSide[]
        {
            new("a",  imageCenter + halfWidth - halfHeight, imageCenter + halfWidth + halfHeight, false),
            new("a", imageCenter - halfWidth - halfHeight, imageCenter - halfWidth + halfHeight, true),
            new("b", imageCenter - halfWidth + halfHeight, imageCenter + halfWidth + halfHeight, true),
            new("b", imageCenter - halfWidth - halfHeight, imageCenter + halfWidth - halfHeight, false) 
            // todo: give names that haven't been used yet
            // todo: calculate these correctly (the new boundary curves are inside). These describe the curve of the connect sum
        };
        var newBaseSurface = surface.WithAddedBoundaries(1, 0, boundaries);
        var newTargetSurface = targetSurface.WithAddedGenus(1, Array.Empty<IPoint>());
        var embedding = new Homeomorphism(newBaseSurface, newTargetSurface, NewEmbedding);
        newTargetSurface.parametrization = embedding;
        return embedding;
    }

    public static AbstractSurface CreateSurface(SurfaceMenu surfaceMenu, IEnumerable<SurfaceParameter> parameters)
    {
        return Torus(surfaceMenu);

        foreach (var parameter in parameters)
        {
            //todo
        }
    }
}

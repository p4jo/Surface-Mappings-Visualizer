
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


[Serializable]
public struct SurfaceParameter
{
    // todo: this should be split into two tpyes:
    // One being basically the input for the constructor of Modelsurface
    // One describing embedded surfaces, e.g. where the genera are. This affects only the embeddings (homeomorphisms)
    public int genus, punctures;
    public bool modelSurface;
    // todo

    public static SurfaceParameter FromString(string s)
    {
        if (s.StartsWith("g=") && int.TryParse(s[2..], out var g))
            return new() { genus = g, punctures = 0, modelSurface = false }; // todo
        
        if (s == "Torus2D")
            return new() { genus = 1, punctures = 0, modelSurface = true };
        if (s == "Torus3D")
            return new() { genus = 1, punctures = 0, modelSurface = false };
        throw new NotImplementedException();
    }
}


public static class SurfaceGenerator
{
    private static Homeomorphism Torus(float smallRadius = 1f, float largeRadius = 1.5f, int punctures = 0)
    {
        var parName = $"Torus with {punctures} punctures (embedded)";

        var modelName = $"Torus with {punctures} punctures (identified flat square)";
        var right = new Vector2(tau, 0);
        var up = new Vector2(0, tau);
        var sides = new List<ModelSurface.PolygonSide>
        {
            new("a", Vector2.zero, right, false),
            new("a", up, up + right, true),
            new("b", Vector2.zero, up, true),
            new("b", right, right + up, false),
        };
        var modelSurface = new ModelSurface(modelName, 1, punctures, GeometryType.Flat, sides);

        var parametrization = new Homeomorphism(modelSurface, null,
            v => FlatTorusEmbedding((Vector2)v, largeRadius, smallRadius), p => FlatTorusEmbeddingInverse(p, largeRadius, smallRadius));
        var chartRects = new List<Rect> { new(0, 0, tau, tau) };
        var parametrizedSurface = new ParametricSurface(parName, parametrization, chartRects); 
        // this assigns the target of the Homeomorphism

        return parametrization;
    }


    private static Vector3 FlatTorusEmbedding(Vector2 angles, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (φ, θ) = (angles.x, angles.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        return new(r * Mathf.Cos(φ), r * Mathf.Sin(φ), smallRadius * Mathf.Cos(θ));
    }

    private static Vector3 FlatTorusEmbeddingInverse(Vector3 point, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (x, y, z) = (point.x, point.y, point.z);
        var φ = Mathf.Atan2(y, x);
        if (Mathf.Abs(z) > smallRadius + 1e-3)
            throw new ArgumentException("point is not on the torus");
        if (Mathf.Abs(z) > smallRadius)
            z = smallRadius * Mathf.Sign(z);
        var θ = Mathf.Acos(z / smallRadius);
        var rSquared = x * x + y * y;
        if (rSquared < largeRadius * largeRadius)
            θ = -θ;
        return new(φ, θ);
    }
    
    private static float errorForBeingOnTorus(Vector3 point, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (x, y, z) = (point.x, point.y, point.z);
        var rSquared = x * x + y * y;
        var r = Mathf.Sqrt(rSquared);
        return new Vector2(r-largeRadius, z).sqrMagnitude - smallRadius * smallRadius;
    }

    private static Homeomorphism ConnectSumWithFlatTorus(Homeomorphism homeomorphism,
        Vector2 imageCenter,
        float imageHalfWidth,
        float imageHalfHeight,
        Vector2 cutoutCenter,
        float cutoutRadius,  
        float pufferZone = 0.2f,
        float largeRadius = 1.5f,
        float smallRadius = 1f,
        int addedPunctures = 0,
        Vector3 center = new())
    {
        if (homeomorphism.source is not ModelSurface surface)
            throw new ArgumentException("source is not a ModelSurface");
        if (homeomorphism.target is not ParametricSurface targetSurface)
            throw new ArgumentException("target is not a ParametricSurface");
        
        var original = homeomorphism.f;
        Vector3 NewTorus(Vector2 p) => FlatTorusEmbedding(p, largeRadius, smallRadius) + center;
        Vector3 NewTorusInverse(Vector3 pt) => FlatTorusEmbeddingInverse(pt - center, largeRadius, smallRadius);

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
                return NewTorus(result);
            }
            // todo: interpolate (e.g. using a spline surface, e.g. the NURBS
            var t = (r - 1) / puffered;
            var dir = normalized / r;
            var closestPointOnOriginal = imageCenter + new Vector2(imageHalfWidth * dir.x, imageHalfHeight * dir.y);
            var start = original(closestPointOnOriginal);
            var closestPointOnNew = cutoutCenter + new Vector2( cutoutRadius * Mathf.PI  * dir.x,  cutoutRadius * Mathf.PI * dir.y);
            var end = NewTorus(closestPointOnNew);
            
            return Vector3.Lerp(start, end, t);
            // todo: smoothen by interpolating between the two
        }
        
        var halfWidth = new Vector2(imageHalfWidth / (1 + pufferZone) * cutoutRadius, 0);
        var halfHeight = new Vector2(0, imageHalfHeight / (1 + pufferZone) * cutoutRadius);
        var labelA = "a" + Random.Range(0, 1000);
        var labelB = "b" + Random.Range(0, 1000); // todo: take next chars in alphabet 
        var cutoutInTarget = new Rect(imageCenter - halfWidth - halfHeight, 2 * (halfWidth + halfHeight));
        var boundaries = new ModelSurface.PolygonSide[]
        {
            new(labelA,  imageCenter + halfWidth - halfHeight, imageCenter + halfWidth + halfHeight, false),
            new(labelA, imageCenter - halfWidth - halfHeight, imageCenter - halfWidth + halfHeight, true),
            new(labelB, imageCenter - halfWidth + halfHeight, imageCenter + halfWidth + halfHeight, true),
            new(labelB, imageCenter - halfWidth - halfHeight, imageCenter + halfWidth - halfHeight, false) 
        };
        var newBaseSurface = surface.WithAddedBoundaries(1, addedPunctures, boundaries);
        
        var newChartRects = targetSurface.chartRects.SelectMany(rect => rect.Minus(cutoutInTarget, Helpers.RectCutMode.Horizontal));
        Debug.Log($"newChartRects: {string.Join(", ", (from rect in newChartRects select rect.ToString()).ToArray())}");

        Vector3 NewEmbeddingInverse(Vector3 pt)
        {
            if (errorForBeingOnTorus(pt - center, largeRadius, smallRadius) < 1e-3)
                return NewTorusInverse(pt - center);
            // todo: inverse of the interpolated sections
            return homeomorphism.fInv(pt);
        }
        
        
        var embedding = new Homeomorphism(newBaseSurface, null, NewEmbedding, NewEmbeddingInverse);
        // targetSurface doesn't exist yet, but the target of the Homeomorphism is assigned in the ParametricSurface constructor
        
        var newTargetSurface = new ParametricSurface($"{targetSurface.Name}#T", embedding, newChartRects);
        
        return embedding;
    }

    public static AbstractSurface CreateSurface(IEnumerable<SurfaceParameter> parameters)
    {
        return GenusGSurface(parameters.First().genus);

        foreach (var parameter in parameters)
        {
            //todo
        }
    }

    private static AbstractSurface GenusGSurface(int genus)
    {
        var torus = Torus();
        var res = torus;
        var cutoutDistance = tau / (genus - 1);
        var cutoutRadius = cutoutDistance / 6;
        for (var i = 0; i < genus - 1; i++)
        {
            var imageCenter = new Vector2(cutoutDistance * i + cutoutRadius, Mathf.PI / 2);
            var cutoutCenter = new Vector2((cutoutDistance * i + cutoutRadius + Mathf.PI) % tau, 0);
            var direction = torus.f(imageCenter);
            var center = direction * 2;
            res = ConnectSumWithFlatTorus(res, imageCenter, cutoutRadius, cutoutRadius, cutoutCenter, cutoutRadius, center: center);
        }
            

        return AbstractSurface.FromHomeomorphism(res);
    }

    private const float tau = Mathf.PI * 2;
}

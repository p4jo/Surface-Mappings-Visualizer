
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

public class ModelSurfaceParameter
{
    
}


public static class SurfaceGenerator
{
    private static Homeomorphism Torus(float smallRadius = 1f, float largeRadius = 1.5f, int punctures = 0)
    {
        var parName = $"Torus with {punctures} punctures (embedded)";

        var modelName = $"Torus with {punctures} punctures (identified flat square)";
        var right = new Vector2(τ, 0);
        var up = new Vector2(0, τ);
        var sides = new List<ModelSurface.PolygonSide>
        {
            new("a", Vector2.zero, right, false),
            new("a", up, up + right, true),
            new("b", Vector2.zero, up, true),
            new("b", right, right + up, false),
        };
        var modelSurface = new ModelSurface(modelName, 1, punctures, GeometryType.Flat, sides);

        var parametrization = new Homeomorphism(modelSurface, null,
            x => FlatTorusEmbedding((Vector2)x, largeRadius, smallRadius),
            y => FlatTorusEmbeddingInverse(y, largeRadius, smallRadius),
            x=> dFlatTorusEmbedding((Vector2)x,  largeRadius, smallRadius),
            null,
            "Torus embedding"
        );
        var chartRects = new List<Rect> { new(0, 0, τ, τ) };
        float size = largeRadius + smallRadius;
        var maximalPosition = new Vector3(size, size, smallRadius);
        var parametrizedSurface = new ParametricSurface(parName, parametrization, chartRects, 
            -maximalPosition, maximalPosition); 
        // this assigns the target of the Homeomorphism

        return parametrization;
    }

    private static Vector3 FlatTorusEmbedding(Vector2 angles, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (φ, θ) = (angles.x, angles.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        return new Vector3(r * Mathf.Cos(φ), r * Mathf.Sin(φ), smallRadius * Mathf.Cos(θ));
    }
    private static Matrix3x3 dFlatTorusEmbedding(Vector2 point, float largeRadius, float smallRadius)
    {
        // todo: make this more efficient
        var (φ, θ) = (point.x, point.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        var dfdφ = new Vector3(-r * Mathf.Sin(φ), r * Mathf.Cos(φ), 0);
        var drdθ = smallRadius * Mathf.Cos(θ);
        var dfdθ = new Vector3(drdθ * Mathf.Cos(φ), drdθ * Mathf.Sin(φ), -smallRadius * Mathf.Sin(θ));
        var normal = -Vector3.Cross(dfdφ, dfdθ);
        return new Matrix3x3(dfdφ, dfdθ, normal);
    }

    private static Vector3 FlatTorusEmbeddingInverse(Vector3 point, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (x, y, z) = (point.x, point.y, point.z);
        var φ = Mathf.Atan2(y, x);
        if (φ < 0)
            φ = τ + φ;
        if (Mathf.Abs(z) > smallRadius + 1e-3)
            throw new ArgumentException("point is not on the torus");
        if (Mathf.Abs(z) > smallRadius)
            z = smallRadius * Mathf.Sign(z);
        var θ = Mathf.Acos(z / smallRadius);
        var rSquared = x * x + y * y;
        if (rSquared < largeRadius * largeRadius)
            θ = τ-θ;
        return new Vector3(φ, θ);
    }
    
    private static float errorForBeingOnTorus(Vector3 point, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (x, y, z) = (point.x, point.y, point.z);
        var rSquared = x * x + y * y;
        var r = Mathf.Sqrt(rSquared);
        return new Vector2(r-largeRadius, z).sqrMagnitude - smallRadius * smallRadius;
    }

    private static Homeomorphism ConnectedSumWithFlatTorus(Homeomorphism homeomorphism,
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
        var Doriginal = homeomorphism.df;
        var originalInverse = homeomorphism.fInv;

        Matrix3x3 inputFlip = new Matrix3x3(-1, 1);

        var f = cutoutRadius / Mathf.PI;

        var halfWidth = new Vector2(imageHalfWidth / (1 + pufferZone) * f, 0);
        var halfHeight = new Vector2(0, imageHalfHeight / (1 + pufferZone) * f);
        var labelA = "a" + Random.Range(0, 1000);
        var labelB = "b" + Random.Range(0, 1000); // todo: take next chars in alphabet 
        var cutoutInTarget = new Rect(imageCenter - halfWidth - halfHeight, 2 * (halfWidth + halfHeight));
        var boundaries = new ModelSurface.PolygonSide[]
        {
            new(labelA,  imageCenter + halfWidth - halfHeight, imageCenter + halfWidth + halfHeight, true),
            new(labelA, imageCenter - halfWidth - halfHeight, imageCenter - halfWidth + halfHeight, false),
            new(labelB, imageCenter - halfWidth + halfHeight, imageCenter + halfWidth + halfHeight, false),
            new(labelB, imageCenter - halfWidth - halfHeight, imageCenter + halfWidth - halfHeight, true) 
        };
        var newBaseSurface = surface.WithAddedBoundaries(1, addedPunctures, boundaries);
        
        var newChartRects = targetSurface.chartRects.SelectMany(rect => rect.Minus(cutoutInTarget, Helpers.RectCutMode.Horizontal));
        Debug.Log($"newChartRects: {string.Join(", ", (from rect in newChartRects select rect.ToString()).ToArray())}");


        var embedding = new Homeomorphism(newBaseSurface,
            null,
            NewEmbedding,
            NewEmbeddingInverse,
            NewEmbeddingDerivative,
            null,
            name: homeomorphism.name + " # " + "Torus"
        ); 
        // targetSurface doesn't exist yet, but the target of the Homeomorphism is assigned in the ParametricSurface constructor
        
        
        float size = largeRadius + smallRadius;
        var torusSize = new Vector3(size, size, smallRadius);
        var newTargetSurface = new ParametricSurface($"{targetSurface.Name}#T",
            embedding,
            newChartRects,
            Helpers.Max(targetSurface.MinimalPosition, center - torusSize), 
            Helpers.Min(targetSurface.MaximalPosition, center + torusSize)
        );
        
        return embedding;

        Vector3 NewTorus(Vector2 p) => FlatTorusEmbedding(new Vector2(2 * cutoutCenter.x - p.x, p.y), largeRadius, smallRadius) + center;

        Matrix3x3 DNewTorus(Vector2 p) => dFlatTorusEmbedding(new Vector2(2 * cutoutCenter.x - p.x, p.y), largeRadius, smallRadius) * inputFlip;

        Vector3 NewTorusInverse(Vector3 pt)
        {
            var X = FlatTorusEmbeddingInverse(pt - center, largeRadius, smallRadius);
            return new Vector3((2 * cutoutCenter.x - X.x + 3 * τ) % τ, X.y);
        }

        void FixSmallX(ref float x, ref float y, ref float r)
        {
            if (r >= f) return;
            Debug.LogWarning("Evaluated point is inside the cutout");
            r = f;
            x = x * f / r;
            y = y * f / r;
        }
        
        Vector2 Inv(float x, float y, float r)
        {
            var factor = cutoutRadius / r / r;
            return new Vector2(x * factor, y * factor) + cutoutCenter;
        }

        (Vector2 Inv_X, Matrix3x3 dInv_X) TInv(float x, float y, float r)
        {
            var (drdx, drdy) = Dr(x, y);

            var factor = cutoutRadius / r / r;
            return (new Vector2(x * factor, y * factor) + cutoutCenter,
                factor * new Matrix3x3(1 - 2 * x / r * drdx,
                    -2 * y / r * drdy,
                    -2 * x / r * drdx,
                    1 - 2 * y / r * drdy)
            );
        }

        (float drdx, float drdy) Dr(float x, float y)
        {
            return (drdx: x < Mathf.Abs(y) ? -1 : x > Mathf.Abs(y) ? 1 : 0,
                drdy: y < Mathf.Abs(x) ? -1 : y > Mathf.Abs(x) ? 1 : 0);
        }

        Vector3 NewEmbedding(Vector3 pt)
        {
            Vector2 p = pt;
            var centered = p - imageCenter;
            var puffered = 1 + pufferZone;
            var totalFactorX = puffered / imageHalfWidth;
            var totalFactorY = puffered / imageHalfHeight;
            var (x, y) = (centered.x * totalFactorX, centered.y * totalFactorY);
            var r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            if (r > puffered)
                return original(pt);
            if (r < 1)
            {
                FixSmallX(ref x, ref y, ref r);
                return NewTorus(Inv(x, y, r));
            }
            // todo: interpolate (e.g. using a spline surface, e.g. the NURBS
            var t = (r - 1) / pufferZone;
            var dir = new Vector2(x, y) / r;
            
            var closestPointOnOriginal = imageCenter + new Vector2(imageHalfWidth * dir.x, imageHalfHeight * dir.y);
            var end = original(closestPointOnOriginal);
            var endVector = Doriginal(closestPointOnOriginal) * new Vector3(dir.x / totalFactorX, dir.y / totalFactorY);
            
            var (closestPointOnNew, dInvX) = TInv(dir.x, dir.y, 1);
            var start = NewTorus(closestPointOnNew);
            var startVector = DNewTorus(closestPointOnNew) * dInvX * dir;
            
            var A = endVector - startVector - 2 * (end - start);
            var B = 3 * (end - start) - endVector;
            var C = startVector;
            var D = start;
            return A * (t * t * t) + B * (t * t) + C * t + D;
        }
       

        Vector3 NewEmbeddingInverse(Vector3 pt)
        {
            if (errorForBeingOnTorus(pt - center, largeRadius, smallRadius) < 1e-3)
                return NewTorusInverse(pt - center);
            // todo: inverse of the interpolated sections
            return originalInverse(pt);
        }
        
        Matrix3x3 NewEmbeddingDerivative(Vector3 pt)
        {
            Vector2 p = pt;
            var centered = p - imageCenter;
            var puffered = 1 + pufferZone;
            var totalFactorX = puffered / imageHalfWidth;
            var totalFactorY = puffered / imageHalfHeight;
            var (x, y) = (centered.x * totalFactorX, centered.y * totalFactorY);
            var r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            if (r > puffered)
                return Doriginal(pt);
            if (r < 1)
            {
                FixSmallX(ref x, ref y, ref r);
                var (inv_x, dInv_x) = TInv(x, y, r);
                return DNewTorus(inv_x) * dInv_x * new Matrix3x3(totalFactorX, totalFactorY);
            }

            var dir = new Vector2(x, y) / r;
            var closestPointOnOriginal = imageCenter + new Vector2(imageHalfWidth * dir.x, imageHalfHeight * dir.y);
            var start = original(closestPointOnOriginal);
            var closestPointOnNew =
                cutoutCenter + new Vector2(cutoutRadius * Mathf.PI * dir.x, cutoutRadius * Mathf.PI * dir.y);
            var end = NewTorus(closestPointOnNew);
            var v = (end - start) / pufferZone; 
            // todo: derivative of closestPointOnNew, closestPointOnOriginal, start, end

            var (drdx, drdy) = Dr(x, y);
            return new Matrix3x3(v * drdx, v * drdy, Vector3.forward); // this is wrong
            // todo: do the interpolation
            // todo: make the derivative function entangled with the normal function for less redundancy
        }
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
        var cutoutDistance = τ / (genus - 1);
        var cutoutRadius = cutoutDistance / 6;
        for (var i = 0; i < genus - 1; i++)
        {
            var imageCenter = new Vector2(cutoutDistance * (i + 0.5f), Mathf.PI / 2);
            var cutoutCenter = imageCenter + new Vector2(Mathf.PI, 0);
            var direction = torus.f(imageCenter);
            var center = direction * 2;
            res = ConnectedSumWithFlatTorus(res, imageCenter, cutoutRadius, cutoutRadius, cutoutCenter, cutoutRadius, center: center, pufferZone: 0.5f);
        }
            

        return AbstractSurface.FromHomeomorphism(res);
    }

    private const float τ = Mathf.PI * 2;
}

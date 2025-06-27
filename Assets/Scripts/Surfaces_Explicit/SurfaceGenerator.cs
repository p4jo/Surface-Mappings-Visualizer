
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;


[Serializable]
public struct SurfaceParameter
{
    // todo: Feature. this should be split into two tpyes:
    // One being basically the input for the constructor of Modelsurface
    // One describing embedded surfaces, e.g. where the genera are. This affects only the embeddings (homeomorphisms)
    public int genus, punctures;
    public bool connectedSumEmbedding;

    public static SurfaceParameter FromString(string str)
    {
        int genus = 1, punctures = 0;
        bool connectedSumEmbedding = false;
        foreach (var s in str.Split(','))
        {
            if (s.StartsWith("g=") && int.TryParse(s[2..], out var g))
                genus = g;
            if (s.StartsWith("p=") && int.TryParse(s[2..], out var p))
                punctures = p;
            if (s.EndsWith("#"))
                connectedSumEmbedding = true;
        }
        return new() { genus = genus, punctures = punctures, connectedSumEmbedding = connectedSumEmbedding };
    }
}

public class ModelSurfaceParameter
{
    
}


public static class SurfaceGenerator
{
    
    public static ModelSurface FlatTorusModelSurface(int punctures, string name, string labelA = null, string labelB = null, Color? colorA = null, Color? colorB = null)
    {
        labelA ??= "a";
        labelB ??= "b";
        colorA ??= ModelSurface.PolygonSide.NextColor();
        colorB ??= ModelSurface.PolygonSide.NextColor();
        
        var right = new Vector2(τ, 0);
        var up = new Vector2(0, τ);
        var sides = new List<ModelSurface.PolygonSide>
        {
            new(labelA, Vector2.zero, right, false, colorA.Value),
            new(labelA, up, up + right, true, colorA.Value),
            new(labelB, Vector2.zero, up, true, colorB.Value),
            new(labelB, right, right + up, false, colorB.Value),
        };
        var modelSurface = new ModelSurface(name, 1, punctures, GeometryType.Flat, sides);
        return modelSurface;
    }
    public static ModelSurface ModelSurface4GGon(int genus, int punctures, string name, IEnumerable<string> labels = null, IEnumerable<Color> colors = null)
    {
        if (genus < 1)
            throw new ArgumentException("genus must be at least 1");
        labels ??= Enumerable.Range(0, 2 * genus).Select(i => "side " + (char)('a' + i));
        colors ??= Curve.colors.Loop(2 * genus);
        if (genus == 1)
        {
            var (labelA, labelB) = labels;
            return FlatTorusModelSurface(punctures, name, labelA, labelB); 
        }
        int n = 4 * genus;
        double radius = Math.Sqrt(Math.Cos(τ / n)); 
        // chosen s.t. all interior angles are τ / n (and thus the vertex is a regular point for the metric) 
        var vertices = (
            from k in Enumerable.Range(0, n)
            select Complex.FromPolarCoordinates(radius, τ * k / n).ToVector2()
        ).ToArray();
        List<ModelSurface.PolygonSide> sides = new();
        var labelArray = labels.ToArray();
        var colorArray = colors.ToArray();
        for (var l = 0; l < genus; l++)
        {
            int i = 4 * l;
            var a = vertices[i];
            var b = vertices[i + 1];
            var c = vertices[i + 2];
            var d = vertices[i + 3];
            var e = vertices[(i + 4) % n];
            sides.Add(new ModelSurface.PolygonSide(labelArray[2 * l], a, b, false, colorArray[2 * l]));
            sides.Add(new ModelSurface.PolygonSide(labelArray[2 * l + 1], b, c, false, colorArray[2 * l + 1]));
            sides.Add(new ModelSurface.PolygonSide(labelArray[2 * l], d, c, true, colorArray[2 * l]));
            sides.Add(new ModelSurface.PolygonSide(labelArray[2 * l + 1], e, d, true, colorArray[2 * l + 1]));
        }
        return new ModelSurface(name, genus, punctures, GeometryType.HyperbolicDisk, sides);
    }
    
    private static ParametricSurface Torus(float smallRadius = 1f, float largeRadius = 1.5f, int punctures = 0)
    {
        var parName = $"Torus with {punctures} punctures (embedded)";

        var modelName = $"Torus with {punctures} punctures (identified flat square)";
        var modelSurface = FlatTorusModelSurface(punctures, modelName);

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

        return parametrizedSurface;
    }


    private static Vector3 FlatTorusEmbedding(Vector2 angles, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (φ, θ) = (angles.x, angles.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        return new Vector3(r * Mathf.Cos(φ), r * Mathf.Sin(φ), smallRadius * Mathf.Cos(θ));
    }
    private static Matrix3x3 dFlatTorusEmbedding(Vector2 point, float largeRadius, float smallRadius)
    {
        // todo: Performance (non-critical)
        var (φ, θ) = (point.x, point.y);
        var r = largeRadius + smallRadius * Mathf.Sin(θ);
        var dfdφ = new Vector3(-r * Mathf.Sin(φ), r * Mathf.Cos(φ), 0);
        var drdθ = smallRadius * Mathf.Cos(θ);
        var dfdθ = new Vector3(drdθ * Mathf.Cos(φ), drdθ * Mathf.Sin(φ), -smallRadius * Mathf.Sin(θ));
        var normal = Vector3.Cross(dfdφ, dfdθ);
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

    private static ParametricSurface ConnectedSumWithFlatTorus(ParametricSurface targetSurface,
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
        var names = from i in Enumerable.Range(0, 25) select "side " + (char)('a' + i);
        
        var homeomorphism = targetSurface.embedding;

        if (homeomorphism.source is not ModelSurface surface)
            throw new ArgumentException("source is not a ModelSurface"); // can't happen
        
        var original = homeomorphism.f;
        var Doriginal = homeomorphism.df;
        var originalInverse = homeomorphism.fInv;

        Matrix3x3 inputFlip = new Matrix3x3(-1, 1);

        var f = cutoutRadius / Mathf.PI;

        var halfWidth = new Vector2(imageHalfWidth / (1 + pufferZone) * f, 0);
        var halfHeight = new Vector2(0, imageHalfHeight / (1 + pufferZone) * f);
        var usedLabels = new HashSet<string>(from side in surface.sides select side.Name);
        string labelA = null, labelB = null;
        foreach (var name in names)
        {
            if (usedLabels.Contains(name)) continue;
            if (labelA == null)
            {
                labelA = name;
                continue;
            }

            labelB = name;
            break;
        }
        if (labelA == null || labelB == null)
            throw new ArgumentException("Did you really want to have 25 sides on the surface?");
        
        var colorA = ModelSurface.PolygonSide.NextColor();
        var colorB = ModelSurface.PolygonSide.NextColor();
        
        var cutoutInTarget = new Rect(imageCenter - halfWidth - halfHeight, 2 * (halfWidth + halfHeight));
        var boundaries = new ModelSurface.PolygonSide[]
        {
            new(labelA,  imageCenter + halfWidth - halfHeight, imageCenter + halfWidth + halfHeight, true, colorA),
            new(labelA, imageCenter - halfWidth - halfHeight, imageCenter - halfWidth + halfHeight, false, colorA),
            new(labelB, imageCenter - halfWidth + halfHeight, imageCenter + halfWidth + halfHeight, false, colorB),
            new(labelB, imageCenter - halfWidth - halfHeight, imageCenter + halfWidth - halfHeight, true, colorB), 
        };
        var newBaseSurface = surface.WithAddedBoundaries(1, addedPunctures, boundaries);
        
        var newChartRects = targetSurface.chartRects.SelectMany(rect => rect.Minus(cutoutInTarget, Helpers.RectCutMode.Horizontal));
        Debug.Log($"newChartRects: {string.Join(", ", (from rect in newChartRects select rect.ToString()).ToArray())}");


        var puffered = 1 + pufferZone;
        var totalFactorX = puffered / imageHalfWidth;
        var totalFactorY = puffered / imageHalfHeight;
        
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

            
        return new ParametricSurface($"{targetSurface.Name}#T",
            embedding,
            newChartRects,
            Helpers.Max(targetSurface.MinimalPosition, center - torusSize), 
            Helpers.Min(targetSurface.MaximalPosition, center + torusSize)
        );

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
            var centered = (Vector2)pt - imageCenter;
            var (x, y) = (centered.x * totalFactorX, centered.y * totalFactorY);
            var r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            if (r > puffered)
                return original(pt);
            if (r < 1)
            {
                FixSmallX(ref x, ref y, ref r);
                return NewTorus(Inv(x, y, r));
            }
            // todo: Feature. Interpolate (e.g. using a spline surface, e.g. the NURBS
            var t = (r - 1) / pufferZone;
            var dir = new Vector2(x, y) / r;
            
            var (A, B, C, D) = InterpolationVariables(dir);
            return A * (t * t * t) + B * (t * t) + C * t + D;
        }
       

        Vector3 NewEmbeddingInverse(Vector3 pt)
        {
            if (errorForBeingOnTorus(pt - center, largeRadius, smallRadius) < 1e-3)
                return NewTorusInverse(pt - center);
            // todo: Feature. Inverse of the interpolated sections
            return originalInverse(pt);
        }
        
        Matrix3x3 NewEmbeddingDerivative(Vector3 pt)
        {
            var centered = (Vector2)pt - imageCenter;
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
            // todo: Feature. Derivative of closestPointOnNew, closestPointOnOriginal, start, end

            var (drdx, drdy) = Dr(x, y);
            return new Matrix3x3(v * drdx, v * drdy, Vector3.forward); // this is wrong
            // todo: Feature. Do the interpolation
            // todo: Clean Code. Make the derivative function entangled with the normal function for less redundancy
        }

        // todo: cache
        (Vector3 A, Vector3 B, Vector3 C, Vector3 D) InterpolationVariables(Vector2 dir)
        {
            var closestPointOnOriginal = imageCenter + new Vector2(imageHalfWidth * dir.x, imageHalfHeight * dir.y);
            var end = original(closestPointOnOriginal);
            var endVector = Doriginal(closestPointOnOriginal) * new Vector3(dir.x / totalFactorX, dir.y / totalFactorY);
            
            var (closestPointOnNew, dInvX) = TInv(dir.x, dir.y, 1);
            var start = NewTorus(closestPointOnNew);
            var startVector = DNewTorus(closestPointOnNew) * dInvX * dir;
            
            var A = endVector + startVector - 2 * (end - start);
            var B = 3 * (end - start) - endVector - 2 * startVector;
            var C = startVector;
            var D = start;
            return (A, B, C, D);
        }
    }

    public static AbstractSurface CreateSurface(IEnumerable<SurfaceParameter> parameters)
    {
        var p = parameters.First();
        if (p.connectedSumEmbedding)
            return new AbstractSurface(
                GenusGSurfaceConnectedSumFlat(p.genus, p.punctures).embedding
            );
        return new AbstractSurface(
            ModelSurface4GGon(p.genus, p.punctures,
                $"Model surface with genus {p.genus} and {p.punctures} punctures")
        );
        foreach (var parameter in parameters)
        {
            // todo: Feature.
        }
    }

    private static ParametricSurface GenusGSurfaceConnectedSumFlat(int genus, int punctures)
    {
        var torus = Torus(punctures: punctures);
        var res = torus;
        var cutoutDistance = τ / (genus - 1);
        var cutoutRadius = cutoutDistance / 6;
        for (var i = 0; i < genus - 1; i++)
        {
            var imageCenter = new Vector2(cutoutDistance * (i + 0.5f), Mathf.PI / 2);
            var cutoutCenter = imageCenter + new Vector2(Mathf.PI, 0);
            var direction = torus.embedding.f(imageCenter);
            var center = direction * 2;
            res = ConnectedSumWithFlatTorus(res, imageCenter, cutoutRadius, cutoutRadius, cutoutCenter, cutoutRadius, center: center, pufferZone: 0.5f);
        }


        return res;
    }

    private const float τ = Mathf.PI * 2;
    
}


using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;


[Serializable]
public struct SurfaceParameter
{
    // todo: Feature. this should be split into two tpyes:
    // One being basically the input for the constructor of ModelSurface
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


    /// <summary>
    /// Creates a spine for the surface by adding a vertex at (0, 0, 0) and connecting it to the midpoints of the sides of the surface. This currently doesn't work for surfaces with punctures, or for surfaces with non-convex polygons.
    /// </summary>
    /// <param name="names">The names for the strips. If not provided this will be the last character of the side name.</param>
    /// <param name="reverse">Whether to reverse the strip. If not provided, this will be false for all strips.
    /// This decides if the drawn spine curve goes towards the "primary" side of the surface (saved in surface.sides), or the "other" side (side.other).
    /// </param>
    /// <param name="peripheralPunctures">This is the number of punctures around which to add a peripheral loop. For example, if you want to study a point push, this should be all punctures. Selects the cusps with the least amount of sides touching it first.</param>
    public static FibredSurface SpineForSurface(int genus, int punctures, int peripheralPunctures, string name, IEnumerable<string> labels = null, IEnumerable<Color> colors = null, IDictionary<string, string> names = null,
        IDictionary<string, bool> reverse = null)
    {
        if (punctures < 0)
            throw new ArgumentException("Can't have a negative number of punctures.");
        if (genus < 0)
            throw new ArgumentException("Can't have a negative genus.");
        if (punctures == 0)
            throw new ArgumentException("A closed surface cannot carry a fibred surface (is not homotopy equivalent to any graph).");
        if (peripheralPunctures < 0)
            throw new ArgumentException("Can't have a negative number of peripheral punctures.");
        if (punctures < peripheralPunctures)
            throw new ArgumentException("Can't have more peripheral punctures than punctures in general.");
        
        string defaultName(string sideName) => sideName.Replace("side", "").Trim();
        string nameMap(string sideName)
        {
            if (names?.ContainsKey(sideName) ?? false)
                return names[sideName];
            return defaultName(sideName);
        }

        
        var surface = GenerateGeodesicSurface(genus, punctures, name, labels, colors);

        switch (genus, punctures)
        {
            case (0, 1):
            {
                // var surface = new EuclideanPlane();
                var graph = new UndirectedGraph<Junction, UnorientedStrip>();
                graph.AddVertex(new Junction(graph, new BasicPoint(Vector3.zero)));
                return new FibredSurface(graph, surface);
            }
            case (0, 2):
            {
                // var surface = new EuclideanPlane("Punctured Plane", new[] { new BasicPoint(Vector3.zero) });
                var graph = new UndirectedGraph<Junction, UnorientedStrip>();
                var junction = new Junction(graph, new BasicPoint(Vector3.right));
                var label = labels?.FirstOrDefault();
                Color color = default;
                bool assignNewColor = colors == null || (color = colors.FirstOrDefault()) == default;
                    
                var circle = new BasicParametrizedCurve(
                    label,
                    τ,
                    surface,
                    t => new Vector3(MathF.Cos(t), MathF.Sin(t), 0),
                    t => new Vector3(-MathF.Sin(t), MathF.Cos(t), 0)
                ) { Color = color };
                var edge = new UnorientedStrip(circle, junction, junction, EdgePath.Empty, graph, 0, 1, newColor: assignNewColor, newName: label is null, addToGraph: true);
                graph.AddVerticesAndEdge(edge);
                return new FibredSurface(graph, surface);
            }
            default:
            {
                if (surface is not ModelSurface modelSurface)
                    throw new ArgumentException("The hyperbolic model surface that was expected here wasn't one!?");
                var cusps = modelSurface.vertices;
                var peripheralCusps = cusps.OrderBy(v => v.boundaryCurves.Count).Take(peripheralPunctures).ToList();
                
                var graph = new UndirectedGraph<Junction, UnorientedStrip>();
                var peripheralGraph = new UndirectedGraph<Junction, UnorientedStrip>();

                var centerPoint = new BasicPoint(Vector3.zero); 
                var centerJunction = new Junction(graph, centerPoint);
                centerJunction.image = centerJunction; 
                graph.AddVertex(centerJunction);
                foreach (var cusp in peripheralCusps)
                {
                    var peripheralCurve = cusp.GeodesicCircleAround(0.33f * cusp.boundaryCurves.First().curve.Length, true);

                    var junction = new Junction(graph, peripheralCurve.StartPosition);
                    junction.image = junction;
                    
                    var curveToJunction = modelSurface.GetBasicGeodesic(centerPoint, peripheralCurve.StartPosition, $"{junction.Name}1");
                    
                    peripheralCurve.Name = $"{junction.Name}0";
                    // junction.Name = $"{junction.Name}.";
                    
                    var edgeToJunction = new UnorientedStrip(curveToJunction, centerJunction, junction, EdgePath.Empty, graph, curveToJunction.StartVelocity.vector.Angle(), 0, newColor: true, addToGraph: true);
                    edgeToJunction.EdgePath = new NormalEdgePath(edgeToJunction);
                    
                    var peripheralEdge = new UnorientedStrip(peripheralCurve, junction, junction, EdgePath.Empty, peripheralGraph, 1, 2, addToGraph: true, newColor: true);
                    peripheralEdge.EdgePath = new NormalEdgePath(peripheralEdge);
                    
                    junction.Color = peripheralEdge.Color;
                }

                foreach (var side in modelSurface.sides)
                {
                    if (char.IsDigit(side.Name[^1]))
                        continue; // todo? This means that it is one of the later sides in CuspsSideParameters that is added to the "normal" side to add cusps
                    var point = side[side.Length / 2];
                    var (point1, point2) = point.Positions;
                    var nameOfEdge = nameMap(side.Name);
                    if (reverse != null && reverse.ContainsKey(nameOfEdge) && reverse[nameOfEdge])
                        (point1, point2) = (point2, point1);
                    var firstPart = modelSurface.GetBasicGeodesic(centerPoint, point1, nameOfEdge); 
                    // saving the full point should mean that in ConcatenatedCurve, it will understand that this is not an actual jump point, just a visual one.
                    // nvm, it is Clamp()ed anyway
                    var secondPart = modelSurface.GetBasicGeodesic(point2, centerPoint, nameOfEdge);
                    var curve = firstPart.Concatenate(secondPart);
                    var edge = new UnorientedStrip(curve, centerJunction, centerJunction, EdgePath.Empty, graph, curve.StartVelocity.vector.Angle(), (- curve.EndVelocity.vector).Angle(), addToGraph: true);
                    edge.Name = nameOfEdge;
                    edge.Color = side.Color;
                    edge.EdgePath = new NormalEdgePath(edge);
                }
                
                return new FibredSurface(graph, modelSurface, peripheralGraph);
            }
        }
    }
    
    
    /// <summary>
    /// Creates a model surface with the fitting hyperbolic polygon and a fitting fibred surface.
    /// </summary>
    /// <param name="genus"></param>
    /// <param name="punctures"></param>
    /// <param name="name"></param>
    /// <param name="labels"></param>
    /// <param name="colors"></param>
    /// <returns></returns>
    public static GeodesicSurface GenerateGeodesicSurface(int genus, int punctures, string name = null, IEnumerable<string> labels = null, IEnumerable<Color> colors = null)
    {
        const float baseAngle = -MathF.PI / 2;
        
        if (genus < 0)
            throw new ArgumentException("Can't have a negative genus.");
        labels ??= Enumerable.Range(0, 2 * genus).Select(i => ("side " + (char)('a' + i)).ToString());
        colors ??= Curve.colors.Loop(2 * genus);
        
        using var labelEnumerator = labels.GetEnumerator();
        using var colorEnumerator = colors.GetEnumerator();

        (string, Color) NextLabelAndColor()
        {
            if (!labelEnumerator.MoveNext() || !colorEnumerator.MoveNext())
                throw new ArgumentException("Not enough labels or colors provided for the surface.");
            return (labelEnumerator.Current, colorEnumerator.Current);
        }
        
        switch (genus, punctures)
        {
            case (0, 0):
                // return new Sphere(name ?? "Sphere");
                throw new NotImplementedException();
            case (0, 1):
                return new EuclideanPlane(name ?? "Euclidean Plane");
            case (0, 2):
                return new EuclideanPlane(name ?? "Punctured Plane", new[] { new BasicPoint(Vector3.zero) });
            case (0, _):
            {
                var sideParameters = CuspsSideParameters(punctures - 1, MathF.PI, baseAngle, baseAngle, 1f);
                return new ModelSurface(name ?? "Torus", 0, punctures, GeometryType.HyperbolicDisk, sideParameters.ToList());
            }
            case (1, 0):
            {
                var (labelA, colorA) = NextLabelAndColor();
                var (labelB, colorB) = NextLabelAndColor();
                return FlatTorusModelSurface(0, name, labelA, labelB, colorA, colorB);
            }
            default:
            {
                int n = 4 * genus + 2 * (punctures - 1);
                float radius = punctures == 0 ? MathF.Sqrt(MathF.Cos(τ / n)) : 0.9f; 
                // chosen s.t. all interior angles are τ / n (and thus the vertex is a regular point for the metric) 
                var angleStep = τ / n;
                var sides = new List<ModelSurface.PolygonSide>();
                var extraPuncturesLeft = punctures - 1;
                int extraPuncturesPerNormalSide = Mathf.CeilToInt(extraPuncturesLeft / (2f * genus));
                int currentAngleStep = 0;
                for (var l = 0; l < genus; l++)
                {
                    var extraPuncturesSideA = extraPuncturesPerNormalSide > extraPuncturesLeft
                        ? extraPuncturesLeft
                        : extraPuncturesPerNormalSide;
                    extraPuncturesLeft -= extraPuncturesSideA;
                    var subSidesA = extraPuncturesSideA + 1;
                    
                    var extraPuncturesSideB = extraPuncturesPerNormalSide > extraPuncturesLeft
                        ? extraPuncturesLeft
                        : extraPuncturesPerNormalSide;
                    extraPuncturesLeft -= extraPuncturesSideB;
                    var subSidesB = extraPuncturesSideB + 1;

                    var startStepRightA = currentAngleStep;
                    var startStepLeftA = currentAngleStep + 2 * subSidesA + subSidesB;
                    var startStepRightB = currentAngleStep + subSidesA;
                    var startStepLeftB = currentAngleStep + 2 * subSidesA + 2 * subSidesB;
                    currentAngleStep = startStepLeftB;
                    
                    sides.AddRange(CuspsSideParameters(
                            subSidesA, 
                            angleStep * subSidesA, 
                            baseAngle + startStepLeftA * angleStep, 
                            baseAngle + startStepRightA * angleStep,
                            radius
                        )
                    );
                    sides.AddRange(CuspsSideParameters(
                            subSidesB, 
                            angleStep * subSidesB, 
                            baseAngle + startStepLeftB * angleStep, 
                            baseAngle + startStepRightB * angleStep,
                            radius
                        )
                    );
                }
                return new ModelSurface(name ?? $"Hyperbolic surface with genus {genus} and {punctures} cusps", genus, punctures, GeometryType.HyperbolicDisk, sides);
            }
        }

        // yields that many pairs of sides
        IEnumerable<ModelSurface.PolygonSide> CuspsSideParameters(int pairs, float totalAngleStep, float startAngleLeft,
            float startAngleRight, float radius, bool sameNameAndColor = true)
        {
            var angleStep = totalAngleStep / pairs;
            var rightAngle = startAngleRight;
            var leftAngle = startAngleLeft;
            Vector2 lastPointRight = radius * new Vector2(MathF.Cos(rightAngle), MathF.Sin(rightAngle));
            Vector2 lastPointLeft = radius * new Vector2(MathF.Cos(leftAngle), MathF.Sin(leftAngle));
            var (baseLabel, baseColor) = NextLabelAndColor();
            for (var i = 0; i < pairs; i++)
            {
                rightAngle += angleStep;
                leftAngle -= angleStep;
                var pointRight = radius * new Vector2(MathF.Cos(rightAngle), MathF.Sin(rightAngle));
                var pointLeft = radius * new Vector2(MathF.Cos(leftAngle), MathF.Sin(leftAngle));
                var label = baseLabel;
                var color = baseColor;
                if (i != 0)
                {
                    if (sameNameAndColor)
                        label = baseLabel + i;
                    else
                        (label, color) = NextLabelAndColor();
                }
                yield return new ModelSurface.PolygonSide(label, lastPointRight, pointRight, false, color);
                yield return new ModelSurface.PolygonSide(label, lastPointLeft, pointLeft, true, color);
                lastPointLeft = pointLeft;
                lastPointRight = pointRight;
            }
        }

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
        var r = largeRadius + smallRadius * MathF.Sin(θ);
        return new Vector3(r * MathF.Cos(φ), r * MathF.Sin(φ), smallRadius * MathF.Cos(θ));
    }
    private static Matrix3x3 dFlatTorusEmbedding(Vector2 point, float largeRadius, float smallRadius)
    {
        // todo: Performance (non-critical)
        var (φ, θ) = (point.x, point.y);
        var r = largeRadius + smallRadius * MathF.Sin(θ);
        var dfdφ = new Vector3(-r * MathF.Sin(φ), r * MathF.Cos(φ), 0);
        var drdθ = smallRadius * MathF.Cos(θ);
        var dfdθ = new Vector3(drdθ * MathF.Cos(φ), drdθ * MathF.Sin(φ), -smallRadius * MathF.Sin(θ));
        var normal = Vector3.Cross(dfdφ, dfdθ);
        return new Matrix3x3(dfdφ, dfdθ, normal);
    }

    private static Vector3 FlatTorusEmbeddingInverse(Vector3 point, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (x, y, z) = (point.x, point.y, point.z);
        var φ = MathF.Atan2(y, x);
        if (φ < 0)
            φ = τ + φ;
        if (MathF.Abs(z) > smallRadius + 1e-3)
            throw new ArgumentException("point is not on the torus");
        if (MathF.Abs(z) > smallRadius)
            z = smallRadius * MathF.Sign(z);
        var θ = MathF.Acos(z / smallRadius);
        var rSquared = x * x + y * y;
        if (rSquared < largeRadius * largeRadius)
            θ = τ-θ;
        return new Vector3(φ, θ);
    }
    
    private static float errorForBeingOnTorus(Vector3 point, float largeRadius = 1.5f, float smallRadius = 1f)
    {
        var (x, y, z) = (point.x, point.y, point.z);
        var rSquared = x * x + y * y;
        var r = MathF.Sqrt(rSquared);
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

        var f = cutoutRadius / MathF.PI;

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
            return (drdx: x < MathF.Abs(y) ? -1 : x > MathF.Abs(y) ? 1 : 0,
                drdy: y < MathF.Abs(x) ? -1 : y > MathF.Abs(x) ? 1 : 0);
        }

        Vector3 NewEmbedding(Vector3 pt)
        {
            var centered = (Vector2)pt - imageCenter;
            var (x, y) = (centered.x * totalFactorX, centered.y * totalFactorY);
            var r = MathF.Max(MathF.Abs(x), MathF.Abs(y));
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
            var r = MathF.Max(MathF.Abs(x), MathF.Abs(y));
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
                cutoutCenter + new Vector2(cutoutRadius * MathF.PI * dir.x, cutoutRadius * MathF.PI * dir.y);
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

    public static (AbstractSurface, FibredSurface) CreateSurface(IEnumerable<SurfaceParameter> parameters)
    {
        var p = parameters.First();
        if (p.connectedSumEmbedding)
            return (new AbstractSurface(
                GenusGSurfaceConnectedSumFlat(p.genus, p.punctures).embedding
            ), null);
        var fibredSurface = SpineForSurface(p.genus, p.punctures, p.punctures, "Spine for " + p.genus + "g" + p.punctures + "p");
        return (new AbstractSurface(fibredSurface.surface), fibredSurface);
        // todo: Feature: Make the parameters useful (or delete them)
        
    }

    private static ParametricSurface GenusGSurfaceConnectedSumFlat(int genus, int punctures)
    {
        var torus = Torus(punctures: punctures);
        var res = torus;
        var cutoutDistance = τ / (genus - 1);
        var cutoutRadius = cutoutDistance / 6;
        for (var i = 0; i < genus - 1; i++)
        {
            var imageCenter = new Vector2(cutoutDistance * (i + 0.5f), MathF.PI / 2);
            var cutoutCenter = imageCenter + new Vector2(MathF.PI, 0);
            var direction = torus.embedding.f(imageCenter);
            var center = direction * 2;
            res = ConnectedSumWithFlatTorus(res, imageCenter, cutoutRadius, cutoutRadius, cutoutCenter, cutoutRadius, center: center, pufferZone: 0.5f);
        }


        return res;
    }

    private const float τ = MathF.PI * 2;
    
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public static class FibredSurfaceFactory
{
    /// <summary>
    /// Creates a spine for the surface by adding a vertex at (0, 0, 0) and connecting it to the midpoints of the sides of the surface. This currently doesn't work for surfaces with punctures, or for surfaces with non-convex polygons.
    /// </summary>
    /// <param name="surface"></param>
    /// <param name="map">This is the map describing the homeomorphism. Use spaces for separation and uppercase for inverse. Example: map["a"] = "a B A b D C A"</param>
    /// <param name="names">The names for the strips. If not provided this will be the last character of the side name.</param>
    /// <param name="reverse">Whether to reverse the strip. If not provided, this will be false for all strips.
    /// This decides if the drawn spine curve goes towards the "primary" side of the surface (saved in surface.sides), or the "other" side (side.other).
    /// </param>
    public static FibredSurface RoseSpine(
        ModelSurface surface,
        IDictionary<string, string[]> map,
        IDictionary<string, string> names = null,
        IDictionary<string, bool> reverse = null
    ) {
        string defaultName(string sideName) => sideName[^1..];
        string nameMap(string sideName)
        {
            if (names?.ContainsKey(sideName) ?? false)
                return names[sideName];
            return defaultName(sideName);
        }

        // todo: map on the punctures and adding peripheral strips around them. 
        var junction = new BasicPoint(Vector3.zero);
        var edges = new List<(Curve, string, string, string[])>();
        if (!map.Keys.ToHashSet().IsSupersetOf(surface.sides.Select(s => nameMap(s.Name))))
            throw new("Provide g(e) for all of the edges of the rose spine. Their names come from the sides of the surface (and are constructed to pass through this side). These names are " + string.Join(", ", surface.sides.Select(s => nameMap(s.Name))));
        
        foreach (var side in surface.sides)
        {
            var point = side[side.Length / 2];
            var (point1, point2) = point.Positions;
            var name = nameMap(side.Name);
            if (reverse != null && reverse.ContainsKey(name) && reverse[name])
                (point1, point2) = (point2, point1);
            var firstPart = surface.GetBasicGeodesic(junction, point1, name); 
            // saving the full point should mean that in ConcatenatedCurve, it will understand that this is not an actual jump point, just a visual one.
            // nvm, it is Clamp()ed anyway
            var secondPart = surface.GetBasicGeodesic(point2, junction, name);
            var curve = firstPart.Concatenate(secondPart);
            curve.Name = name;
            var stripData = (curve, "v", "v", map[curve.Name]);
            edges.Add(stripData);
        }

        return new FibredSurface(edges, surface);
    }
    
    /// <summary>
    /// The rose spine of the surface with the given map. Use spaces for separation and uppercase for inverse
    /// Example: map["a"] = "a B A b D C A" 
    /// </summary>
    public static FibredSurface RoseSpine(
        ModelSurface surface, 
        IDictionary<string, string> map,
        IDictionary<string, string> names = null,
        IDictionary<string, bool> reverse = null
    ) =>
        RoseSpine(surface,
            map.ToDictionary(kv => kv.Key, 
                kv => kv.Value.Split(' ')
            ),
            names,
            reverse);

    /// <summary>
    /// The rose spine of the surface with the identity map. 
    /// </summary>
    public static FibredSurface RoseSpine(ModelSurface surface) => 
        RoseSpine(surface, (surface.sides.Select(s => s.Name).ToDictionary(s => s, s => s)));

    public static void Test()
    {
        ModelSurface surface = SurfaceGenerator.ModelSurface4GGon(2, 0, "Genus-2 surface", new string[]{"d", "c", "a", "b"});
        // if (surface.Name)
        var map = new Dictionary<string, string>
        {
            ["a"] = "a B A b D C A",
            ["b"] = "a c d B a b c d B",
            ["c"] = "c c d B",
            ["d"] = "b c d B"
        };
        FibredSurface s = RoseSpine(surface, map, reverse: new Dictionary<string, bool>
        {
            ["a"] = false,
            ["b"] = false,
            ["c"] = true,
            ["d"] = true
        });
        s.BestvinaHandelAlgorithm();
    }   
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public static class FibredSurfaceFactory
{
    public static FibredSurface RoseSpine(ModelSurface surface, IDictionary<string, string[]> map, IDictionary<string, string> namesd = null)
    {
        string defaultName(string sideName) => sideName[^1..];
        string names(string sideName)
        {
            if (namesd?.ContainsKey(sideName) ?? false)
                return namesd[sideName];
            return defaultName(sideName);
        }

        // todo: map on the punctures and adding peripheral strips around them. 
        var junction = new BasicPoint(Vector3.zero);
        var edges = new List<(Curve, string, string, string[])>();
        if (!map.Keys.ToHashSet().IsSupersetOf(surface.sides.Select(s => names(s.Name))))
            throw new("Provide g(e) for all of the edges of the rose spine. Their names come from the sides of the surface (and are constructed to pass through this side). These names are " + string.Join(", ", surface.sides.Select(s => names(s.Name))));
        
        foreach (var side in surface.sides)
        {
            var point = side[side.Length / 2];
            var name = names(side.Name);
            var firstPart = surface.GetBasicGeodesic(junction, point, name); // saving the full point should mean that in ConcatenatedCurve, it will understand that this is not an actual jump point, just a visual one. // nvm, it is Clamp()ed anyway
            var secondPart = surface.GetBasicGeodesic(point.Positions.ElementAt(1), junction, name);
            var curve = firstPart.Concatenate(secondPart);
            curve.Name = name;
            var stripData = (curve, "v", "v", map[curve.Name]);
            edges.Add(stripData);
        }

        return new FibredSurface(edges);
    }
    
    /// <summary>
    /// The rose spine of the surface with the given map. Use spaces for separation and uppercase for inverse
    /// Example: map["a"] = "a B A b D C A" 
    /// </summary>
    public static FibredSurface RoseSpine(ModelSurface surface, IDictionary<string, string> map) => 
        RoseSpine(surface, map.ToDictionary(kv => kv.Key, kv => kv.Value.Split(' ')));

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
        FibredSurface s = RoseSpine(surface, map);
        s.BestvinaHandelAlgorithm();
    }   
}
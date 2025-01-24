using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public static class BHTest
{
    public static FibredSurface RoseSpine(ModelSurface surface, IDictionary<string, string[]> map)
    { 
        // todo: map on the punctures and adding peripheral strips around them. 
        var junction = new BasicPoint(Vector3.zero);
        var edges = new List<(Curve, string, string, string[])>();
        if (!map.Keys.ToHashSet().IsSupersetOf(surface.sides.Select(s => s.Name)))
            throw new("Provide g(e) for all of the edges of the rose spine, which will have the same names as the sides of the surface (and are constructed to pass through this side). These names are " + string.Join(", ", surface.sides.Select(s => s.Name)));
        
        foreach (var side in surface.sides)
        {
            var point = side[side.Length / 2];
            var firstPart = surface.GetGeodesic(junction, point, side.Name); // saving the full point should mean that in ConcatenatedCurve, it will understand that this is not an actual jump point, just a visual one.
            var secondPart = surface.GetGeodesic(point.Positions.ElementAt(1), junction, side.Name);
            var curve = firstPart.Concatenate(secondPart);
            curve.Name = side.Name;
            var strip = (curve, "v", "v", map[curve.Name]);
            edges.Add(strip);
        }

        return new FibredSurface(edges);
    }
    
    public static FibredSurface RoseSpine(ModelSurface surface, IDictionary<string, string> map)
    {
        return RoseSpine(surface, map.ToDictionary(kv => kv.Key, kv => kv.Value.Split(' ')));
    }
    
    public static void Test()
    {
        ModelSurface surface = SurfaceGenerator.ModelSurface4GGon(2, 0, "Genus-2 surface", new string[]{"d", "c", "a", "b"});
        // if (surface.Name)
        var map = new Dictionary<string, string>();
        map["a"] = "a B A b D C A";
        map["b"] = "a c d B a b c d B";
        map["c"] = "c c d B";
        map["d"] = "b c d B";
        FibredSurface s = RoseSpine(surface, map);
        s.BestvinaHandelAlgorithm();
    }   
}
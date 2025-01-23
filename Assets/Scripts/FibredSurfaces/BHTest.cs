using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public class BHTest
{
    public FibredSurface RoseSpine(ModelSurface surface, IDictionary<string, string[]> map)
    { 
        // todo: map on the punctures and adding peripheral strips around them. 
        var junction = new BasicPoint(Vector3.zero);
        var edges = new List<(Curve, string, string, string[])>();
        if (!map.Keys.SequenceEqual(surface.sides.Select(s => s.Name)))
            throw new("Provide g(e) for all of the edges of the rose spine, which will have the same names as the sides of the surface (and are constructed to pass through this side). These names are " + string.Join(", ", surface.sides.Select(s => s.Name)));
        
        foreach (var side in surface.sides)
        {
            var point = side[side.Length / 2];
            var firstPart = surface.GetGeodesic(junction, point, side.Name); // saving the full point should mean that in ConcatenatedCurve, it will understand that this is not an actual jump point, just a visual one.
            var secondPart = surface.GetGeodesic(point.Positions.ElementAt(1), junction, side.Name);
            var curve = firstPart.Concatenate(secondPart);
            curve.Name = side.Name;
            var strip = (curve, "v", "v", map[curve.Name]);
        }

        return new FibredSurface(edges);
    }
    
    public void Test(ModelSurface surface)
    {
        // if (surface.Name)
        var map = new Dictionary<string, string[]>();
        
        FibredSurface s = RoseSpine(surface, map);
    }   
}
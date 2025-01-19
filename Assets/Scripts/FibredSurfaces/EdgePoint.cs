using System.Collections.Generic;
using System.Linq;


public class EdgePoint
{
    public EdgePoint(Strip edge, int i)
    {
        this.edge = edge;
        this.i = i;
    }

    public readonly Strip edge;
    public readonly int i;

    public Junction Image => edge.EdgePath[i].Source;

    public override bool Equals(object obj) => obj switch
    {
        EdgePoint other => edge.Equals(other.edge) && i == other.i ||
                           edge.Equals(other.edge.Reversed()) && i == other.edge.EdgePath.Count - other.i,
        _ => false
    };

    public float GetCurveTimeInJunction()
    {
        // todo: place at f.Inverse(where f * strip.curve is inside the junction strip.EdgePath[i].Target)
        // I.e. a) we have to know the homeo f.
        // b) we have to follow the curve until it enters a junction; i times.
        return i * edge.Curve.Length / edge.EdgePath.Count;
    }
    
    public int IndexDirectionFixed(Strip other)
    {
        if (edge.Equals(other))
            return i;
        if (edge.Equals(other.Reversed()))
            return edge.EdgePath.Count - i;
        return -1;
    }
    
    public override string ToString()
    {
        var names = from e in edge.EdgePath select e.Name;
        var textSegments = names.Take(i).Append("·").Concat(names.Skip(i));
        return $"Point in {edge} at g({edge.Name}) = {string.Join("", textSegments)}";
    }
}
public class Inefficiency: EdgePoint
{
     public readonly List<Strip> edgesToFold;
     public int initialSegmentToFold;
     public readonly int order;
     public Inefficiency(Strip edge, int i, int order, List<Strip> edgesToFold, int initialSegmentToFold) : base(edge, i)
     {
         this.edgesToFold = edgesToFold;
         this.initialSegmentToFold = initialSegmentToFold;
         this.order = order;
     }
 
}
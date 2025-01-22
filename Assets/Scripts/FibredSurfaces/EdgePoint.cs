using System;
using System.Collections.Generic;
using System.Linq;



public class EdgePoint
{
    /// <summary>
    /// Represents the (equivalence class of) point in the edge that gets mapped into a vertex, in between the edges in the edgePath with index i-1 and i.
    /// (i.e. the i-th vertex after leaving the original vertex). If i=0, it is the source vertex, if i=edge.EdgePath.Count, it is the target vertex.
    /// </summary>
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
        Junction other => i == 0 && Equals(other, edge.Source) || i == edge.EdgePath.Count && Equals(other, edge.Target),
        EdgePoint other => i == 0 && Equals(other, edge.Source) || i == edge.EdgePath.Count && Equals(other, edge.Target) || 
                           other.i == IndexDirectionFixed(other.edge),
        _ => false
    };

    public float GetCurveTimeInJunction()
    {
        // todo: place at f.Inverse(where f * strip.curve is inside the junction strip.EdgePath[i].Target)
        // Better have the entrance and exit times?
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

    public Strip DgBefore()
    {
        if (i != 0) return edge.EdgePath[i - 1].Reversed();

        var star = edge.Source.Star().ToArray();
        // we assume that in this case there is only one other edge in the star of the source.
        if (star.Length != 2) return null;
        
        var e = star.First(e => !Equals(e, edge));
        return e.Dg;
    }
    
    public Strip DgAfter()
    {
        if (i != edge.EdgePath.Count) return edge.EdgePath[i];
        var edgeReversed = edge.Reversed();
        var star = edge.Target.Star().ToArray();
        // we assume that in this case there is only one other edge in the star of the target.
        if (star.Length != 2) return null;
        
        var e = star.First(e => !Equals(e, edgeReversed));
        return e.Dg;
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

     public Inefficiency(EdgePoint edgePoint) : base(edgePoint.edge, edgePoint.i)
     {
         Strip a = edgePoint.DgBefore();
         Strip b = edgePoint.DgAfter();
         Strip aOld = a;
         for (int j = 0; j <= 2 * 1000; j++) // should be graph.Edges.Count but we don't have access to that here
         {
             if (!Equals(a, b))
             {
                 aOld = a;
                 a = a!.Dg;
                 b = b!.Dg;
                 continue;
             }

             edgesToFold = (from e in aOld!.Source.Star() where Equals(e.Dg, a) select e).ToList<Strip>();
             initialSegmentToFold = Strip.SharedInitialSegment(edgesToFold);
             order = j;
             return;
         }
         throw new Exception("Bug: Two edges in the same gate didn't eventually get mapped to the same edge under Dg.");

     }

}
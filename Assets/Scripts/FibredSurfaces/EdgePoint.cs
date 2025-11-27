using System;
using System.Collections.Generic;
using System.Linq;



public class EdgePoint
{
    /// <summary>
    /// Represents the (equivalence class of) point in the edge that gets mapped into a vertex, in between the edges in the edgePath with index i-1 and i.
    /// (i.e. the i-th vertex after leaving the original vertex). If i=0, it is the source vertex, if i=edge.EdgePath.Count, it is the target vertex.
    /// </summary>
    public EdgePoint(Strip edge, int index)
    {
        this.edge = edge;
        this.index = index;
    }

    public readonly Strip edge;
    public readonly int index;

    public Junction Image => edge.EdgePath[index].Source;

    public override bool Equals(object obj) => obj switch
    {
        Junction other => index == 0 && Equals(other, edge.Source) || index == edge.EdgePath.Count && Equals(other, edge.Target),
        EdgePoint other => index == 0 && Equals(other, edge.Source) || index == edge.EdgePath.Count && Equals(other, edge.Target) || 
                           other.index == AlignedIndex(other.edge),
        _ => false
    };

    public float GetCurveTimeInJunction()
    {
        // todo: place at f.Inverse(where f * strip.curve is inside the junction strip.EdgePath[i].Target)
        // Better have the entrance and exit times?
        // I.e. a) we have to know the homeo f.
        // b) we have to follow the curve until it enters a junction; i times.
        return index * edge.Curve.Length / edge.EdgePath.Count;
    }
    
    public int AlignedIndex(Strip other)
    {
        if (IsInEdge(other, out var reverse))
            return reverse ? edge.EdgePath.Count - index : index;
        return -1;
    }
    public int AlignedIndex(Strip other, out bool reverse)
    {
        if (IsInEdge(other, out reverse))
            return reverse ? edge.EdgePath.Count - index : index;
        return -1;
    }

    public bool IsInEdge(Strip other, out bool reverse)
    {
        reverse = edge.Equals(other.Reversed());
        return edge.Equals(other) || reverse;
    }
    
    /// <summary>
    /// The same point but with the edge reversed.
    /// </summary>
    /// <returns></returns>
    public EdgePoint Reversed() => new(edge.Reversed(), edge.EdgePath.Count - index);
    
    public EdgePoint AlignedWith(Strip other)
    {
        if (IsInEdge(other, out var reverse))
            return reverse ? Reversed() : this;
        return null;
    }
    
    public EdgePoint AlignedWith(Strip other, out bool reverse)
    {
        if (IsInEdge(other, out reverse))
            return reverse ? Reversed() : this;
        return null;
    }


    public override string ToString() => ToString(false);
    public string ToColorfulString() => ToString(true);
    protected virtual string ToString(bool colorful) => $"Point in Strip {edge.ColorfulName} at {ToShortString(colorful: colorful)}";
    
    public string ToShortString(int innerLength = 5, int outerLength = 4, bool colorful = false)
    {
        var initialSegmentLength = index;
        var firstMiddleSegmentLength = 0;
        var secondMiddleSegmentLength = 0;
        var lastSegmentStart = index;
        var firstEllipse = "";
        var secondEllipse = "";
        if (index > innerLength + outerLength + 1)
        {
            initialSegmentLength = outerLength;
            firstMiddleSegmentLength = innerLength;
            firstEllipse = "...";
        }
        if (edge.EdgePath.Count - index > innerLength + outerLength + 1)
        {
            secondMiddleSegmentLength = innerLength;
            lastSegmentStart = edge.EdgePath.Count - outerLength;
            secondEllipse = "...";
        }
        var names = (from e in edge.EdgePath select colorful ? e.ColorfulName : e.Name).ToArray();
        var initialSegment = names[..initialSegmentLength];
        var firstMiddleSegment = names[(index-firstMiddleSegmentLength)..index];
        var lastMiddleSegment = names[index..(index+secondMiddleSegmentLength)];
        var lastSegment = names[lastSegmentStart..];
            
        return $"g({(colorful ? edge.ColorfulName : edge.Name)}) = {string.Join(" ", initialSegment)}{firstEllipse}{string.Join(" ", firstMiddleSegment)}|{string.Join(" ", lastMiddleSegment)}{secondEllipse}{string.Join(" ", lastSegment)}";
    }
    
    public string ToSerializationString()
    {
        return $"{edge.Name}@{index}";
    }
    
    public static EdgePoint Deserialize(string inefficiencySerializationString, IEnumerable<Strip> orientedEdges)
    {
        var a = inefficiencySerializationString.Split('@');
        var edge = orientedEdges.First(e => e.Name == a[0]);
        var index = int.Parse(a[1]);
        return new EdgePoint(edge, index);
    }

    public Strip DgBefore()
    {
        if (index != 0) return edge.EdgePath[index - 1].Reversed();

        var star = FibredSurface.Star(edge.Source).ToArray();
        // we assume that in this case there is only one other edge in the star of the source.
        if (star.Length != 2) return null;
        
        var e = star.First(e => !Equals(e, edge));
        return e.Dg;
    }
    
    public Strip DgAfter()
    {
        if (index != edge.EdgePath.Count) return edge.EdgePath[index];
        var edgeReversed = edge.Reversed();
        var star = FibredSurface.Star(edge.Target).ToArray();
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
     public Inefficiency(Strip edge, int index, int order, List<Strip> edgesToFold, int initialSegmentToFold) : base(edge, index)
     {
         this.edgesToFold = edgesToFold;
         this.initialSegmentToFold = initialSegmentToFold;
         this.order = order;
     }

     public Inefficiency(EdgePoint edgePoint) : base(edgePoint.edge, edgePoint.index)
     {
         Strip a = edgePoint.DgBefore();
         Strip b = edgePoint.DgAfter();
         Strip aOld = a, bOld = b;
         for (int j = 0; j <= 2 * 1000; j++) // should be graph.Edges.Count but we don't have access to that here
         {
             if (a.Source != b.Source) throw new Exception("Bug: Two edges stopped starting from the same vertex after mapping with Dg.");

             if (!Equals(a, b))
             {
                 aOld = a;
                 bOld = b;
                 a = a!.Dg;
                 b = b!.Dg;
                 continue;
             }

             order = j;
             if (j == 0) { // the case where the two edges are already the same.
                 edgesToFold = new List<Strip> ();
                 initialSegmentToFold = 0;
                 return;
             }

             if (AlwaysFoldAllEdgesWithShortSharedInitialSegment) // this is the way the algorithm is described in [BH]
             {
                 edgesToFold = (from e in FibredSurface.Star(aOld!.Source) where Equals(e.Dg, a) select e).OrderBy(s => s.Name).ToList<Strip>();
                 initialSegmentToFold = Strip.SharedInitialSegment(edgesToFold);
             }
             else // this is how it is handled in the first example in [BH]. This is "cooler" since the initial segment is longer.
             {
                 initialSegmentToFold = Strip.SharedInitialSegment(new List<Strip> {aOld, bOld});
                 var initialSegment = aOld.EdgePath.Take(initialSegmentToFold).ToList();
                 edgesToFold = (from e in FibredSurface.Star(aOld.Source) where e.EdgePath.Take(initialSegmentToFold).SequenceEqual(initialSegment) select e).OrderBy(s => s.Name).ToList<Strip>();
             }
             return;
         }
         throw new Exception("Bug: Two edges in the same gate didn't eventually get mapped to the same edge under Dg.");

     }

     public bool FullFold => edgesToFold.Any(e => e.EdgePath.Count == initialSegmentToFold);

     public bool SameEdgesToFold(Inefficiency other) => edgesToFold.SequenceEqual(other.edgesToFold);

     protected override string ToString(bool colorful)
     {
         var fullyFoldedEdgesText = (from e in edgesToFold where e.EdgePath.Count == initialSegmentToFold select colorful ? e.ColorfulName : e.Name).ToCommaSeparatedString();
         var partiallyFoldedEdgesText = (from e in edgesToFold where e.EdgePath.Count > initialSegmentToFold select colorful ? e.ColorfulName : e.Name).ToCommaSeparatedString();

         var text = new System.Text.StringBuilder($"Inefficiency of order {order}: {ToShortString(colorful: colorful)}");
         var before = DgBefore();
         var after = DgAfter();
         for (int i = 1; i <= order; i++)
         {
             text.Append(" -> ...");
             text.AppendJoin(" ", before.EdgePath.TakeLast(3).Select(e => colorful ? e.ColorfulName : e.Name));
             text.Append("|");
             text.AppendJoin(" ", after.EdgePath.Take(3).Select(e => colorful ? e.ColorfulName : e.Name));
             text.Append("...");
             before = before.Dg; // cannot be null, can it?
             after = after.Dg;
         }
         text.Append("\nFold ").Append(fullyFoldedEdgesText);
         if (fullyFoldedEdgesText != "" && partiallyFoldedEdgesText != "")
             text.Append($" and initial segments of {partiallyFoldedEdgesText}");
         else if (partiallyFoldedEdgesText != "")
             text.Append($"initial segments of {partiallyFoldedEdgesText}");
         return $"{text} (based at {edgesToFold.First().Source.ColorfulName}).";
     }

     private const bool AlwaysFoldAllEdgesWithShortSharedInitialSegment = false;
}
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.ConnectedComponents;
using UnityEngine;
using TransformedGraph = QuikGraph.UndirectedGraph<Junction, UnorderedStrip>;

public class EmbeddedGraph
{
    public TransformedGraph graph;

    public List<OrderedStrip> Star(Junction junction)
    {
        return graph.AdjacentEdges(junction).Select(
            strip => new OrderedStrip(strip, strip.Source != junction)
        ).Concat(
            from strip in graph.AdjacentEdges(junction)
            where strip.IsSelfEdge()
            select new OrderedStrip(strip, true)
        ).ToList();
    }
    

    public void CollapseInvariantSubforest(List<UnorderedStrip> subforest)
    {
        TransformedGraph subgraph = new TransformedGraph();
        foreach (UnorderedStrip strip in subforest)
        {
            subgraph.AddVertex(strip.Source);
            subgraph.AddVertex(strip.Target);
            subgraph.AddEdge(strip);
        }

        Dictionary<Junction, int> components = new();
        int numberOfComponents = subgraph.ConnectedComponents(components);
        var newVertices = (
            from i in Enumerable.Range(0, numberOfComponents)
            select subgraph.Vertices.First(
                vertex => components[vertex] == i)
        ).ToList();
        // todo: save the collapsed subgraph into junction - display all but as one junction.
            
        TransformedGraph newGraph = new TransformedGraph(true);
        newGraph.AddVertexRange(newVertices);
        foreach (var strip in graph.Edges)
        {
            if (subforest.Contains(strip)) continue;
            strip.Source = newVertices[components[strip.Source]];
            strip.Target = newVertices[components[strip.Target]];
            strip.EdgePath = strip.EdgePath.Where(edge => !subforest.Contains(edge.UnderlyingEdge)).ToList();
            newGraph.AddEdge(strip);
        }
        
    }

    public void RemoveValenceOneJunction(Junction junction)
    {
        var star = Star(junction);
        if (star.Count() != 1) Debug.LogError($"Supposed valence-one junction has valence {star.Count()}");
        var removedStrip = star.First();
        Junction otherJunction = removedStrip.Target;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removedStrip.strip);
        foreach (var strip in graph.Edges) 
            strip.EdgePath = strip.EdgePath.Where(edge => edge.UnderlyingEdge != strip).ToList();
        // todo: isotopy or make the junction bigger to include the removed strip
    }

    public void RemoveValenceTwoJunction(Junction junction, UnorderedStrip removeStrip)
    {
        var star = Star(junction);
        if (star.Count() != 2) Debug.LogError($"Supposed valence-two junction has valence {star.Count()}");
        OrderedStrip keptStrip = star[0].strip == removeStrip ? star[1] : star[0];
        Junction enlargedJunction = removeStrip.Source == junction ? removeStrip.Target : removeStrip.Source;
        // todo: include removedStrip and junction into the enlargedJunction
        keptStrip.Source = enlargedJunction;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removeStrip);
        foreach (var strip in graph.Edges) 
            strip.EdgePath = strip.EdgePath.Where(edge => edge.UnderlyingEdge != removeStrip).ToList();
    }

    public int PullTight() // still has to be repeated until nothing changes. This only pulls back along one strip each.
    {
        int extremalVertices = 0, backTracks = 0;
        foreach (var vertex in graph.Vertices)
        {
            var star = Star(vertex);
            var firstOutgoingEdge = star.FirstOrDefault()?.EdgePath.FirstOrDefault();
            if (star.Any(strip => !Equals(strip.EdgePath.FirstOrDefault(), firstOutgoingEdge)))
                continue;
            PullTightExtremalVertex(vertex, firstOutgoingEdge);
            extremalVertices++;
        }

        foreach (var strip in graph.Edges)
        {
            for (int i = 0; i < strip.EdgePath.Count - 1; i++)
            {
                if (!Equals(strip.EdgePath[i], strip.EdgePath[i+1].Reversed())) continue;
                PullTightBackTrack(strip, i);
                backTracks++;
            }
        }
        Debug.Log($"Pulled tight {extremalVertices} extremal vertices and {backTracks} backtracks.");
        return extremalVertices + backTracks;
    }
    
    public void PullTightExtremalVertex(Junction vertex, Strip firstOutgoingEdge)
    {
        vertex.image = firstOutgoingEdge.Target;
        foreach (var strip in Star(vertex))
        {
            strip.EdgePath = strip.EdgePath.Skip(1).ToList();
            // for self-loops, this takes one from both ends.
        }
        // todo: isotopy: Move vertex and shorten the strips
    }
    
    public void PullTightBackTrack(Strip strip, int i)
    {
        strip.EdgePath = strip.EdgePath.Take(i).Concat(strip.EdgePath.Skip(i + 2)).ToList();
        // todo: isotopy to make the strip shorter
    }

    public void FoldEdges(IList<Strip> edges)
    {
        var edgePath = edges[0].EdgePath;
        if (edges.Any(edge => !edge.EdgePath.SequenceEqual(edgePath)))
            Debug.LogError("Edges to fold do not have the same edge path.");
        var targetVerticesToFold = from edge in edges select edge.Target;
        var newVertex = new Junction(); // todo: a Junction that contains the vertices, but avoids the rest of the fibred surface
        graph.RemoveEdges(from edge in edges.Skip(1) select edge.UnderlyingEdge);
        foreach (var vertex in targetVerticesToFold)
        {
            foreach (var edge in Star(vertex)) 
                edge.Source = newVertex;
            graph.RemoveVertex(vertex);
        }
    }
    
    public (UnorderedStrip, UnorderedStrip) SplitEdge(OrderedStrip splitEdge, int i)
    {
        var edgePath = splitEdge.EdgePath;
        var newVertex = new Junction(); // todo: place at f.Inverse(where f * strip.curve is inside the junction strip.EdgePath[i].Target)
        var firstSegment = splitEdge.strip.Copy(splitEdge.reverse);
        firstSegment.EdgePath = edgePath.Take(i).ToList();
        firstSegment.Target = newVertex;
        
        var secondSegment = splitEdge.strip.Copy(splitEdge.reverse);
        secondSegment.EdgePath = edgePath.Skip(i).ToList();
        secondSegment.Source = newVertex;
        
        graph.AddVertex(newVertex);
        graph.AddEdge(firstSegment);
        graph.AddEdge(secondSegment);
        graph.RemoveEdge(splitEdge.strip);

        foreach (var strip in graph.Edges)
        {
            strip.EdgePath = strip.EdgePath.SelectMany(
                edge => edge.Equals(splitEdge)
                    ? new() {firstSegment, secondSegment}
                    : edge.Equals(splitEdge.Reversed()) ?
                        new() {secondSegment.Reversed(), firstSegment.Reversed()} 
                        : new List<Strip> {edge}
            ).ToList();
        }
        
        return (firstSegment, secondSegment);
    }

    public void FoldInitialSegment(params OrderedStrip[] strips)
    {
        IEnumerable<Strip> initialSegment = strips[0].EdgePath;
        foreach (var strip in strips.Skip(1)) 
            initialSegment = initialSegment.Zip(strip.EdgePath, (a, b) => Equals(a, b) ? a : null);

        int i = initialSegment.TakeWhile(strip => strip != null).Count();
        if (i == 0) Debug.LogError("The edges do not have a common initial segment.");

        var initialStripSegments = new List<Strip>(strips.Length);
        // var terminalStripSegments = new List<Strip>(strips.Length);
        foreach (var edge in strips)
        {
            if (edge.EdgePath.Count == i)
            {
                initialStripSegments.Add(edge);
                // terminalStripSegments.Add(null);
                continue;
            }

            var (firstSegment, secondSegment) = SplitEdge(edge, i);
            initialStripSegments.Add(firstSegment);
            // terminalStripSegments.Add(secondSegment);
        }
        FoldEdges(initialStripSegments);
    }
}

public class Junction
{
    public List<ITransformable> drawables;
    // todo: save and display embedded disk and actually a collection of disks and strips.

    /// <summary>
    /// f maps this junction into the image junction, i.e. g(this) = image;
    /// </summary>
    public Junction image; 
}

public abstract class Strip: IEdge<Junction>
{
    public virtual Curve Curve { get; set; }

    /// <summary>
    /// f maps this strip into the fibred Surface, and it traces out the edgePath g(this) = edgePath;
    /// </summary>
    public virtual List<Strip> EdgePath { get; set; }
    
    public virtual Junction Source { get; set; }
    public virtual Junction Target { get; set; }
    public abstract UnorderedStrip UnderlyingEdge { get; }

    public virtual Junction OtherVertex(Junction vertex) => vertex == Source ? Target : Source;
    
    public abstract OrderedStrip Reversed();

    public override bool Equals(object other)
    {
        if (other is OrderedStrip orderedStrip) return OrderedStrip.Equals(orderedStrip, this);
        if (other is not Strip otherStrip) return false; 
        return Source == otherStrip.Source && Target == otherStrip.Target && Curve.Equals(otherStrip.Curve) && EdgePath.SequenceEqual(otherStrip.EdgePath);
    }
}

public class UnorderedStrip : Strip
{
    public UnorderedStrip Copy(bool reversed = false)
    {
        return new UnorderedStrip
        {
            Curve = reversed ? Curve.Reverse() : Curve,
            EdgePath = EdgePath,
            Source = Source,
            Target = Target
        };
    }

    public override UnorderedStrip UnderlyingEdge => this;
    public override OrderedStrip Reversed() => new(this, true);

}

public class OrderedStrip: Strip
{
    public override Curve Curve => strip.Curve.Reverse();
    
    public readonly UnorderedStrip strip;
    public readonly bool reverse;

    public OrderedStrip(UnorderedStrip strip, bool reverse)
    {
        this.strip = strip;
        this.reverse = reverse;
    }

    public override Junction Source
    {
        get => reverse ? strip.Target : strip.Source;
        set
        {
            if (reverse) strip.Target = value;
            else strip.Source = value;
        }
    }

    public override Junction Target
    {
        get => reverse ? strip.Source : strip.Target;
        set {
            if (reverse) strip.Source = value;
            else strip.Target = value;
        }
    }

    public override List<Strip> EdgePath => reverse ? ReversedEdgePath(strip.EdgePath) : strip.EdgePath;

    private static List<Strip> ReversedEdgePath(IEnumerable<Strip> edgePath) =>
        edgePath.Reverse().Select(
            strip => strip.Reversed() as Strip
        ).ToList();

    public override OrderedStrip Reversed() => new(strip, !reverse);

    public override bool Equals(object obj) =>
        obj switch
        {
            OrderedStrip other => strip == other.strip && reverse == other.reverse,
            UnorderedStrip otherStrip => strip == otherStrip && !reverse,
            _ => false
        };

    public override UnorderedStrip UnderlyingEdge => strip;
    public override Junction OtherVertex(Junction vertex) => strip.OtherVertex(vertex);
}
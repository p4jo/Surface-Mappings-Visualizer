using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class FibredSurface
{
    /// <summary>
    /// These are the positions where the graph map is not tight, so we can pull tight here.
    /// We have to assume that there are no invariant subforests because the isotopy would touch them!
    /// </summary>
    public IEnumerable<(Strip, EdgePoint[], Junction[])> GetLoosePositions() =>
        from strip in OrientedEdges
        let backTracks = GetBackTracks(strip).ToArray()
        let extremalVertices = GetExtremalVertices(strip).ToArray()
        where backTracks.Length != 0 || extremalVertices.Length != 0
        select (strip, backTracks, extremalVertices);

    public (Strip, EdgePoint[], Junction[]) GetLoosePositions(Strip strip)
    {
        var backTracks = GetBackTracks(strip).ToArray();
        var extremalVertices = GetExtremalVertices(strip).ToArray();
        return (strip, backTracks, extremalVertices);
    }

    public AlgorithmSuggestion TighteningSuggestion()
    {
        var options = (
            from loosePosition in GetLoosePositions()
            select (loosePosition.Item1.Name as object, PullTightString(loosePosition))
        ).ToArray();
        if (options.Length == 0) return null;
        return new AlgorithmSuggestion(
            options, 
            description: "Pull tight at one or more edges.",
            buttons: new[] { AlgorithmSuggestion.tightenAllButton, AlgorithmSuggestion.tightenSelectedButton }, 
            allowMultipleSelection: true
        );
    }

    private static string PullTightString((Strip, EdgePoint[], Junction[]) loosePositions) =>
        $"In edge {loosePositions.Item1.ColorfulName} there are the {loosePositions.Item2.Length} backtracks " +
        loosePositions.Item2.Select(edgePoint => edgePoint.ToShortString(3, 2, colorful: true)).Take(50).ToCommaSeparatedString() + 
        (loosePositions.Item2.Length > 50 ? "..." : "") +
        " and " + (loosePositions.Item3.Length > 0 ? "the" : "no") +
        (loosePositions.Item3.Length > 1 ? loosePositions.Item3.Length + " extremal vertices " : " extremal vertex ") +
        loosePositions.Item3.Take(50).ToCommaSeparatedString(v => v.ToColorfulString()) +
        (loosePositions.Item3.Length > 50 ? "..." : "");

    IEnumerable<Junction> GetExtremalVertices(Strip edge = null)
    {
        if (edge != null)
            return from vertex in graph.Vertices
                let star = Star(vertex)
                // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
                where star.Any() && star.All(strip => Equals(strip.Dg, edge))
                select vertex;
        return from vertex in graph.Vertices
            let star = Star(vertex)
            let firstOutgoingEdge = star.FirstOrDefault()
            // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
            where firstOutgoingEdge != null && firstOutgoingEdge.Dg != null &&
                  star.All(strip => Equals(strip.Dg, firstOutgoingEdge.Dg))
            select vertex;
    }

    IEnumerable<EdgePoint> GetBackTracks(Strip edge = null)
    {
        // FirstOrDefault() gets called > 300 times on this in a typical call to PullTightAll (takes > 1 second) 
        if (edge != null)
            return from edgePoint in GetBackTracks()
                where Equals(edgePoint.DgAfter(), edge)
                select edgePoint;

        return from strip in Strips
            where strip.EdgePath.Count > 1
            from i in Enumerable.Range(1, strip.EdgePath.Count - 1)
            // only internal points: Valence-2 extremal vertices are found in parallel anyways.
            let edgePoint = new EdgePoint(strip, i)
            // gets called > 20000 times in a typical call to PullTightAll (takes > 1 second)
            where Equals(edgePoint.DgBefore(), edgePoint.DgAfter())
            select edgePoint; 
    }

    private void PullTightExtremalVertex(Junction vertex)
    {
        vertex.image = null;
        foreach (var strip in Star(vertex))
        {
            strip.EdgePath = strip.EdgePath.Skip(1);
            vertex.image ??= strip.Dg?.Source;
            // for self-loops, this takes one from both ends.
        }
        // isotopy: Move vertex and shorten the strips (only the homeomorphism is changed, not the graph)
        // todo? update EdgePoints?
    }

    private void PullTightBackTrack(EdgePoint backTrack, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        if (!Equals(backTrack.DgBefore(), backTrack.DgAfter()))
        {
            Debug.LogWarning($"Assumed Backtrack at {backTrack} is not a backtrack (anymore)!");
            return;
        }

        var strip = backTrack.edge;
        var i = backTrack.index;
        if (i == 0)
        {
            PullTightExtremalVertex(strip.Source);
            return;
        }

        strip.EdgePath = strip.EdgePath.Take(i - 1).Concat(strip.EdgePath.Skip(i + 1));

        for (int k = 0; k < updateEdgePoints.Count; k++)
        {
            var j = updateEdgePoints[k].AlignedIndex(strip, out var reverse);
            if (j < i) continue;
            var res = j == i ? new EdgePoint(strip, j - 1) : new EdgePoint(strip, j - 2);
            updateEdgePoints[k] = reverse ? res.Reversed() : res;
        }

        // todo? isotopy to make the strip shorter
    }


    public void PullTightAll(string edgeName) => 
        PullTightAll(OrientedEdges.FirstOrDefault(e => e.Name == edgeName));

    public void PullTightAll(Strip edge = null)
    {
        var limit = Strips.Sum(e => e.EdgePath.Count) + graph.Vertices.Count();
        for (int i = 0; i < limit; i++)
        {
            var extremalVertex = GetExtremalVertices(edge).FirstOrDefault();
            if (extremalVertex != null)
            {
                PullTightExtremalVertex(extremalVertex);
                continue;
            }

            var backTrack = GetBackTracks(edge).FirstOrDefault();
            if (backTrack != null)
            {
                PullTightBackTrack(backTrack);
                continue;
            }

            break;
        }
    }

}
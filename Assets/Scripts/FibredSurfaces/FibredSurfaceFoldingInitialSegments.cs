using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class FibredSurface
{
    public IEnumerable<AlgorithmSuggestion> FoldEdgesInSteps(IList<Strip> edges,
        IList<EdgePoint> updateEdgePoints = null, List<Strip> edgesWithAcceptableColors = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();

        if (edges.Count < 2)
        {
            Debug.Log($"Wanted to fold {edges.Count} edges. Nothing to do.");
            yield break;
        }

        var edgePath = edges[0].EdgePath;
        if (edges.Any(edge => !edge.EdgePath.SequenceEqual(edgePath)))
            throw new("Edges to fold do not have the same edge path.");

        var star = StarOrdered(edges[0].Source).ToList();

        var edgesOrdered = SortConnectedSet(star, edges).ToList();
        if (!edges.All(edgesOrdered.Contains))
            throw new($"Edges to fold are not connected in the cyclic order: {string.Join(", ", edges)}");
        
        edges = edgesOrdered;

        
        var movements = 
            from edge in edges
            from i in Enumerable.Range(0, edge.Curve.SideCrossingWord.Count() + 1).Reverse()
            select new MovementForFolding(edges, edge, i);
        
        var movementsOrdered = movements.OrderBy(m =>
            m.Badness +
            MathF.Abs(edges.IndexOf(m.preferredEdge) - edges.Count / 2f) / edges.Count + // prefer the middle edges
            graph.AdjacentDegree(m.preferredEdge.Target) / (edges.Count + 1f) + // prefer the edges with fewer crossings
            (!char.IsLetter(m.preferredEdge.Name[^1]) ? 1f / edges.Count : 0) 
        ).ToArray();
        
        yield return new AlgorithmSuggestion(
            description: "Move vertices for folding:",
            options: from m in movementsOrdered 
                select (m as object, m.ToString()),
                // for the other steps (see fold inefficiencies) we changed it so that this doesn't contain actual references to Strips, but a  SerializationString, so that the FibredSurface can be copied, but this step is only ever applied on this uncopied Surface itself. This means we cannot undo and choose something different.
            buttons: new[] { AlgorithmSuggestion.foldMoveButton, AlgorithmSuggestion.foldMoveAltButton }
        ); 
        if (selectedOptionsDuringAlgorithmPause?.FirstOrDefault() is not MovementForFolding selectedMovement) 
            selectedMovement = movementsOrdered[0];
        
        if (selectedMovement.edges.First().fibredSurface != this)
            throw new InvalidOperationException(
                $"The selected movement {selectedMovement} is not defined on this FibredSurface. " +
                $"It was defined on {selectedMovement.edges.First().fibredSurface}.");
        
        var targetVerticesToFold =
            (from edge in edges select edge.Target).Distinct().ToArray(); // has the correct order
        
        selectedMovement.MoveVerticesForFolding(ignoreGivenEdges: selectedButtonDuringAlgorithmPause == AlgorithmSuggestion.foldMoveAltButton || selectedButtonDuringAlgorithmPause == null);
        

        var remainingEdge = selectedMovement.preferredEdge;
        var newName = char.IsLetter(remainingEdge.Name[^1]) ? remainingEdge.Name.ToLower() : NextEdgeName();
        // OrderIndexStart might be set in the FoldVertices (if one of the targetVerticesToFold was the source of the edge)
        var newColor = NextEdgeColor();
        var assignNewColor = edgesWithAcceptableColors == null || !edgesWithAcceptableColors.Contains(remainingEdge);
        
        var description =
            $"Fold the edges {edges.ToCommaSeparatedString(e => e.ColorfulName)} by removing all but the preferred edge {remainingEdge.ColorfulName}";
        if (remainingEdge.Name == newName && !assignNewColor)
            description += ".";
        else {
            if (remainingEdge.Name != newName && assignNewColor)
                description += ", recolor it and rename it: ";
            else if (assignNewColor)
                description += " and recolor it: ";
            else
                description += " and rename it: ";

            description += IDrawable.GetColorfulName(newColor, newName);
        }
        
                          
        yield return new AlgorithmSuggestion(description);
        remainingEdge.UnderlyingEdge.Name = newName;
        remainingEdge.Color = newColor;

        foreach (var edge in edges)
        {
            for (var k = 0; k < updateEdgePoints.Count; k++)
            {
                int j = updateEdgePoints[k].AlignedIndex(edge, out bool reverse);
                if (j < 0) continue;
                var res = new EdgePoint(remainingEdge, j);
                updateEdgePoints[k] = reverse ? res.Reversed() : res;
            }

        }

        ReplaceVertices(StarAtFoldedVertex().ToArray(), remainingEdge.Target, removeOldVertices: true);
        ReplaceEdges(edges, remainingEdge, removeEdges: true);

        yield break;

        IEnumerable<Strip> StarAtFoldedVertex()
        {
            yield return remainingEdge.Reversed();
            
            foreach (var vertex in targetVerticesToFold)
            {
                var localStar = StarOrdered(vertex).ToList();
                var outerEdges = localStar.ToHashSet();
                outerEdges.ExceptWith(from e in edges select e.Reversed());
                var outerEdgesSorted = SortConnectedSet(localStar, outerEdges);
                foreach (var edge in outerEdgesSorted)
                {
                    yield return edge;
                }         
            }
        }
    }

    private void ReplaceEdges(ICollection<Strip> edges, Strip newEdge, bool removeEdges = false)
    {
        foreach (var strip in Strips) 
            strip.EdgePath = strip.EdgePath.Replace(edge => 
                edges.Contains(edge) ? newEdge :
                edges.Contains(edge.Reversed()) ? newEdge.Reversed() :
                edge
            );
        if (removeEdges)
            foreach (var edge in edges)
                if (!Equals(edge.UnderlyingEdge, newEdge.UnderlyingEdge) && graph.Edges.Contains(edge.UnderlyingEdge))
                    graph.RemoveEdge(edge.UnderlyingEdge);
    }

    void ReplaceVertices(ICollection<Junction> vertices, Junction newVertex, bool removeOldVertices = false)
    {
        foreach (var strip in OrientedEdges.ToList())
            if (vertices.Contains(strip.Source))
                strip.Source = newVertex;

        if (removeOldVertices)
            foreach (var vertex in vertices)
                if (vertex != newVertex)
                    graph.RemoveVertex(vertex);
        
        foreach (var junction in graph.Vertices)
        {
            if (vertices.Contains(junction.image))
                junction.image = newVertex;
        }
    }
    
    void ReplaceVertices(IReadOnlyCollection<Strip> newOrderedStar, Junction newVertex, bool removeOldVertices = false)
    {
        ReplaceVertices(newOrderedStar.Select(strip => strip.Source).ToHashSet(), newVertex, removeOldVertices);
        int index = 0;
        foreach (var strip in newOrderedStar)
        {
            strip.OrderIndexStart = index;
            index++;
        }
    }

    public (Strip, Strip) SplitEdge(EdgePoint splitPoint, IList<EdgePoint> updateEdgePoints = null, float splitTime = -1f)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        var splitEdge = splitPoint.edge;
        var edgePath = splitEdge.EdgePath;
        if (splitTime < 0)
            splitTime = splitPoint.GetCurveTimeInJunction();

        var newVertex = new Junction(this, splitEdge.Curve[splitTime], NextVertexName(), splitPoint.Image, NextVertexColor());

        var firstSegment = splitEdge.Copy(
            curve: splitEdge.Curve.Restrict(0, splitTime),
            edgePath: edgePath.Take(splitPoint.index),
            target: newVertex,
            orderIndexEnd: 0);

        var secondSegment = splitEdge.Copy(
            curve: splitEdge.Curve.Restrict(splitTime, splitEdge.Curve.Length),
            edgePath: edgePath.Skip(splitPoint.index),
            source: newVertex,
            orderIndexStart: 1
        );

        string firstSegmentName, secondSegmentName;
        string originalName = splitEdge.Name.ToLower();
        switch (originalName[^1])
        {
            case '2':
                firstSegmentName = originalName;
                secondSegmentName = originalName[..^1] + '3';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                    secondSegmentName = originalName[..^1] + '4';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                    secondSegmentName = NextEdgeName();
                break;
            case '1':
                firstSegmentName = originalName + '-';
                secondSegmentName = originalName + '+';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                    secondSegmentName = NextEdgeName();
                break;
            default:
                firstSegmentName = originalName + '1';
                if (Strips.Any(edge => edge.Name == firstSegmentName))
                {
                    firstSegmentName = NextEdgeName();
                    secondSegmentName = firstSegmentName + '2';
                    firstSegmentName += '1';
                    break;
                }

                secondSegmentName = originalName + '2';
                if (Strips.Any(edge => edge.Name == secondSegmentName))
                {
                    secondSegmentName = originalName + '3';
                    if (Strips.Any(edge => edge.Name == secondSegmentName))
                        secondSegmentName = originalName + "2+";
                }

                break;
        }

        if (splitEdge is ReverseStrip)
        {
            // in this case, the firstSegment and secondSegment are also reversed OrderedStrips
            firstSegment.Name = secondSegmentName.ToUpper();
            secondSegment.Name = firstSegmentName.ToUpper();
        }
        else
        {
            firstSegment.Name = firstSegmentName;
            secondSegment.Name = secondSegmentName;
        }


        for (var k = 0; k < updateEdgePoints.Count; k++)
        {
            var splitPointAligned = splitPoint.AlignedWith(updateEdgePoints[k].edge, out var reverse);
            if (splitPointAligned is null) continue;

            var j = updateEdgePoints[k].index;
            var i = splitPointAligned.index;
            // if the edge point was on the split edge, it is now on the first or second segment
            // if it was on the reversed edge, it is now on the reversed first or second segment
            if (j < i) updateEdgePoints[k] = new EdgePoint(reverse ? secondSegment.Reversed() : firstSegment, j);
            else updateEdgePoints[k] = new EdgePoint(reverse ? firstSegment.Reversed() : secondSegment, j - i);
        }

        for (var k = 0; k < updateEdgePoints.Count; k++)
        {
            // move the edge point index to the right in preparation for the replacement in the next step
            var edgePoint = updateEdgePoints[k];
            updateEdgePoints[k] = new EdgePoint(edgePoint.edge,
                edgePoint.index + edgePoint.edge.EdgePath.Take(edgePoint.index)
                    .Count(edge => edge.UnderlyingEdge.Equals(splitEdge.UnderlyingEdge)));
        }

        // these three things now already happen in the Strip.set_Target and Strip.set_Source accessors
        graph.AddVertex(newVertex);
        graph.AddEdge(firstSegment.UnderlyingEdge);
        graph.AddEdge(secondSegment.UnderlyingEdge);
        graph.RemoveEdge(splitEdge.UnderlyingEdge);

        foreach (var strip in Strips)
        {
            // in each edgePath, replace the splitEdge by the two new edges
            strip.EdgePath = strip.EdgePath.Replace( edge => 
                edge.Equals(splitEdge)
                    ? new NormalEdgePath(firstSegment, secondSegment)
                    : edge.Equals(splitEdge.Reversed())
                        ? new NormalEdgePath(secondSegment.Reversed(), firstSegment.Reversed())
                        : new NormalEdgePath(edge));
        }

        return (firstSegment, secondSegment);
    }

    public IEnumerable<AlgorithmSuggestion> FoldInitialSegmentInSteps(IReadOnlyList<Strip> strips, int? i = null, IList<EdgePoint> updateEdgePoints = null)
    {
        i ??= Strip.SharedInitialSegment(strips);
        if (i == 0)
            throw new("The edges do not have a common initial segment.");

        updateEdgePoints ??= new List<EdgePoint>();
        var l = updateEdgePoints.Count;
        foreach (var strip in strips)
            updateEdgePoints.Add(strip[i.Value]);
        // we have to update the edge points after each split, as else we might split an edge and its reverse (which doesn't exist anymore after we split the original edge), thus creating 4 instead of 3 new edges! (or instead of 2)


        var initialStripSegments = new List<Strip>(strips.Count);
        var edgesWithAcceptableColors = new List<Strip>(strips.Count);
        var renameSegments = new Dictionary<string, Strip>(strips.Count);
        var arclengthParametrization = (from strip in strips select 
            (   strip.Curve,
                GeodesicSurface.ArclengthFromTime(strip.Curve),
                GeodesicSurface.TimeFromArclength(strip.Curve)
            )).ToArray(); // This took up 90 % of the time (6s for a simple fold), because of ModelSurface.DistanceSquared calling ClampPoint often. 
        float maxDistanceAlongCurves = arclengthParametrization.Min( input =>
        {
            var (curve, lengthFromTime, _) = input;
            float jumpTime = curve.VisualJumpTimes.DefaultIfEmpty(curve.Length).First();
            return lengthFromTime(jumpTime);
        });
        float splitLength = maxDistanceAlongCurves * 0.5f;
        // todo: optimize this with respect to the movements
        yield return
            new AlgorithmSuggestion($"To fold the initial segments of length {i} of the edges {strips.ToCommaSeparatedString(e => e.ColorfulName)}, first split them.");
        for (var index = 0; index < strips.Count; index++)
        {
            var splitEdgePoint = updateEdgePoints[l + index];
            var edge = splitEdgePoint.edge; // instead of strips[index]
            int splitIndex = splitEdgePoint.index;
            if (splitIndex == edge.EdgePath.Count) // it is never 0
            {
                initialStripSegments.Add(edge);
                edgesWithAcceptableColors.Add(edge);
                continue;
            }

            var timeFromLength = arclengthParametrization[index].Item3;
            float splitTime = timeFromLength(splitLength);
            var (firstSegment, secondSegment) = SplitEdge(splitEdgePoint, updateEdgePoints, splitTime);
            
            initialStripSegments.Add(firstSegment);
            var preferredName = edge.Name.ToLower().TrimEnd('2');
            if (!Strips.Any(e => e.Name == preferredName))
                renameSegments[preferredName] = secondSegment;
        }

        // give the remaining segments the original names. 
        // We do this here so that, if we split an edge and its reverse, the middle part gets the same name as the original edge.
        foreach (var (name, secondSegment) in renameSegments) 
            secondSegment.UnderlyingEdge.Name = name;


        foreach (var yieldedString in FoldEdgesInSteps(initialStripSegments, updateEdgePoints, edgesWithAcceptableColors: edgesWithAcceptableColors))
            yield return yieldedString; 
    }

}
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130
// ReSharper disable CheckNamespace


public partial class FibredSurface
{
    private sealed class JunctionSegment
    {
        public Junction junction;
        public int startIndex;
        public int length;

        public bool Covers(IReadOnlyCollection<int> positions, int starCount)
        {
            if (starCount <= 0) return positions.Count == 0;
            foreach (var position in positions)
            {
                var relative = (position - startIndex) % starCount;
                if (relative < 0) relative += starCount;
                if (relative >= length)
                    return false;
            }

            return true;
        }
    }

    private sealed class StripSegment : QuikGraph.IEdge<JunctionSegment>
    {
        public readonly Strip strip;
        public readonly int positionAlongEdgePath;

        public StripSegment(Strip strip, int positionAlongEdgePath)
        {
            this.strip = strip;
            this.positionAlongEdgePath = positionAlongEdgePath;
        }

        public JunctionSegment Source { get; set; } = null!;
        public JunctionSegment Target { get; set; } = null!;

        public override string ToString() => $"{strip.ColorfulName}@{positionAlongEdgePath}";
    }

    public Dictionary<UnorientedStrip, List<Strip>> Stripes()
    {
        var result = Strips.ToDictionary(s => s, _ => new List<Strip>());
        var cutGraph = new QuikGraph.UndirectedGraph<JunctionSegment, StripSegment>(true);

        var starCache = graph.Vertices.ToDictionary(v => v, v => StarOrdered(v).ToList());
        var segmentsByImage = new Dictionary<Junction, List<JunctionSegment>>();
        var splitByJunction = new Dictionary<Junction, JunctionSegment>();

        foreach (var junction in graph.Vertices)
        {
            var image = junction.image ?? junction;
            var imageStar = starCache.TryGetValue(image, out var star) ? star : new List<Strip>();
            var mappedStrips = StarOrdered(junction)
                .Select(strip => strip.Dg)
                .Where(strip => strip != null)
                .ToList();

            var positions = mappedStrips
                .Select(strip => imageStar.IndexOf(strip))
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();

            if (!segmentsByImage.TryGetValue(image, out var segments))
                segmentsByImage[image] = segments = new List<JunctionSegment>();

            var segment = segments
                .Where(candidate => candidate.Covers(positions, imageStar.Count))
                .OrderBy(candidate => candidate.length)
                .ThenBy(candidate => candidate.startIndex)
                .FirstOrDefault();

            if (segment == null)
            {
                segment = CreateSegment(image, imageStar, positions);
                segments.Add(segment);
            }

            splitByJunction[junction] = segment;
        }

        foreach (var strip in Strips)
        {
            var edgePath = strip.EdgePath.ToList();
            if (edgePath.Count == 0)
            {
                result[strip].Add(strip);
                continue;
            }

            for (var i = 0; i < edgePath.Count; i++)
            {
                var pathStrip = edgePath[i];
                var sourceSegment = GetSplitSegment(pathStrip.Source);
                var targetSegment = GetSplitSegment(pathStrip.Target);

                var copiedStrip = pathStrip.Copy(
                    fibredSurface: this,
                    source: sourceSegment.junction,
                    target: targetSegment.junction,
                    edgePath: pathStrip.EdgePath,
                    name: pathStrip.Name,
                    orderIndexStart: pathStrip.OrderIndexStart,
                    orderIndexEnd: pathStrip.OrderIndexEnd
                );

                var segmentEdge = new StripSegment(copiedStrip, i)
                {
                    Source = sourceSegment,
                    Target = targetSegment
                };
                cutGraph.AddVerticesAndEdge(segmentEdge);
                result[strip].Add(copiedStrip);
            }
        }

        return result;

        JunctionSegment GetSplitSegment(Junction junction)
        {
            if (splitByJunction.TryGetValue(junction, out var segment))
                return segment;

            var image = junction.image ?? junction;
            if (!segmentsByImage.TryGetValue(image, out var imageSegments))
            {
                var imageStar = starCache.TryGetValue(image, out var star) ? star : new List<Strip>();
                segment = CreateSegment(image, imageStar, Array.Empty<int>());
                imageSegments = new List<JunctionSegment> { segment };
                segmentsByImage[image] = imageSegments;
            }
            else
            {
                segment = imageSegments.FirstOrDefault();
                if (segment == null)
                {
                    var imageStar = starCache.TryGetValue(image, out var star) ? star : new List<Strip>();
                    segment = CreateSegment(image, imageStar, Array.Empty<int>());
                    imageSegments.Add(segment);
                }
            }

            splitByJunction[junction] = segment;
            return segment;
        }

        JunctionSegment CreateSegment(Junction image, IReadOnlyList<Strip> imageStar, IReadOnlyList<int> positions)
        {
            var segment = new JunctionSegment { junction = image };
            if (imageStar.Count == 0)
            {
                segment.startIndex = 0;
                segment.length = 0;
                return segment;
            }

            if (positions.Count == 0)
            {
                segment.startIndex = 0;
                segment.length = imageStar.Count;
                return segment;
            }

            var uniquePositions = positions.Distinct().OrderBy(index => index).ToArray();
            if (uniquePositions.Length == 1)
            {
                segment.startIndex = uniquePositions[0];
                segment.length = 1;
                return segment;
            }

            var bestGap = -1;
            var bestGapEnd = uniquePositions[^1];
            for (var i = 0; i < uniquePositions.Length; i++)
            {
                var current = uniquePositions[i];
                var next = uniquePositions[(i + 1) % uniquePositions.Length];
                var gap = (next - current - 1 + imageStar.Count) % imageStar.Count;
                if (gap <= bestGap) continue;
                bestGap = gap;
                bestGapEnd = current;
            }

            segment.startIndex = (bestGapEnd + 1) % imageStar.Count;
            segment.length = Math.Max(1, imageStar.Count - bestGap);
            return segment;
        }
    }
}
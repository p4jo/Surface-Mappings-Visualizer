using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using MathNet.Numerics.LinearAlgebra;
using QuikGraph;
using QuikGraph.Algorithms;
using Unity.VisualScripting;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;
using Object = UnityEngine.Object;

public class PushingPath : IPatchedDrawnsformable
{
    public abstract class Entry
    {
        public abstract EdgePath AssociatedPath(PushingPath pushingPath);

        public readonly IDrawnsformable drawnsformable;

        public readonly IEnumerable<PointNearVertex> pointsNearVertices;
        // TODO: Refactor. Save the turns extra during the constructor and remove these points from the Segment class

        protected Entry(IEnumerable<PointNearVertex> pointsNearVertices, IDrawnsformable drawnsformable)
        {
            this.pointsNearVertices = pointsNearVertices;
            this.drawnsformable = drawnsformable;
        }

        public PointNearVertex GetPointInQuadrant(Strip xAxis, Strip yAxis) => 
            pointsNearVertices.FirstOrDefault(pointNearVertex => 
                Equals(pointNearVertex.xAxis, xAxis) && Equals(pointNearVertex.yAxis, yAxis)
            );
    }

    public class EdgeFollowing : Entry
    {
        public readonly Strip strip;
        public readonly float xDistance;
        public readonly float startY;
        public float endY;
        private readonly Strip _previousStripInCyclicOrderStart;
        private readonly Strip nextStripInCyclicOrderEnd;

        public EdgeFollowing(Strip strip, float xDistance, float startY, float endY,
            Strip previousStripInCyclicOrderStart,
            Strip nextStripInCyclicOrderEnd):
            base(
                pointsNearVertices: new[]
                { // todo: xDistance > 0; else save that we are reversed!, reorder axes; second point at end
                    // todo: Refactor: save only "outer turn" with xUpwards and yUpwards and leave EdgeFollowing free from PointNearVertex business
                    new PointNearVertex(
                        previousStripInCyclicOrderStart,
                        strip,
                        xDistance,
                        startY,
                        PointNearVertex.SegmentType.yUpwards
                    ),
                    new PointNearVertex(
                        strip,
                        nextStripInCyclicOrderEnd,
                        startY,
                        xDistance,
                        PointNearVertex.SegmentType.xUpwards
                    )
                },
                new ShiftedCurve(
                    strip.Curve.Restrict(startY, endY),
                    xDistance
                )// todo: rescale 
            )
        {
            this.strip = strip;
            this.xDistance = xDistance;
            this.startY = startY;
            this.endY = endY;
            this._previousStripInCyclicOrderStart = previousStripInCyclicOrderStart;
            this.nextStripInCyclicOrderEnd = nextStripInCyclicOrderEnd;
            if (xDistance == 0) throw new ArgumentException();
            
        }

        public override EdgePath AssociatedPath(PushingPath pushingPath) => new NormalEdgePath( strip );
        
        public float AlignedDistance(Strip other, out bool reverse)
        {
            reverse = !Equals(other, strip);
            return reverse ? -xDistance : xDistance;
        }
        public float AlignedDistance(Strip other) => AlignedDistance(other, out _); 

        public float AlignedStartDistance(Strip other, out bool reverse)
        {
            reverse = !Equals(other, strip);
            return reverse ? endY : startY;
        }
        public float AlignedStartDistance(Strip other) => AlignedStartDistance(other, out _); 
    }

    public class EdgeCrossing : Entry
    {
        public readonly Strip crossedEdge;
        /// <summary>
        /// This is how the pushing path crosses the edge.
        /// If true, the edge gets dragged along the conjugation path along the left, then following the puncture word clockwise (following the edge to the right), then following the conjugation path backwards on the left. 
        /// </summary>
        public readonly bool rightToLeft;

        /// <summary>
        /// Where the edge is crossed. Negative values are at the start, positive values are at the end.
        /// If the edge is crossed multiple times, then this determines the order.
        /// </summary>
        public readonly float positionAlongEdge;

        public readonly float startDistanceToCrossedEdge;

        public EdgeCrossing(Strip crossedEdge, bool rightToLeft, float positionAlongEdge,
            float startDistanceToCrossedEdge, Strip lastEdge):
            base(
                new[]
                {
                    rightToLeft
                        ? new PointNearVertex(
                            lastEdge.Reversed(),
                            crossedEdge,
                            startDistanceToCrossedEdge,
                            positionAlongEdge,
                            PointNearVertex.SegmentType.xDownwards
                        )
                        : new PointNearVertex(
                            crossedEdge,
                            lastEdge.Reversed(),
                            positionAlongEdge,
                            startDistanceToCrossedEdge,
                            PointNearVertex.SegmentType.yDownwards
                        ) // todo?
                }, (IDrawnsformable) ShiftedCurve.CurveToRight(positionAlongEdge, startDistanceToCrossedEdge, crossedEdge.Curve) ?? crossedEdge.Curve.ValueAt(positionAlongEdge)
            )
        {
            this.crossedEdge = crossedEdge;
            this.rightToLeft = rightToLeft;
            this.positionAlongEdge = positionAlongEdge;
            this.startDistanceToCrossedEdge = startDistanceToCrossedEdge;
        }

        public override EdgePath AssociatedPath(PushingPath pushingPath) => EdgePath.Empty;

    }

    public class SelfIntersection : Entry
    {
        public readonly bool rightToLeft;
        public SelfIntersectionSecondTime secondTime;

        public SelfIntersection(bool rightToLeft,
            SelfIntersectionSecondTime secondTime, 
            PointNearVertex intersectionPoint
        ): base(
            new []{ intersectionPoint },
            intersectionPoint.ToPoint()
        )
        {
            this.rightToLeft = rightToLeft;
            this.secondTime = secondTime;
        }

        public override EdgePath AssociatedPath(PushingPath pushingPath) => new ConjugateEdgePath(
            rightToLeft ? pushingPath.punctureWord : pushingPath.punctureWord.Inverse(),
            pushingPath.ConjugationPath(pushingPath.path.IndexOf(secondTime)),
            true
        );
    }

    public class SelfIntersectionSecondTime : Entry
    {
        public override EdgePath AssociatedPath(PushingPath pushingPath) => EdgePath.Empty;

        public SelfIntersectionSecondTime(IEnumerable<PointNearVertex> pointsNearVertices, IDrawnsformable drawnsformable) : base(pointsNearVertices, drawnsformable)
        {
        }

        public SelfIntersectionSecondTime(PointNearVertex intersection) : base(
            new []{ intersection },
            intersection.ToPoint()    
        )
        {
            throw new NotImplementedException();
        }
    }
    
    public class PointNearVertex
    {
        private readonly SegmentType type;

        [Flags]
        public enum SegmentType
        {
            xDownwards = 1,
            xUpwards = 2,
            yDownwards = 4,
            yUpwards = 8
        }
        
        public readonly Strip xAxis;
        public readonly Strip yAxis;
        public readonly float x;
        public readonly float y;

        public PointNearVertex(Strip xAxis, Strip yAxis, float x, float y, SegmentType type)
        {
            this.xAxis = xAxis;
            this.yAxis = yAxis;
            this.x = x;
            this.y = y;
            this.type = type;
        }

        public Point ToPoint()
        {
            if (yAxis.Curve.Surface is not GeodesicSurface surface)
                return yAxis.Source.Position;
            return surface.GetGeodesic(
                new TangentVector(
                    xAxis.Curve[x],
                    yAxis.Curve.StartVelocity.vector.normalized
                ), y, "Point near vertex segment"
            ).EndPosition;
        }
    }
    
    private readonly List<Entry> path;
    /// <summary>
    /// This is where the small loop around the marked point / puncture that all pushed curves follow gets isotoped in the graph.
    /// This is the path that starts where the pushingPath starts and follows all edges to the right, never crossing an edge.
    /// It thus runs around the marked point / puncture clockwise 
    /// </summary>
    public readonly EdgePath punctureWord;
    
    public readonly EdgePath edgePath;
    
    private PushingPath(List<Entry> path, EdgePath edgePath)
    {
        this.path = path;
        this.edgePath = edgePath;
        punctureWord = new NormalEdgePath( FibredSurface.BoundaryWord(edgePath.First()) );
        // todo: Feature. A variable? I.e. an EdgePath that encapsulates another EdgePath but gets displayed as a single symbol
    }


    public PushingPath(List<Entry> path) : this(
        path,
        new NormalEdgePath(
            from entry in path
            where entry is EdgeFollowing
            select ((EdgeFollowing)entry).strip
        )
    )
    {  }
    
    /// <summary>
    /// This creates a PushingPath based at a marked point to the right (or left) of the start of the first edge.
    /// You should set Color and Name as well.
    /// </summary>
    public PushingPath(EdgePath edgePath, bool startLeft = false) :
        this(edgePath, 
            edgePath.First().graph.Vertices.ToDictionary(
                v => v, 
                v => FibredSurface.StarOrdered(v).ToList()
            ), startLeft)
    {   }
        
    public PushingPath(EdgePath edgePath, IReadOnlyDictionary<Junction, List<Strip>> stars, bool startLeft = false) :
        this(FindPath(edgePath, stars, startLeft), edgePath)
    {  }
        
    private static List<Entry> FindPath(EdgePath edgePath, IReadOnlyDictionary<Junction, List<Strip>> stars, bool startLeft = false) 
    {
        var path = new List<Entry>();
        var currentX = startLeft ? -1f : 1f;
        var currentlyFollowedStrip = edgePath.First();
        var nextStartY = 2f; // todo: the first start distance can be adjusted , bigger is better?
        foreach (var nextStrip in edgePath.CyclicShift(1)) // todo: in last step check that we align with the starting point.
        {
            var star = stars[currentlyFollowedStrip.Target].CyclicShift(currentlyFollowedStrip.Reversed()).Skip(1).ToList();
            var nextStripInCyclicOrderEnd = currentX > 0 ? star[0] : star[^1];
            var oldStar = stars[currentlyFollowedStrip.Source].CyclicShift(currentlyFollowedStrip).Skip(1);
            var nextStripInCyclicOrderStart = currentX > 0 ? oldStar.Last() : oldStar.First();
            var lastFollowingEdge = new EdgeFollowing(currentlyFollowedStrip, currentX, nextStartY, 0f, nextStripInCyclicOrderStart, nextStripInCyclicOrderEnd); 
            // we will want to edit the endDistance
            path.Add(lastFollowingEdge);
            
            
            if (!star.Contains(nextStrip)) 
                throw new ArgumentException($"Your pushing path contains broken concatenation points or backtracking between {currentlyFollowedStrip.Name} and {nextStrip.Name}!");

            int indexInStar = star.IndexOf(nextStrip);
            int edgesToCrossClockwise = star.Count - 1 - indexInStar + (currentX > 0 ? 1 : 0);
            int edgesToCrossCounterClockwise =  indexInStar + (currentX < 0 ? 1 : 0);
            
            bool turnAroundClockwise = edgesToCrossClockwise < edgesToCrossCounterClockwise; // todo?
            
            if (turnAroundClockwise) 
                star.Reverse();

            var currentStartDistanceToCrossedEdge = float.MaxValue;
            if (currentX > 0 && turnAroundClockwise || currentX < 0 && !turnAroundClockwise)
            {  // in these cases we have to cross the edge that we just followed before continuing with the edge crossings
                currentlyFollowedStrip = star[^1];
                star.Insert(0, currentlyFollowedStrip.Reversed());
                currentStartDistanceToCrossedEdge = Mathf.Abs(currentX);
                // currentDistance = ?;
                
                if (path.Count > 0 && path[^1] is EdgeFollowing edgeFollowing)
                    edgeFollowing.endY = currentX;
            }
            
            bool crossedAnEdge = false;
            bool thereIsANextCrossing = !star[0].Equals(nextStrip);
            for (var starIndex = 0; starIndex < star.Count; starIndex++)
            {
                var edgeCrossed = star[starIndex];
                if (!thereIsANextCrossing)
                {
                    if (starIndex == 0) // haven't crossed an edge
                    {
                        // insert normal turn
                        nextStartY = Mathf.Abs(currentX);

                        var max = float.MinValue;
                        foreach (var t in path)
                        {
                            if (t is not EdgeFollowing otherEdgeFollowing)
                                continue; //  todo: also check for turns?
                            if (!otherEdgeFollowing.strip.UnderlyingEdge.Equals(nextStrip.UnderlyingEdge))
                                continue;
                            var otherEdgeFollowingAlignedDistance =
                                otherEdgeFollowing.AlignedDistance(nextStrip);
                            if (otherEdgeFollowingAlignedDistance < 0)
                                continue;
                            if (otherEdgeFollowingAlignedDistance > max)
                                max = otherEdgeFollowingAlignedDistance;
                        }

                        currentX = MathF.Sign(currentX) * ( max == float.MinValue ? 1f : max * 1.333333333333f );

                        if (path.Count > 0 && path[^1] is EdgeFollowing edgeFollowing)
                            edgeFollowing.endY = currentX;
                        break;
                        // we avoid all self-intersections here
                    }

                    // todo: can we change current distance? We should, in particular if there are already parallel strands along nextEdge, but this makes it even more complicated, because we add extra corners

                    nextStartY = 0;
                    break;
                }
                thereIsANextCrossing = !Equals(star[starIndex + 1], nextStrip); // is only evaluated when we haven't hit nextStrip yet, but we know that star contains nextStrip, so starIndex + 1 is not too big


                var outgoingDistanceThis = thereIsANextCrossing ? currentX : float.MaxValue;

                var selfIntersections =
                    new List<(SelfIntersectionSecondTime secondIntersectionTime, float sideDistance)>();

                List<PointNearVertex> obstructions = path.SelectMany(entry => entry.pointsNearVertices).ToList();
                
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i] is not EdgeFollowing otherEdgeFollowing)
                        continue;
                    if (!otherEdgeFollowing.strip.UnderlyingEdge.Equals(edgeCrossed.UnderlyingEdge))
                        continue;
                    var otherEdgeFollowingAlignedStartDistance =
                        otherEdgeFollowing.AlignedStartDistance(edgeCrossed, out var otherEdgeFollowingIsIncoming);


                    var otherEdgeY = (turnAroundClockwise ? -1 : 1) *
                                                                otherEdgeFollowing.AlignedDistance(edgeCrossed);

                    var x = (turnAroundClockwise ? -currentX : currentX);
                    if (otherEdgeFollowingAlignedStartDistance > x)
                        continue;
                    if (otherEdgeY > currentStartDistanceToCrossedEdge) // we start closer to the crossed edge than the other edge is to it.
                        continue;

                    if (thereIsANextCrossing && -outgoingDistanceThis <= otherEdgeY)
                    {
                        // todo: also check for turns
                        outgoingDistanceThis =
                            -0.75f *
                            otherEdgeY; // turn earlier so that we don't intersect it
                    }

                    if (otherEdgeY <= -outgoingDistanceThis) // we turn away closer to the crossed edge than the other edge is
                        continue;
                    PointNearVertex intersection = new PointNearVertex(edgeCrossed, otherEdgeFollowing.strip, x, otherEdgeY, (PointNearVertex.SegmentType)0); // todo
                    // todo: might also cross other turning segments, i.e. consecutive (not separated by EdgeFollowings) EdgeCrossings
                    var secondIntersectionTime = new SelfIntersectionSecondTime(intersection);
                    selfIntersections.Add((secondIntersectionTime, otherEdgeY));
                    var firstIntersectionTime = new SelfIntersection(
                        otherEdgeFollowingIsIncoming == turnAroundClockwise,
                        secondIntersectionTime,
                        intersection
                    );
                    path.Insert(otherEdgeFollowingIsIncoming ? i + 1 : i, firstIntersectionTime);
                    i++; // don't count the same thing again (or don't read the firstIntTime)
                    if (thereIsANextCrossing)
                        currentX = outgoingDistanceThis;
                }

                var intersectionsBeforeTheCrossing =
                    selfIntersections.Where(
                        t => t.sideDistance > 0
                    ).OrderByDescending(
                        t => t.sideDistance
                    ).Select(
                        t => t.secondIntersectionTime
                    );
                path.AddRange(intersectionsBeforeTheCrossing);

                path.Add(new EdgeCrossing(
                    edgeCrossed,
                    !turnAroundClockwise,
                    Mathf.Abs(currentX),
                    currentStartDistanceToCrossedEdge,
                    starIndex > 0 ? star[starIndex - 1] : currentlyFollowedStrip
                ));

                var intersectionsAfterTheCrossing =
                    selfIntersections.Where(
                        t => t.sideDistance < 0
                    ).OrderByDescending(
                        t => t.sideDistance
                    ).Select(
                        t => t.secondIntersectionTime
                    ); // this is empty because we choose our distance after the crossing so small
                path.AddRange(intersectionsAfterTheCrossing);

                currentStartDistanceToCrossedEdge = currentX;
                nextStartY = 0f;
            }


            currentlyFollowedStrip = nextStrip;
        }

        return path;
    }

    public EdgePath ConjugationPath(int startTime)
    {
        return EdgePath.Concatenate(path.Skip(startTime).Select(p => p.AssociatedPath(this)));
    }

    public EdgePath Image(UnorientedStrip strip)
    {
        EdgePath res = EdgePath.Empty;
        bool notYetCrossed = true;
        foreach (var (time, entry) in
                 path.Enumerate().Where(p =>
                         p.t is EdgeCrossing crossing && Equals(crossing.crossedEdge, strip)
                     ).OrderBy(p => ((EdgeCrossing)p.t).positionAlongEdge)
                )
        {
            var edgeCrossing = (EdgeCrossing)entry;
            if (edgeCrossing.positionAlongEdge > 0 && notYetCrossed)
            {
                notYetCrossed = false;
                res = res.Concat(new NormalEdgePath(strip));
            }

            res = res.Concat(new ConjugateEdgePath(
                edgeCrossing.rightToLeft ? punctureWord : punctureWord.Inverse(),
                ConjugationPath(time),
                true
            ));
        }
        if (notYetCrossed)    
            res = res.Concat(new NormalEdgePath(strip));

        return res;
    }

    public string Name { get; set; }

    public Color Color // Copied from "virtual" implementation in PatchedDrawnsformable
    {
        get => Patches.FirstOrDefault()?.Color ?? Color.magenta;
        set
        {
            foreach (var patch in Patches)
                patch.Color = value;
        }
    }

    public IPatchedDrawnsformable Copy() => new PushingPath(path, edgePath) { Name = Name, Color = Color } ;

    public IEnumerable<IDrawnsformable> Patches => from entry in path select entry.drawnsformable;
}
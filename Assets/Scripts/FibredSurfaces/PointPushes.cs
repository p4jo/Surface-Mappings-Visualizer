using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using MathNet.Numerics.LinearAlgebra;
using QuikGraph;
using QuikGraph.Algorithms;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;
using Object = UnityEngine.Object;

public class PushingPath : IPatchedDrawnsformable
{
    private abstract class Entry
    {
        public virtual EdgePath AssociatedPath(PushingPath pushingPath) => EdgePath.Empty;
    }

    private class EdgeFollowing : Entry
    {
        public readonly Strip strip;
        private readonly bool followingLeft;

        public EdgeFollowing(Strip strip, bool followingLeft)
        {
            this.strip = strip;
            this.followingLeft = followingLeft;
        }

        public override EdgePath AssociatedPath(PushingPath pushingPath) => new NormalEdgePath( strip );

        public override string ToString() => strip.Name + (followingLeft ? "L" : "R");
    }

    private class EdgeCrossing : Entry
    {
        public readonly Strip crossedEdge;
        /// <summary>
        /// This is how the pushing path crosses the edge.
        /// If true, the edge gets dragged along the conjugation path along the left, then following the puncture word clockwise (following the edge to the right), then following the conjugation path backwards on the left. 
        /// </summary>
        public readonly bool rightToLeft;

        /// <summary>
        /// If the edge is crossed multiple times, then this determines the order.
        /// </summary>
        public readonly Variable positionAlongEdge;

        /// <summary>
        /// An edge is crossed near its start.
        /// </summary>
        public EdgeCrossing(Strip crossedEdge, bool rightToLeft, Variable positionAlongEdge)
        {
            this.crossedEdge = crossedEdge;
            this.rightToLeft = rightToLeft;
            this.positionAlongEdge = positionAlongEdge;
        }

        public override string ToString() => $"({crossedEdge.Name}" + (rightToLeft ? "<-" : "->") + ")";
    }

    private class SelfIntersection : Entry
    {
        public readonly bool rightToLeft;
        public SelfIntersectionSecondTime secondTime;

        public SelfIntersection(bool rightToLeft, SelfIntersectionSecondTime secondTime)
        {
            this.rightToLeft = rightToLeft;
            this.secondTime = secondTime;
        }

        public override EdgePath AssociatedPath(PushingPath pushingPath) => new ConjugateEdgePath(
            rightToLeft ? pushingPath.punctureWord : pushingPath.punctureWord.Inverse(),
            pushingPath.ConjugationPath(pushingPath.path.IndexOf(secondTime)),
            true
        );

        public override string ToString() => $"[{secondTime.name}]";
    }

    private class SelfIntersectionSecondTime : Entry
    {
        internal readonly string name;

        public SelfIntersectionSecondTime(string name)
        {
            this.name = name;
        }

        public override string ToString() => $"{{{name}}}";
    }

    public class Variable
    {
        public readonly string name;

        private float? value;

        public bool Concrete => value.HasValue;

        public float Value => value ?? 1f;

        public Variable(string name, float? value = null)
        {
            this.value = value;
            this.name = name;
        }

        public void SetValue(float f)
        {
            value = f;
        }

        public void FreeVariable()
        {
            value = null;
        }

        public static implicit operator Variable(float f) => new($"implicitly converted constant {f}", f);

        public override string ToString() => Concrete ? $"{name} = {Value}" : $"{name} = ?";
    }
    
    private class CornerSegment
    {
        public bool Concrete => x.Concrete && y.Concrete;
        
        private readonly SegmentType type;

        [Flags]
        public enum SegmentType
        {
            xDownwards = 1,
            xUpwards = 2,
            yDownwards = 4,
            yUpwards = 8,
            innerTurn = xDownwards | yDownwards,
            outerTurn = xUpwards | yUpwards,
            halfTurn = xUpwards | yDownwards,
            halfTurnFromLeft = yUpwards | xDownwards
        }

        public readonly Strip xAxis;
        /// <summary>
        /// The yAxis is always assumed to be the successor of xAxis in the cyclic order.
        /// </summary>
        public readonly Strip yAxis;
        public readonly Variable x;
        public readonly Variable y;

        public CornerSegment(Strip xAxis, Strip yAxis, Variable x, Variable y, SegmentType type, bool flip)
        {
            if (flip)
            {
                this.xAxis = yAxis;
                this.yAxis = xAxis;
                this.x = y;
                this.y = x;
                this.type = (type.HasFlag(SegmentType.xDownwards) ? SegmentType.yDownwards : 0) |
                        (type.HasFlag(SegmentType.xUpwards) ? SegmentType.yUpwards : 0) |
                        (type.HasFlag(SegmentType.yDownwards) ? SegmentType.xDownwards : 0) |
                        (type.HasFlag(SegmentType.yUpwards) ? SegmentType.xUpwards : 0);
            }
            else
            {
                this.xAxis = xAxis;
                this.yAxis = yAxis;
                this.x = x;
                this.y = y;
                this.type = type;
            }
        }

        public Point ToPoint()
        {
            if (!Concrete)
                Debug.LogWarning("Converted a PointNearVertex with free variables to Point");
            if (yAxis.Curve.Surface is not GeodesicSurface surface)
                return yAxis.Source.Position;
            return surface.GetGeodesic(
                new TangentVector(
                    xAxis.Curve[x.Concrete ? x.Value : 1f],
                    yAxis.Curve.StartVelocity.vector.normalized
                ), y.Concrete ? y.Value : 1f, "Point near vertex segment"
            ).EndPosition;
        }

        public IDrawnsformable ToDrawnsformable()
        {
            if (!Concrete)
                Debug.LogWarning("Converted a PointNearVertex with free variables to Drawnsformable");
            if (yAxis.Curve.Surface is not GeodesicSurface surface)
                return yAxis.Source.Position; // todo?

            Curve xSegment = null;
            Curve ySegment = null;
            if (type.HasFlag(SegmentType.xDownwards))
                xSegment = new ShiftedCurve(xAxis.Curve.Restrict(0, x.Value), y.Value);
            if (type.HasFlag(SegmentType.xUpwards))
                xSegment = new ShiftedCurve(xAxis.Curve.Restrict(x.Value, xAxis.Curve.Length / 2), y.Value);
            if (type.HasFlag(SegmentType.yDownwards))
                ySegment = new ShiftedCurve(yAxis.Curve.Restrict(0, y.Value), x.Value);
            if (type.HasFlag(SegmentType.yUpwards))
                ySegment = new ShiftedCurve(yAxis.Curve.Restrict(y.Value, yAxis.Curve.Length / 2), x.Value);
            // todo: unordered curve? will be displayed as ordered

            if (xSegment != null && ySegment != null)
                return new ConcatenatedCurve(new[] { xSegment, ySegment }, smoothed: true); // todo: check concatenation
            return (IDrawnsformable)(xSegment ?? ySegment) ?? ToPoint();
        }
    }

    private readonly List<Entry> pathWithoutSelfIntersections;

    private List<Entry> path;
    /// <summary>
    /// This is where the small loop around the marked point / puncture that all pushed curves follow gets isotoped in the graph.
    /// This is the path that starts where the pushingPath starts and follows all edges to the right, never crossing an edge.
    /// It thus runs around the marked point / puncture clockwise 
    /// </summary>
    public readonly EdgePath punctureWord;
    
    public readonly EdgePath edgePath;

    public bool Concrete => cornerList.All(pt => pt.Concrete);

    private readonly List<CornerSegment> cornerList;
    private readonly List<Variable> variables;

    private PushingPath(List<Entry> path, EdgePath edgePath, List<CornerSegment> cornerList, List<Variable> variables)
    {
        this.pathWithoutSelfIntersections = this.path = path;
        this.edgePath = edgePath;
        this.cornerList = cornerList;
        this.variables = variables;
        punctureWord = new NormalEdgePath( FibredSurface.BoundaryWord(edgePath.First()) );
        // todo: Feature. A variable? I.e. an EdgePath that encapsulates another EdgePath but gets displayed as a single symbol
    }
    
    /// <summary>
    /// This creates a PushingPath based at a marked point to the right (or left) of the start of the first edge.
    /// You should set Color and Name as well.
    /// The PushingPath will be created with free variables and will be inconcrete, thus having no idea about self-intersections. Call Concretize();
    /// </summary>
    public PushingPath(EdgePath edgePath, bool startLeft = false) :
        this(edgePath, 
            edgePath.First().graph.Vertices.ToDictionary(
                v => v, 
                v => FibredSurface.StarOrdered(v).ToList()
            ), startLeft)
    {   }

    private PushingPath(EdgePath edgePath, IReadOnlyDictionary<Junction, List<Strip>> stars, bool startLeft = false) :
        this(
            FindPathWithCrossingsButNoSelfIntersections(
                edgePath,
                stars,
                startLeft,
                out var cornerPoints,
                out var variables
            ),
            edgePath,
            cornerPoints, 
            variables
        )
    {  }
        
    private static List<Entry> FindPathWithCrossingsButNoSelfIntersections(EdgePath edgePath, IReadOnlyDictionary<Junction, List<Strip>> stars,
        bool startLeft, out List<CornerSegment> cornerPoints, out List<Variable> variables)
    {
        variables = new List<Variable>(2 * edgePath.Count);
        cornerPoints = new List<CornerSegment>(2 * edgePath.Count);
        // todo: save the corresponding indices of the cornerPoints in path
        
        var path = new List<Entry>(2 * edgePath.Count);

        bool followingLeft = startLeft;
        
        var currentDistanceToFollowedStrip = new Variable("distance to first followed strip");
        variables.Add(currentDistanceToFollowedStrip);

        // we currently insert the first half-edge-following last.
        Variable nextStartY = 0; // todo: the first start distance can be adjusted, bigger is better? 
        List<Strip> edgesToFollow = edgePath.ToList();
        for (var index = 0; index < edgesToFollow.Count; index++)
        {
            var currentlyFollowedStrip = edgesToFollow[index];
            var nextStrip = edgesToFollow[(index + 1) % edgesToFollow.Count];

            var lastFollowingEdge = new EdgeFollowing(currentlyFollowedStrip, followingLeft);
            path.Add(lastFollowingEdge);

            var star = stars[currentlyFollowedStrip.Target].CyclicShift(currentlyFollowedStrip.Reversed()).Skip(1)
                .ToList();
            var otherAxisInArrivingQuadrant = followingLeft ? star[^1] : star[0];

            int indexInStar = star.IndexOf(nextStrip);

            if (indexInStar < 0) // doesn't contain the nextStrip
                throw new ArgumentException(
                    $"Your pushing path contains broken concatenation points or backtracking between {currentlyFollowedStrip.Name} and {nextStrip.Name}!");

            int edgesToCrossClockwise = star.Count - 1 - indexInStar + (!followingLeft ? 1 : 0);
            int edgesToCrossCounterClockwise = indexInStar + (followingLeft ? 1 : 0);

            bool turnAroundClockwise =
                edgesToCrossClockwise <
                edgesToCrossCounterClockwise; // todo? optimize intersections? probably way too hard (it's hard to optimize already)

            if (turnAroundClockwise)
            {
                star.Reverse();
                indexInStar = star.Count - 1 - indexInStar;
            }

            var edgesToCross = star.GetRange(0, indexInStar);

            Variable currentEdgeCrossingPosition = null;

            if (!followingLeft && turnAroundClockwise || followingLeft && !turnAroundClockwise)
            {
                // in these cases we have to cross the edge that we just followed before continuing with the edge crossings

                currentEdgeCrossingPosition = new Variable($"Distance to the end of first edge crossing (half-turn) through {currentlyFollowedStrip}");
                variables.Add(currentEdgeCrossingPosition);
                
                var halfTurn = new CornerSegment(
                    currentlyFollowedStrip.Reversed(),
                    otherAxisInArrivingQuadrant,
                    currentEdgeCrossingPosition,
                    currentDistanceToFollowedStrip, // todo think about variable: could be chosen independent from the distance that the strip has at the beginning, but this would mean that we introduce self-intersections in the middle of the edge when permuting edge followings // the incoming distance might be modified
                    CornerSegment.SegmentType.halfTurn,
                    flip: followingLeft
                );

                cornerPoints.Add(halfTurn);

                edgesToCross.Insert(0, currentlyFollowedStrip.Reversed());
                // the crossing loop will cross this edge at distance currentEdgeCrossingPosition

                followingLeft = !followingLeft;
            }
            else
            {
                if (indexInStar == 0) // otherAxisInArrivingQuadrant == nextStrip
                {
                    
                    // var max = float.MinValue;
                    // foreach (var t in path)
                    // {
                    //     if (t is not EdgeFollowing otherEdgeFollowing)
                    //         continue; //  todo: also check for turns?
                    //     if (!otherEdgeFollowing.strip.UnderlyingEdge.Equals(nextStrip.UnderlyingEdge))
                    //         continue;
                    //     var otherEdgeFollowingAlignedDistance =
                    //         otherEdgeFollowing.AlignedDistance(nextStrip);
                    //     if (otherEdgeFollowingAlignedDistance < 0)
                    //         continue;
                    //     if (otherEdgeFollowingAlignedDistance > max)
                    //         max = otherEdgeFollowingAlignedDistance;
                    // }
                    //
                    // currentDistanceToFollowedStrip = MathF.Sign(currentDistanceToFollowedStrip) *
                    //                                  (max == float.MinValue ? 1f : max * 1.333333333333f);

                    currentEdgeCrossingPosition = new Variable($"Distance to edge {otherAxisInArrivingQuadrant.Name} when turning " + (followingLeft ? "left" : "right" ) + $" after following {currentlyFollowedStrip}");

                    var outerTurn = new CornerSegment(
                        currentlyFollowedStrip.Reversed(),
                        otherAxisInArrivingQuadrant,
                        currentEdgeCrossingPosition,
                        currentDistanceToFollowedStrip, // again, the incoming distance might be modified
                        CornerSegment.SegmentType
                            .xUpwards, // outerTurn not really because we add the outgoing thing extra at the end
                        flip: followingLeft
                    );
                    cornerPoints.Add(outerTurn);
                    nextStartY = currentDistanceToFollowedStrip; // also overwrites the variable reference
                }
                else
                {
                    // We run straight into the next edge
                    // TODO: Refactoring / Feature: To optimize over different values we should introduce "variables", i.e. instead of actual numbers we should give references to variables
                    var incomingSegment = new CornerSegment(
                        currentlyFollowedStrip.Reversed(),
                        otherAxisInArrivingQuadrant,
                        0,
                        currentDistanceToFollowedStrip, 
                        CornerSegment.SegmentType.xUpwards,
                        flip: followingLeft
                    );

                    cornerPoints.Add(incomingSegment);

                    currentEdgeCrossingPosition =
                        currentDistanceToFollowedStrip; // also overwrites the variable reference
                }
            }

            // crossing loop
            for (var i = 0; i < edgesToCross.Count; i++)
            {
                var edgeCrossed = edgesToCross[i];
                // this loop body starts right before the crossing of the edgeCrossed 

                var edgeCrossing = new EdgeCrossing(edgeCrossed, !turnAroundClockwise, currentEdgeCrossingPosition);

                path.Add(edgeCrossing);

                if (i == edgesToCross.Count - 1)
                {
                    nextStartY = 0f;
                    break; // breaks after continue as well   
                }

                var nextEdgeToCross = edgesToCross[i + 1];

                var nextEdgeCrossingPosition = new Variable($"Distance along edge {nextEdgeToCross.Name} where it is intersected as the {i + 1}th edge crossed between the {index}th strip {currentlyFollowedStrip.Name} and the {index+1}th strip {nextStrip.Name}"); // new Variable

                var innerTurn = new CornerSegment(
                    edgeCrossed,
                    nextEdgeToCross,
                    currentEdgeCrossingPosition,
                    nextEdgeCrossingPosition,
                    CornerSegment.SegmentType.innerTurn,
                    turnAroundClockwise
                );

                cornerPoints.Add(innerTurn);

                currentEdgeCrossingPosition = nextEdgeCrossingPosition;

            }

            var lastFollowedOrCrossedStrip = edgesToCross.Count > 0 ? edgesToCross[^1] : currentlyFollowedStrip;
            var outgoingSegment = new CornerSegment(
                lastFollowedOrCrossedStrip,
                nextStrip,
                currentEdgeCrossingPosition,
                nextStartY,
                CornerSegment.SegmentType.yUpwards, turnAroundClockwise);
            cornerPoints.Add(outgoingSegment);
        }

        return path;
    }

    // private void CalculateSelfIntersections() { 
    //
    //     path = new List<Entry>(pathWithoutSelfIntersections);
    //     
    //     if (!Concrete)
    //         Debug.LogWarning("Evaluating self-intersections of PushingPath with free variables!");
    //     var selfIntersections =
    //         new List<(SelfIntersectionSecondTime secondIntersectionTime, float sideDistance)>();
    //
    //     
    //     for (int i = 0; i < path.Count; i++)
    //     {
    //         var currentPoint = cornerList[i];
    //         for (int j = 0; j < i; j++)
    //         {
    //             var earlierPoint = cornerList[j];
    //             if (!earlierPoint.xAxis.Equals(currentPoint.xAxis))
    //                 continue;
    //             var otherEdgeFollowingAlignedStartDistance =
    //                 otherEdgeFollowing.AlignedStartDistance(edgeCrossed, out var otherEdgeFollowingIsIncoming);
    //
    //
    //             var otherEdgeY = (turnAroundClockwise ? -1 : 1) *
    //                              otherEdgeFollowing.AlignedDistance(edgeCrossed);
    //
    //             var x = (turnAroundClockwise ? -currentDistanceToFollowedStrip : currentDistanceToFollowedStrip);
    //             if (otherEdgeFollowingAlignedStartDistance > x)
    //                 continue;
    //             if (otherEdgeY >
    //                 currentStartDistanceToCrossedEdge) // we start closer to the crossed edge than the other edge is to it.
    //                 continue;
    //
    //             if (thereIsANextCrossing && -outgoingDistanceThis <= otherEdgeY)
    //             {
    //                 // todo: also check for turns
    //                 outgoingDistanceThis =
    //                     -0.75f *
    //                     otherEdgeY; // turn earlier so that we don't intersect it
    //             }
    //
    //             if (otherEdgeY <=
    //                 -outgoingDistanceThis) // we turn away closer to the crossed edge than the other edge is
    //                 continue;
    //             CornerSegment intersection = new CornerSegment(edgeCrossed, otherEdgeFollowing.strip, x,
    //                 otherEdgeY, (CornerSegment.SegmentType)0, followingLeft); // todo
    //             // todo: might also cross other turning segments, i.e. consecutive (not separated by EdgeFollowings) EdgeCrossings
    //             var secondIntersectionTime = new SelfIntersectionSecondTime(intersection);
    //             selfIntersections.Add((secondIntersectionTime, otherEdgeY));
    //             var firstIntersectionTime = new SelfIntersection(
    //                 otherEdgeFollowingIsIncoming == turnAroundClockwise,
    //                 secondIntersectionTime,
    //                 intersection
    //             );
    //             path.Insert(otherEdgeFollowingIsIncoming ? i + 1 : i, firstIntersectionTime);
    //         }
    //     }
    //         
    //     var intersectionsBeforeTheCrossing =
    //         selfIntersections.Where(
    //             t => t.sideDistance > 0
    //         ).OrderByDescending(
    //             t => t.sideDistance
    //         ).Select(
    //             t => t.secondIntersectionTime
    //         );
    //     path.AddRange(intersectionsBeforeTheCrossing);
    //
    //
    //     var intersectionsAfterTheCrossing =
    //         selfIntersections.Where(
    //             t => t.sideDistance < 0
    //         ).OrderByDescending(
    //             t => t.sideDistance
    //         ).Select(
    //             t => t.secondIntersectionTime
    //         ); // this is empty because we choose our distance after the crossing so small
    //     path.AddRange(intersectionsAfterTheCrossing);
    //
    // }

    private EdgePath ConjugationPath(int startTime)
    {
        return EdgePath.Concatenate(path.Skip(startTime).Select(p => p.AssociatedPath(this)));
    }

    public EdgePath Image(UnorientedStrip strip)
    {
        if (!Concrete)
            Debug.LogWarning("Calculating the graph map of a point push with free variables!");
        
        return EdgePath.Concatenate(
                SortedConjugationPathsInEdge(strip, false)
                    .Append(new NormalEdgePath(strip))
                    .Concat(SortedConjugationPathsInEdge(strip.Reversed(), true)
            )
        );

        IEnumerable<EdgePath> SortedConjugationPathsInEdge(Strip edge, bool reverse) =>
            path
                .Enumerate()
                .Where(tuple =>
                    tuple.t is EdgeCrossing crossing && Equals(crossing.crossedEdge, edge)
                ).OrderBy(
                    tuple => ((EdgeCrossing) tuple.t).positionAlongEdge.Value * (reverse ? -1 : 1))
                .Select(tuple => 
                        (((EdgeCrossing) tuple.t).rightToLeft ? punctureWord : punctureWord.Inverse())
                        .Conjugate(ConjugationPath(tuple.i), true)
                    );
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

    public IPatchedDrawnsformable Copy() => new PushingPath(path, edgePath, cornerList, variables) { Name = Name, Color = Color } ; // todo: create new variables and new lists, this is not a deep copy!

    public IEnumerable<IDrawnsformable> Patches => from cornerSegment in cornerList select cornerSegment.ToDrawnsformable();

    public override string ToString() => path.ToCommaSeparatedString(" ");
}
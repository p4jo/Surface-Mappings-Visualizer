using System;
using System.Collections.Generic;
using System.Linq;

public partial class FibredSurface
{

    private const double tau = Math.PI * 2;
    public void ConvertToTrainTrack()
    {
        var gates = Gate.FindGates(graph);
        var gatesAtJunctions = gates.GroupBy(gate => gate.junctionIdentifier).ToDictionary(
            g => g.Key, 
            g => g.OrderBy(gate => gate.Edges.First().OrderIndexStart).ToList()
        );
        var edgeGates = new Dictionary<Strip, Gate<Junction>>(gates.SelectMany(gate =>
            from edge in gate.Edges select new KeyValuePair<Strip, Gate<Junction>>(edge, gate)));
        var gateVectors = new Dictionary<Gate<Junction>, TangentVector>(gates.Count);
        var gateJunctions = new Dictionary<Gate<Junction>, Junction>(gates.Count);
        var gateImages = new Dictionary<Gate<Junction>, Gate<Junction>>(gates.Count);
        var edgeTimes = new Dictionary<Strip, float>(edgeGates.Count);
        var edgeTimes2 = new Dictionary<Strip, float>(edgeGates.Count);
        
        
        foreach (var junction in graph.Vertices.ToArray())
        {
            var star = StarOrdered(junction).ToList();
            var gatesHere = gatesAtJunctions[junction];
            const float distance = 0.2f;
            foreach (var gate in gatesHere)
            {
                foreach (var strip in gate.Edges)
                {
                    var timeFromArclength = GeodesicSurface.TimeFromArclength(strip.Curve.Restrict(0, MathF.Min(strip.Curve.Length, 10f)));
                    edgeTimes[strip] = MathF.Min(strip.Curve.Length, timeFromArclength(distance));
                    edgeTimes2[strip] = MathF.Min(strip.Curve.Length,timeFromArclength(2 * distance));
                }
                var gatePosition = (
                    from edge in gate.Edges
                    select edge.Curve[edgeTimes[edge]].Position 
                ).Average();
                Point gatePoint = gatePosition;
                var gateVector = new TangentVector(gatePosition, gatePosition - gate.junctionIdentifier.Position.Position);
                gateVectors[gate] = gateVector;
                foreach (var edge in gate.Edges) 
                    edge.Curve = edge.Curve.AdjustStartVector(gateVector, edgeTimes2[edge]);

                var gateJunction = new Junction(this, gatePoint,
                    $"gate {{{gate.Edges.Select(e => e.Name).ToCommaSeparatedString()}}} @ {junction.Name}", color: gate.junctionIdentifier.Color);
                gateJunctions[gate] = gateJunction;
                foreach (var (index, strip) in SortConnectedSet(star, gate.Edges).Enumerate())
                {
                    strip.Source = gateJunction; // this adds the new junctions to the graph
                    strip.OrderIndexStart = index;
                }
                
            }
            graph.RemoveVertex(junction); // junction is replaced by the gates and infinitesimal edges
        }

        foreach (var gate in gates)
        {
            var gateJunction = gateJunctions[gate];
            gateImages[gate] = edgeGates[gate.Edges.First().Dg!];
            gateJunction.image = gateJunctions[gateImages[gate]]; 
            // the gate of the edge e.Dg is independent of the choice of e in gate.Edges, by construction of the gates
        }

        var infinitesimalEdges = new Dictionary<(Gate<Junction>, Gate<Junction>), Strip>();
        
        foreach (var strip in Strips.ToArray())
        {
            var newEdgePath = new List<Strip>();
            var edgePath = strip.EdgePath.ToList();
            newEdgePath.Add(edgePath[0]);
            for (int i = 0; i < edgePath.Count - 1; i++)
            {
                var edge = edgePath[i];
                var nextEdge = edgePath[i + 1];
                var incomingGate = edgeGates[edge.Reversed()];
                var outgoingGate = edgeGates[nextEdge];
                if (incomingGate == outgoingGate) 
                    throw new ArgumentException("The fibred surface is not efficient and cannot be converted to a train track.");
                if (!infinitesimalEdges.TryGetValue((incomingGate, outgoingGate), out var infinitesimalEdge))
                    // create infinitesimal edges that are "strongly needed", i.e. come from pairs of gates
                    // that appear in succession in g(e) for some edge e.  
                    infinitesimalEdge = NewInfinitesimalEdge(incomingGate, outgoingGate);
                    
                newEdgePath.Add(infinitesimalEdge);
                newEdgePath.Add(nextEdge);
            }
            strip.EdgePath = new NormalEdgePath(newEdgePath); // yes, this destroys the bracketing structure
        }
        
        var infinitesimalEdgesToAssign = new Queue<KeyValuePair<(Gate<Junction>, Gate<Junction>), Strip>>(
            infinitesimalEdges.Where(kvp => kvp.Value is UnorientedStrip)
        );
        while (infinitesimalEdgesToAssign.TryDequeue(out var kvp))
        {
            var ((sourceGate, targetGate), infinitesimalEdge) = kvp;
            
            if (!infinitesimalEdges.TryGetValue((gateImages[sourceGate], gateImages[targetGate]),
                    out var imageInfinitesimalEdge))
            {
                // Lemma: An infinitesimal edge, given by a pair of gates (γ, δ), is needed (i.e. appears in succession in some g^k(e) for some edge e) if and only if g^k(γ') = γ and g^k(δ') = δ for some pair (γ', δ') that is strongly needed (i.e. created already above). 
                // This justifies the late creation of infinitesimal edges here - just when needed as images of inf. edges we already created.
                imageInfinitesimalEdge = NewInfinitesimalEdge(gateImages[sourceGate], gateImages[targetGate]);
                infinitesimalEdgesToAssign.Enqueue(
                    new KeyValuePair<(Gate<Junction>, Gate<Junction>), Strip>(
                        (gateImages[sourceGate], gateImages[targetGate]), imageInfinitesimalEdge
                    )
                );
            }

            Strip[] strips = new[] { imageInfinitesimalEdge };
            infinitesimalEdge.EdgePath = new NormalEdgePath(strips);
        }

        UnorientedStrip NewInfinitesimalEdge(Gate<Junction> incomingGate, Gate<Junction> outgoingGate)
        {
            var gatesHere = gatesAtJunctions[incomingGate.junctionIdentifier];
            int outGateIndex = gatesHere.IndexOf(outgoingGate);
            int inGateIndex = gatesHere.IndexOf(incomingGate);
            var incomingGateOrderIndexInStar = incomingGate.Edges.Count + (outGateIndex > inGateIndex ? outGateIndex : outGateIndex + gatesHere.Count);
            var outgoingGateOrderIndexInStar = outgoingGate.Edges.Count + (inGateIndex > outGateIndex ? inGateIndex : inGateIndex + gatesHere.Count);
            // var scale = surface.Distance(gateVectors[incomingGate].point, gateVectors[outgoingGate].point) * 0.2f;
            var newInfinitesimalEdge = new UnorientedStrip(
                new SplineSegment(
                    - gateVectors[incomingGate], // -scale * gateVectors[incomingGate].Normalized,
                    gateVectors[outgoingGate], // scale * gateVectors[outgoingGate].Normalized,
                    1.6f, // ,
                    surface,
                    NextEdgeNameGreek()
                ),
                source: gateJunctions[incomingGate],
                target: gateJunctions[outgoingGate],
                edgePath: EdgePath.Empty, // we can only add these once we created them all
                fibredSurface: this, 
                orderIndexStart: incomingGateOrderIndexInStar,
                orderIndexEnd: outgoingGateOrderIndexInStar,
                addToGraph: true
            );
            infinitesimalEdges[(incomingGate, outgoingGate)] = newInfinitesimalEdge;
            infinitesimalEdges[(outgoingGate, incomingGate)] = newInfinitesimalEdge.Reversed();
            
            return newInfinitesimalEdge;
        }
    }

}
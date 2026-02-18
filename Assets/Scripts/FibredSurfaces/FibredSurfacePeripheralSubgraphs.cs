using System.Collections.Generic;
using System.Linq;

public partial class FibredSurface
{
    private List<HashSet<UnorientedStrip>> PrePeripheralDegrees()
    {
        var degrees = new List<HashSet<UnorientedStrip>> { peripheralSubgraph.ToHashSet() }; // make copy
        var remainingEdges = Strips.ToHashSet();
        remainingEdges.ExceptWith(degrees[0]);
        for (int i = 1; i <= 2 * graph.EdgeCount; i++) // should never reach this limit
        {
            var P_i = (
                from strip in remainingEdges
                where strip.EdgePath.All(
                    edge => !remainingEdges.Contains(edge.UnderlyingEdge) // i.e. contained in some lower P_j
                )
                select strip).ToHashSet();
            if (P_i.Count == 0) break;
            degrees.Add(P_i);
            remainingEdges.ExceptWith(P_i);
            if (remainingEdges.Count == 0) break;
        }

        return degrees;
    }

    /// <summary>
    /// This is the subgraph P u pre-P = P0 u P1 u ... that consists of all edges that eventually get mapped into the periphery P.
    /// </summary>
    /// <returns></returns>
    public HashSet<UnorientedStrip> PrePeriphery()
    {
        var prePeripheralDegrees = PrePeripheralDegrees();
        var prePeriphery = new HashSet<UnorientedStrip>();
        foreach (var degree in prePeripheralDegrees)
            prePeriphery.UnionWith(degree);
        return prePeriphery;
    }

    /// <summary>
    /// This is the subgraph H that contains all the edges that are not in the pre-periphery.
    /// These are the edges that will not be "infinitesimal" as determined by the eigenvector defining lengths.
    /// </summary>
    /// <returns></returns>
    public HashSet<UnorientedStrip> EssentialSubgraph()
    {
        var edges = Strips.ToHashSet();
        edges.ExceptWith(PrePeriphery());
        return edges;
    }

}
using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using UnityEngine;

public class EdgeCycle
{
    public int order;
    /// <summary>
    /// The first order elements are the edges in the cycle.
    /// </summary>
    public List<(Strip, int)> attractedEdges = new();

    public EdgeCycle(Strip edge, int order)
    {
        this.order = order;
        attractedEdges.Add((edge, 0)); // this is the basepoint of the cycle
        for (int i = order - 1; i > 0; i--)
        {
            edge = edge?.Dg;
            attractedEdges.Add((edge, i));
        }
    }

    /// <summary>
    /// Checks if the edge is known to be attracted into the cycle and returns the distance to the basepoint of the cycle (or -1 if it is not in the cycle).
    /// </summary>
    public int CycleIndexOf(Strip edge)
    {
        try
        {
            return attractedEdges.First(stripDistancePair => Equals(stripDistancePair.Item1, edge)).Item2;
        }
        catch
        {
            return -1;
        }
    }
    
    /// <summary>
    /// The edgeCycle.attratcedEdges (for edgeCycle in edgeCycles) forms a partition of the set of oriented edges.  
    /// </summary>
    public static List<EdgeCycle> FindEdgeCycles(IReadOnlyCollection<Strip> edges)
    {        
        List<EdgeCycle> edgeCycles = new();

        foreach (var edge in edges.Concat<Strip>(edges.Select(e => e.Reversed())))
        {
            Strip e_i = edge;
            List<Strip> orbit = new();
            for (int i = 0; i <= 2 * edges.Count; i++)
                // it should never break because of the upper limit because after at most #E elements the orbit must repeat.
            {

                var (edgeCycle, l) = Dg_infinity(e_i);
                if (l >= 0)
                {
                    for (int m = 0; m < i; m++)
                        if (edgeCycle.CycleIndexOf(orbit[m]) == -1)
                            edgeCycle.attractedEdges.Add((orbit[m], l + i - m));
                        else
                            Debug.LogWarning("This should not happen. An edge is already in an edge cycle, but isn't?");
                    break;
                }

                int j = i;
                if (e_i == null)
                {
                    // create edgeCycle to which all edges attract that eventually map to null under Dg (Dg∞ = null, with distance as appropriate)
                     edgeCycle = new EdgeCycle(null, 1);
                }
                else
                {
                    // look for repetition in the orbit
                    j = orbit.IndexOf(e_i); 
                    if (j < 0) {
                        orbit.Add(e_i);
                        e_i = e_i.Dg;
                        continue;
                    }
                    edgeCycle = new EdgeCycle(e_i, i - j); 
                }
                edgeCycles.Add(edgeCycle); // this assigns the Dg∞ to the cycle

                for (int m = 0; m < j; m++)
                    if (edgeCycle.CycleIndexOf(orbit[m]) == -1)
                        edgeCycle.attractedEdges.Add((orbit[m], j - m));
                    else
                        Debug.LogWarning("This should not happen. An edge is already in an edge cycle, but isn't?");

                break;
            }
        }
        return edgeCycles; 
        

        (EdgeCycle, int) Dg_infinity(Strip edge)
        {
            
            foreach (var edgeCycle in edgeCycles)
            {
                int l = edgeCycle.CycleIndexOf(edge);
                if (l < 0) continue;
                return (edgeCycle, l);
            }

            return (null, -1);
        }
    }

} 
public class Gate<T>
{
    public readonly T junctionIdentifier;
    /// <summary>
    /// The edges in the star of the vertex that form the gate, i.e. that get mapped to the same edge under Dg^k for some k.
    /// </summary>
    public readonly List<Strip> Edges;

    public readonly int cycleDistance;

    public Gate(Strip edge, int cycleDistance, T junctionIdentifier)
    {
        this.junctionIdentifier = junctionIdentifier;
        Edges = new List<Strip> {edge};
        this.cycleDistance = cycleDistance;
    }

    public override string ToString() => $"Gate {{{string.Join(", ", Edges.Select(e => e.Name))}}} ⊆ Star({junctionIdentifier})";
}

public static class Gate
{
    public static List<Gate<Junction>> FindGates(UndirectedGraph<Junction, UnorientedStrip> graph) => FindGates(graph.Edges.ToList(), junction => junction);

    
    public static List<Gate<T>> FindGates<T>(IReadOnlyCollection<Strip> edges, Func<Junction, T> identifier) where T : IEquatable<T>
    {
        List<EdgeCycle> edgeCycles = EdgeCycle.FindEdgeCycles(edges);
        List<Gate<T>> gates = new();
        foreach (var edgeCycle in edgeCycles)
        {
            var gatesFromPreviousEdgeCycles = gates.Count;
            foreach (var (edge, cycleDistance) in edgeCycle.attractedEdges)
            {
                var gate = gates.Skip(gatesFromPreviousEdgeCycles).FirstOrDefault( gate =>
                    identifier(edge.Source).Equals(gate.junctionIdentifier) &&
                    (cycleDistance - gate.cycleDistance) % edgeCycle.order == 0
                );
                if (gate == null)
                    gates.Add(new Gate<T>(edge, cycleDistance, identifier(edge.Source)));
                else
                    gate.Edges.Add(edge);
            }
        }
        return gates;
    }
}
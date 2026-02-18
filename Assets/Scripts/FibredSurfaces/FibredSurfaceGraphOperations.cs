using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public partial class FibredSurface
{
    public string GraphString()
    {
        var vertexString = graph.Vertices.ToLineSeparatedString(vertex => vertex.ToColorfulString(Gate.FindGates(graph)));
        var stripsOrdered = StripsOrdered.ToList();
        var edgeString = stripsOrdered.ToLineSeparatedString(edge => edge.ToColorfulString());
        var boundaryWords = BoundaryWords().ToHashSet();
        var peripheralBoundaryWords = boundaryWords.Where(w => 
            w.All(e => peripheralSubgraph.Contains(e.UnderlyingEdge))
        ).ToHashSet();
        boundaryWords.ExceptWith(peripheralBoundaryWords);
        var boundaryWordsText = new List<string> {
            boundaryWords.ToCommaSeparatedString(w => w.ToColorfulString(180, 10)),
            peripheralBoundaryWords.ToCommaSeparatedString(w => w.ToColorfulString(180, 10))
        }.ToCommaSeparatedString("\nPeripheral: ");
        FrobeniusPerron(false, out var growthRate, out var widths, out var lengths, out var matrix);
        // var matrix = TransitionMatrix();

        var VariableString = "";
        var variables = UsedNamedEdgePaths.Where(v => char.IsLower(v.name.First(char.IsLetter))).ToList();
        if (variables.Count > 0)
        {
            VariableString = "\nVariables: " + variables.ToCommaSeparatedString(v => v.name + " = " + v.value.ToColorfulString(180, 10), "\n");
        }
        stripsOrdered.Add(null); // for the "width" column and "length" row
        
        string graphString = $"<line-indent=-20>{vertexString}\n{edgeString}\nBoundary / puncture words (following to the right):\n{boundaryWordsText}{VariableString}\n{MatrixStrings().ToCommaSeparatedString("\n")}\nGrowth rate: {growthRate:g3}";

        if (ignoreBeingReducible)
            graphString += "\nType: Reducible";
        else if (isTrainTrack)
            if (growthRate > 1 + 1e-6)
                graphString += "\nType: Pseudo-Anosov";
            else
                graphString += "\nType: Finite-Order";

        return graphString;

        IEnumerable<string> MatrixStrings()
        {
            const int maxColumns = 10;
            var totalColumns = stripsOrdered.Count;
            var tables = Mathf.CeilToInt(totalColumns / (float) maxColumns);
            int columnsFirstTables = maxColumns;
            int columnsLastTable = totalColumns - (tables - 1) * maxColumns;
            while (columnsFirstTables >= columnsLastTable + tables)
            {
                columnsFirstTables--;
                columnsLastTable += tables - 1;
            }

            for (int i = 0; i < tables; i++)
            {
                var columns = i == tables - 1 ? columnsLastTable : columnsFirstTables;
                var scale = 100f / (columns + 1);
                var startIndex = i * columnsFirstTables;
                var matrixHeader = stripsOrdered.GetRange(startIndex, columns).Select((edge, index) => 
                    $"<pos={scale * (index + 1):00}%>{Header(edge, "width")}"
                ).ToCommaSeparatedString("");
                    
                var matrixString = stripsOrdered.Select(edge =>
                    Header(edge, "length") + ": " + 
                    stripsOrdered.GetRange(startIndex, columns).Select((strip, index) =>
                        $"<pos={scale * (index + 1):00}%>{MatrixEntryGreyedOut(edge, strip)}"     
                    ).ToCommaSeparatedString("")
                ).ToCommaSeparatedString("\n");
                yield return matrixHeader + "\n" + matrixString;
            }
            yield break;
            
            string Header(IDrawable e, string nullValue) => e == null ? nullValue : e.ColorfulName;
            string MatrixEntry(UnorientedStrip edge, UnorientedStrip strip) // edge is counted downwards, strip towards the right
            {
                if (edge == null && strip == null) return "";
                if (edge == null) return lengths[strip].ToShortString();
                if (strip == null) return widths[edge].ToShortString();
                return matrix[edge][strip].ToShortString();
            }

            string MatrixEntryGreyedOut(UnorientedStrip edge, UnorientedStrip strip)
            {
                var res = MatrixEntry(edge, strip);
                return res == "0" ? "<color=grey>0</color>" : res;
            }
        }
    }

    public static IEnumerable<Strip> Star(Junction junction, FibredGraph graph = null)
    {
        return (graph ?? junction.fibredSurface.graph).AdjacentEdges(junction).SelectMany(strip =>
            strip.IsSelfEdge() ? new[] { strip, strip.Reversed() } :
            strip.Source != junction ? new[] { strip.Reversed() } :
            new[] { strip });
    }
    /// <summary>
    /// This is the cyclic order of the star of the given junction.
    /// If firstEdge is not null, the order is shifted to start with firstEdge.
    /// </summary>
    public static IEnumerable<Strip> StarOrdered(Junction junction, Strip firstEdge = null, FibredGraph graph = null, bool removeFirstEdgeIfProvided = false)
    {
        IEnumerable<Strip> orderedStar = Star(junction, graph).OrderBy(strip => strip.OrderIndexStart);
        if (firstEdge == null) 
            return orderedStar;

        orderedStar = orderedStar.CyclicShift(firstEdge);
        return removeFirstEdgeIfProvided ? orderedStar.Skip(1) : orderedStar;
    }

    public IEnumerable<Strip> SubgraphStar(FibredGraph subgraph) =>
        from edge in OrientedEdges
        where subgraph.ContainsVertex(edge.Source)
        where !subgraph.Edges.Contains(edge.UnderlyingEdge)
        select edge;

    public IEnumerable<Strip> SubgraphStarOrdered(FibredGraph subgraph)
    {
        if (subgraph?.VertexCount == 0) yield break;
        var vertex = subgraph!.Vertices.First();
        var star = StarOrdered(vertex).GetEnumerator();
        Strip firstEdge = null;
        var i = 0; // just to stop infinite loop

        while (star.MoveNext()) // this should never break
        {
            var edge = star.Current;
            if (Equals(firstEdge, edge)) break;
            firstEdge ??= edge;
            if (subgraph.Edges.Contains(edge!.UnderlyingEdge))
            {
                vertex = edge.Target;
                star.Dispose();
                star = StarOrdered(vertex, edge.Reversed()).CyclicShift(1).GetEnumerator();
                continue;
            }

            yield return edge;
            if (i++ > graph.EdgeCount)
                HandleInconsistentBehavior("The star of the subgraph didn't loop correctly. Stopped infinite loop");
        }

        star.Dispose();
    }

    /// <summary>
    /// Returns the list (e, σ(e), σ²(e),..., σ^k(e)) where σ is the cyclic order of the star (i.e. the cyclic successor map of
    /// the given list), k is the number of elements in connectedSet and e is an element of connectedSet that is not the successor
    /// of another element of connectedSet (unless k = #star).
    /// So, if connectedSet is not actually connected, then the result will not set-equal to connectedSet.
    /// </summary>
    private IEnumerable<T> SortConnectedSet<T>(List<T> sortedStar, ICollection<T> connectedSet)
    {
        int startIndex = sortedStar.FindIndex(connectedSet.Contains);
        // this should be the index in the cyclic order where the connected set starts
        if (startIndex == -1) throw new InvalidOperationException("The connected set is not in the star.");
        if (startIndex == 0) startIndex = sortedStar.FindLastIndex(e => !connectedSet.Contains(e)) + 1;
        // if the connected set is all of the star, then this is -1 + 1 = 0.
        return sortedStar.CyclicShift(startIndex).Take(connectedSet.Count);
    }
    private IEnumerable<T> SortConnectedSet<T>(List<T> sortedStar, ICollection<T> connectedSet, out int startIndex)
    {
        startIndex = sortedStar.FindIndex(connectedSet.Contains);
        // this should be the index in the cyclic order where the connected set starts
        if (startIndex == -1) throw new InvalidOperationException("The connected set is not in the star.");
        if (startIndex == 0) startIndex = sortedStar.FindLastIndex(e => !connectedSet.Contains(e)) + 1;
        // if the connected set is all of the star, then this is -1 + 1 = 0.
        return sortedStar.CyclicShift(startIndex).Take(connectedSet.Count);
    }
    
    bool IsConnectedSet<T>(List<T> sortedStar, ICollection<T> connectedSet) => 
        SortConnectedSet(sortedStar, connectedSet).All(connectedSet.Contains);

    /// <summary>
    /// The smallest invariant subgraph containing this edge.
    /// </summary>
    private static FibredGraph OrbitOfEdgeGraph(Strip edge)
    {
        var orbit = OrbitOfEdge(edge);

        FibredGraph newGraph = new FibredGraph();
        newGraph.AddVerticesAndEdgeRange(orbit);
        return newGraph;
    }

    private static HashSet<UnorientedStrip> OrbitOfEdge(Strip edge, HashSet<UnorientedStrip> edgesKnownToHaveFullOrbit = null)
    {
        HashSet<UnorientedStrip> orbit = new() { edge.UnderlyingEdge };
        Queue<Strip> queue = new(orbit);
        while (queue.TryDequeue(out edge))
        {
            if (edgesKnownToHaveFullOrbit != null && edgesKnownToHaveFullOrbit.Contains(edge.UnderlyingEdge))
                return edge.graph.Edges.ToHashSet();
            foreach (var e in edge.EdgePath)
            {
                if (orbit.Add(e.UnderlyingEdge)) // add returns false if the edge is already in the orbit
                    queue.Enqueue(e.UnderlyingEdge);
            }
        }

        return orbit;
    }
    
    /// <summary>
    /// The boundary words, following at the right. I.e. the orbits of the map e -> σ(e.Reversed()), where σ is the cyclic order that goes counter-clockwise. 
    /// </summary>
    public IEnumerable<EdgePath> BoundaryWords()
    {
        var visited = new HashSet<Strip>();
        var stars = new Dictionary<Junction, List<Strip>>(
            from vertex in graph.Vertices
            select new KeyValuePair<Junction, List<Strip>>(vertex, StarOrdered(vertex).ToList())
        );
        foreach (var strip in OrientedEdges)
        {
            if (visited.Contains(strip))
                continue;

            var boundaryWord = BoundaryWord(strip, stars);
            visited.UnionWith(boundaryWord);
            yield return boundaryWord;
        }
        
    }
    /// <summary>
    /// The boundary word starting with strip, following at the right. I.e. the orbit of the map e -> σ(e.Reversed()), where σ is the cyclic order that goes counter-clockwise. 
    /// </summary>
    public static EdgePath BoundaryWord(Strip strip)
    {
        var stars = new Dictionary<Junction, List<Strip>>(
            from vertex in strip.graph.Vertices
            select new KeyValuePair<Junction, List<Strip>>(vertex, StarOrdered(vertex).ToList())
        );
        return BoundaryWord(strip, stars);
    }
    

    private static NormalEdgePath BoundaryWord(Strip strip, IReadOnlyDictionary<Junction, List<Strip>> stars)
    {
        var boundaryWord = new List<Strip>();
        Strip edge = strip;
        do
        {
            boundaryWord.Add(edge);
            edge = NextEdge(edge);
        } while (!Equals(strip, edge));

        return new NormalEdgePath(boundaryWord);

        Strip NextEdge(Strip edge)
        {
            var star = stars[edge.Target];
            int index = star.IndexOf(edge.Reversed());
            return star[(index + 1) % star.Count];
        }
    }

}
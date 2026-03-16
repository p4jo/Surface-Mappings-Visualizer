using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using MathNet.Numerics.LinearAlgebra;
using QuikGraph.Algorithms;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public partial class FibredSurface
{

    /// <summary>
    /// Tests if there is a preserved subgraph that is not contained in the (pre-)periphery.
    /// If there is such a subgraph and it is not a forest in G/P (clear at the point in the algorithm where this test is done), then the map is reducible:
    /// The boundary components of the preserved subgraph are preserved up to isotopy and at least one of them is essential, thus determining a reduction in the sense of the Thurston-Nielsen classification.  
    /// 
    /// If not, the transition matrix M is irreducible*. 
    /// The "growth" of the graph map is the spectral radius (Frobenius-Perron eigenvalue) and the associated positive eigenvector is interpreted as the widths of the edges (in the train track sense).
    /// (*) it is not necessarily irreducible, as P u pre-P is a preserved subgraph, but it defines positive weights on the edges (this is a condition on the block of M mapping G\P to P)
    /// </summary>
    /// <returns> A non-trivial essential preserved subgraph or null if it doesn't exist, i.e. the transition matrix is irreducible. </returns>
    public IEnumerable<HashSet<UnorientedStrip>> PreservedSubgraphsReducibility()
    {
        var prePeriphery = PrePeriphery();
        var fullGraph = Strips.ToHashSet();
        var essentialSubgraph = new HashSet<UnorientedStrip>(fullGraph);
        essentialSubgraph.ExceptWith(prePeriphery);
        var knownOrbits = new Dictionary<UnorientedStrip, HashSet<UnorientedStrip>>();

        foreach (var edge in essentialSubgraph)
        {   
            var orbit = OrbitOfEdge(edge, knownOrbits);
            
            if (!orbit.IsSupersetOf(fullGraph))  
                yield return orbit;
            
        }
    }

    public AlgorithmSuggestion ReducibilitySuggestion()
    {
        var preservedSubgraphs = PreservedSubgraphsReducibility();
        
        // Group preserved subgraphs by their maximal invariant subgraph that deformation retracts to them
        var maximalInvariantSubgraphGroups = new Dictionary<string, (HashSet<UnorientedStrip> maximalSubgraph, HashSet<UnorientedStrip> minimalPreservedSubgraph)>();
        
        foreach (var preservedSubgraph in preservedSubgraphs)
        {
            var maximalInvariantSubgraph = GetMaximalInvariantSubgraphDeformationRetractingTo(preservedSubgraph);
            var maximalEdges = maximalInvariantSubgraph.Edges.ToHashSet();
            if (maximalInvariantSubgraph.IsUndirectedAcyclicGraph())
            {
                int counter = 0;
                while (true)
                {
                    var previousEdgeCount = maximalEdges.Count;
                    var touchedPeripheralEdges = peripheralSubgraph
                        .Where(e => maximalInvariantSubgraph.Vertices.Contains(e.Source) || maximalInvariantSubgraph.Vertices.Contains(e.Target));
                    maximalEdges.UnionWith(touchedPeripheralEdges);
                    if (previousEdgeCount == maximalEdges.Count)
                        break;
                    if (counter++ > peripheralSubgraph.Count)
                    {
                        HandleInconsistentBehavior("Caught in a loop when adding the touched peripheral edges to the maximal invariant subgraph. This should not happen.");
                        break;
                    }
                }

                if (maximalEdges.Count == graph.EdgeCount)
                    continue; // can only happen if g is a graph automorphism
            }
            
            // Create a unique key for this maximal invariant subgraph
            var key = string.Join(",", maximalEdges.Select(e => e.Name).OrderBy(n => n));
            
            if (!maximalInvariantSubgraphGroups.ContainsKey(key) || 
                preservedSubgraph.Count < maximalInvariantSubgraphGroups[key].minimalPreservedSubgraph.Count)
            {
                maximalInvariantSubgraphGroups[key] = (maximalEdges, preservedSubgraph);
            }
        }
        
        var options = new List<(object, string)>{ };
        
        foreach (var (maximalSubgraph, minimalPreservedSubgraph) in maximalInvariantSubgraphGroups.Values)
        {
            var (boundaryWordText, arePreserved) = CheckIfBoundaryWordsOfSubgraphArePreserved(minimalPreservedSubgraph);
            if (!arePreserved)
                HandleInconsistentBehavior($"The boundary words of the invariant subgraph {minimalPreservedSubgraph.ToCommaSeparatedString(e => e.ColorfulName)} are not preserved!");
            
            // Display the minimal preserved subgraph and the maximal invariant subgraph
            var preservedSubgraphText = minimalPreservedSubgraph.Select(e =>
                peripheralSubgraph.Contains(e)
                    ? e.ColorfulName
                    : $"<b>{e.ColorfulName}</b>"
            ).ToCommaSeparatedString();
            
            var maximalSubgraphText = maximalSubgraph.Select(e =>
                minimalPreservedSubgraph.Contains(e)
                    ? (peripheralSubgraph.Contains(e) ? e.ColorfulName : $"<b>{e.ColorfulName}</b>")
                    : $"<i>{e.ColorfulName}</i>"
            ).ToCommaSeparatedString();
            
            var displayText = preservedSubgraphText;
            if (!maximalSubgraph.SetEquals(minimalPreservedSubgraph))
            {
                displayText += $"\n  (maximal invariant subgraph: {maximalSubgraphText})";
            }
            displayText += $".\n {boundaryWordText}";
            
            options.Add((
                minimalPreservedSubgraph.Select(e => e.Name).ToArray(),
                displayText
            ));
        }
        
        if (options.Count == 0) return null;

        return new AlgorithmSuggestion(
            options: options,
            description: "The map is reducible because there are invariant essential subgraphs:", 
            buttons: new[]
            {
                AlgorithmSuggestion.ignoreReducibleButton, 
                AlgorithmSuggestion.reductionKeepInnerButton, 
                AlgorithmSuggestion.reductionKeepOuterButton
            }
        );
    }

    /// <summary>
    /// Prepares the reduction by collapsing the maximal invariant subgraph deformation retracting to each preserved subgraph component to it, and checking that the boundary words of each preserved subgraph component are preserved.
    /// The preservedSubgraph that is returned 
    /// </summary>
    /// <param name="preservedSubgraphEdges"></param>
    /// <returns></returns>
    private (IEnumerable<AlgorithmSuggestion>, FibredGraph preservedSubgraph) PrepareReduction(
        HashSet<string> preservedSubgraphEdges)
    {
        var preservedSubgraph = new FibredGraph();
        return (PrepareReductionInternal(preservedSubgraphEdges, preservedSubgraph), preservedSubgraph);
    }
    
    private IEnumerable<AlgorithmSuggestion> PrepareReductionInternal(HashSet<string> preservedSubgraphEdges, FibredGraph preservedSubgraph)
    {
        preservedSubgraph.AddVerticesAndEdgeRange(Strips.Where(e => preservedSubgraphEdges.Contains(e.Name)));
        var preservedSubgraphComponents = preservedSubgraph.ComponentGraphs(out var componentDict);

        // Induced map on preserved-subgraph components: source component -> target component
        var componentImage = Enumerable.Repeat(-1, preservedSubgraphComponents.Count).ToArray();
        foreach (var (vertex, sourceComponentIndex) in componentDict)
        {
            if (vertex.image == null || !componentDict.TryGetValue(vertex.image, out var targetComponentIndex))
            {
                HandleInconsistentBehavior(
                    $"The vertex {vertex.ColorfulName} of the preserved subgraph maps outside the preserved subgraph. This should not happen.");
                continue;
            }

            if (componentImage[sourceComponentIndex] == -1)
            {
                componentImage[sourceComponentIndex] = targetComponentIndex;
            }
            else if (componentImage[sourceComponentIndex] != targetComponentIndex)
            {
                HandleInconsistentBehavior(
                    $"The preserved component {sourceComponentIndex} maps to multiple components ({componentImage[sourceComponentIndex]} and {targetComponentIndex}). This should not happen.");
            }
        }

        for (int componentIndex = 0; componentIndex < preservedSubgraphComponents.Count; componentIndex++)
        {
            var component = preservedSubgraphComponents[componentIndex];
            if (component.EdgeCount == 0) continue;

            // Omit sinks for the reverse dynamics (no component maps into them), but flag as inconsistent.
            if (componentImage[componentIndex] != -1) continue;
            
            HandleInconsistentBehavior(
                $"No preserved-subgraph component maps into component {{{component.Edges.ToCommaSeparatedString(e => e.ColorfulName)}}}. This should not happen. Removing this component.");
            preservedSubgraph.RemoveVertexIf(v => componentDict[v] == componentIndex);
        }

        var (boundaryWordText, arePreserved) = CheckIfBoundaryWordsOfSubgraphArePreserved(preservedSubgraph.Edges);
        if (!arePreserved)
            HandleInconsistentBehavior(boundaryWordText);

        // Collapse the trees hanging off of it, i.e. calculate the maximal invariant subgraph deformation retracting to it,
        // and collapse the subforest formed by deleting from it the preserved subgraph.
        var maximalInvariantSubgraph = GetMaximalInvariantSubgraphDeformationRetractingTo(preservedSubgraph.Edges);
        var maximalEdges = maximalInvariantSubgraph.Edges.ToHashSet();

        var subforestToCollapse = new FibredGraph();
        subforestToCollapse.AddVerticesAndEdgeRange(maximalEdges.Except(preservedSubgraph.Edges));
        if (subforestToCollapse.EdgeCount <= 0) 
            yield break;
        
        using var enumerator = CollapseSubforest(subforestToCollapse, preservedSubgraph.Vertices);
        while (enumerator.MoveNext())
            yield return enumerator.Current;

    }

    private IEnumerable<AlgorithmSuggestion> ReduceToSubgraph(IEnumerable<string> preservedSubgraphEdges)
    {
        var preservedSubgraphEdgeSet = preservedSubgraphEdges.ToHashSet();
        var (enumerable, preservedSubgraph) = PrepareReduction(preservedSubgraphEdgeSet);
        foreach (var suggestion in enumerable)
            yield return suggestion;

        // replace the graph by the preserved subgraph
        graph.RemoveEdgeIf(e => !preservedSubgraph.Edges.Contains(e));
        graph.RemoveVertexIf(v => !preservedSubgraph.Vertices.Contains(v));
        
    }

    private IEnumerable<AlgorithmSuggestion> ReduceSubgraphToPeriphery(IEnumerable<string> preservedSubgraphEdges)
    {
        var preservedSubgraphEdgeSet = preservedSubgraphEdges.ToHashSet();
        var (enumerable, preservedSubgraph) = PrepareReduction(preservedSubgraphEdgeSet);
        foreach (var suggestion in enumerable)
            yield return suggestion;

        var preservedEdges = preservedSubgraph.Edges.ToHashSet();
        if (preservedEdges.Count == 0)
        {
            HandleInconsistentBehavior(
                $"The preserved subgraph has no edge anymore. This should not happen. Aborting reduction.");
            yield break;
        }

        yield return new AlgorithmSuggestion(
            $"Double all edges in the preserved subgraph {{{preservedEdges.ToCommaSeparatedString(e => e.ColorfulName)}}}");

        const float lateralShift = 0.02f;
        const float startVectorCutoff = 0.12f;

        var oldStarsAtPreservedVertices = preservedSubgraph.Vertices.ToDictionary(v => v, v => StarOrdered(v).ToList());
        var boundaryWords = BoundaryWords(subgraphEdges: preservedEdges).ToArray();

        // Double each preserved edge into left/right copies.
        var leftCopy = new Dictionary<UnorientedStrip, UnorientedStrip>(preservedEdges.Count);
        var rightCopy = new Dictionary<UnorientedStrip, UnorientedStrip>(preservedEdges.Count);
        IEnumerable<UnorientedStrip> NewEdges() => leftCopy.Values.Concat(rightCopy.Values);
        foreach (var edge in preservedEdges)
        {
            var cutoff = MathF.Min(startVectorCutoff, edge.Curve.Length / 2);
            Curve leftCurve = new ShiftedCurve(edge.Curve, -lateralShift);
            leftCurve = leftCurve.Restrict(cutoff, leftCurve.Length - cutoff);
            Curve rightCurve = new ShiftedCurve(edge.Curve, lateralShift);
            rightCurve = rightCurve.Restrict(cutoff, rightCurve.Length - cutoff);

            var edgeL = edge.CopyUnoriented(name: edge.Name + "l", curve: leftCurve);
            edgeL.Color = NextEdgeColor();
            var edgeR = edge.CopyUnoriented(name: edge.Name + "r", curve: rightCurve);
            
            graph.AddVerticesAndEdge(edgeL);
            graph.AddVerticesAndEdge(edgeR);
            peripheralSubgraph.Add(edgeL);
            peripheralSubgraph.Add(edgeR);
            graph.RemoveEdge(edge);
            peripheralSubgraph.Remove(edge);
            leftCopy[edge] = edgeL;
            rightCopy[edge] = edgeR;
        }

        Strip Left(Strip orientedEdge)
        {
            var edge = orientedEdge.UnderlyingEdge;
            return Equals(edge, orientedEdge) ? leftCopy[edge] : rightCopy[edge].Reversed();
        }

        Strip Right(Strip orientedEdge)
        {
            var edge = orientedEdge.UnderlyingEdge;
            return Equals(edge, orientedEdge) ? rightCopy[edge] : leftCopy[edge].Reversed();
        }

        void ReanchorCurveAtSource(Strip strip)
        {
            if (strip.Curve.Length <= 1e-4f)
                return;

            var startDirection = strip.Curve.StartVelocity.vector;
            if (startDirection.sqrMagnitude <= 1e-8f)
                return;

            var cutoff = MathF.Min(startVectorCutoff, strip.Curve.Length);
            strip.Curve = strip.Curve.AdjustStartVector(new TangentVector(strip.Source.Position, startDirection), cutoff);
        }

        // Create one new vertex for each oriented preserved edge, shifted to the left
        var vertexByOrientedPreservedEdge = new Dictionary<Strip, Junction>(2 * preservedEdges.Count);
        IEnumerable<Junction> NewVertices() => vertexByOrientedPreservedEdge.Values;
        
        foreach (var edge in preservedEdges)
        {
            var eRev = edge.Reversed();

            var vertexAtE = edge.Source.Copy(name: NextVertexName(), color: NextVertexColor(), patches: new[] { leftCopy[edge].Curve.StartPosition });
            graph.AddVertex(vertexAtE);
            var vertexAtERev = edge.Target.Copy(name: NextVertexName(), color: NextVertexColor(), patches: new[] { rightCopy[edge].Curve.EndPosition });
            graph.AddVertex(vertexAtERev);
            
            vertexByOrientedPreservedEdge[edge] = vertexAtE;
            vertexByOrientedPreservedEdge[eRev] = vertexAtERev;
        }


        // For each preserved vertex, traverse the cyclic star and reassign sources in sectors a, e1..ek, b.
        foreach (var (vertex, oldStar) in oldStarsAtPreservedVertices)
        {
            var preservedIndices = oldStar
                .Select((edge, index) => (edge, index))
                .Where(pair => preservedEdges.Contains(pair.edge.UnderlyingEdge))
                .Select(pair => pair.index)
                .ToList();

            if (preservedIndices.Count == 0)
            {
                HandleInconsistentBehavior(
                    $"The vertex {vertex} of the preserved subgraph has no preserved edge in its star. This should not happen.");
                continue;
            }

            
            for (int i = 0; i < preservedIndices.Count; i++)
            {
                var indexA = preservedIndices[i];
                var indexB = i + 1 < preservedIndices.Count
                    ? preservedIndices[i + 1]
                    : preservedIndices[0] + oldStar.Count;
        
                var a = oldStar[indexA];
                var b = oldStar[indexB % oldStar.Count];

                var sectorOutsideEdges = oldStar
                    .Concat(oldStar)
                    .Skip(indexA + 1)
                    .Take(indexB - indexA - 1)
                    .ToList();

                var newSource = vertexByOrientedPreservedEdge[a];

                // Pull tight: Remove the shared initial segment from aL, all e_i, and bR
                var aLeft = Left(a);
                var bRight = Right(b);
                    
                aLeft.Source = newSource;
                ReanchorCurveAtSource(aLeft);

                foreach (var outsideEdge in sectorOutsideEdges)
                {
                    outsideEdge.Source = newSource;
                    ReanchorCurveAtSource(outsideEdge);
                }

                bRight.Source = newSource;
                ReanchorCurveAtSource(bRight);
            }
            
            graph.RemoveVertex(vertex);
        }
        
        var sb = new StringBuilder();
        sb.Append("The boundary words of the subgraph are:\n");
        sb.AppendJoin('\n', boundaryWords.Select(w => w.ToColorfulString(100, 10)));
        sb.Append("\nIsotope away the backtracking in their images by pulling tight the new vertices:");

        List<FibredGraph> VertexGroupsByPretrivialEdges(HashSet<UnorientedStrip> pretrivialEdges)
        {
            var pretrivialForest = new FibredGraph();
            pretrivialForest.AddVertexRange(NewVertices());
            pretrivialForest.AddEdgeRange(NewEdges().Where(e => e.Dg == null));
            return pretrivialForest.ComponentGraphs();
        }

        // Iteratively remove shared initial segments, where adjacency is computed modulo pretrivial edges.
        while (true)
        {
            var changed = false;
            var stripsToUpdate = new Dictionary<Strip, EdgePath>();
            var pretrivialEdges = Strips.Where(strip => strip.EdgePath.IsEmpty).ToHashSet();
            var vertexGroups = VertexGroupsByPretrivialEdges(pretrivialEdges);

            foreach (var vertexGroup in vertexGroups)
            {
                var star = SubgraphStarOrdered(vertexGroup).CyclicShift(e => e switch
                {
                    UnorientedStrip strip => leftCopy.ContainsValue(strip.UnderlyingEdge),
                    ReverseStrip reverseStrip => rightCopy.ContainsValue(reverseStrip.UnderlyingEdge),
                    _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
                }).ToArray();
                
                if (star.Length < 2)
                {
                    HandleInconsistentBehavior(
                        $"The pretrivial segment containing edges {vertexGroup.Edges.ToCommaSeparatedString(v => v.ColorfulName)} has only {star.Length} incident edges!?");
                    continue;
                }

                var sharedInitialSegment = star
                    .Select(edge => edge.EdgePath)
                    .SharedInitialSegment<Strip, EdgePath>()
                    .ToArray();

                if (sharedInitialSegment.Length == 0)
                    continue;

                changed = true;
                sb.Append("\n Vertex group ");
                sb.Append(vertexGroup.Vertices.ToCommaSeparatedString(v => v.ColorfulName));
                sb.Append(" with star\n ");
                sb.Append(star.ToCommaSeparatedString(e => e.ColorfulName));
                sb.Append("\n has shared initial segment \n");
                sb.Append(sharedInitialSegment.ToCommaSeparatedString(e => e.ColorfulName));

                foreach (var strip in star)
                    stripsToUpdate[strip] = strip.EdgePath.Skip(sharedInitialSegment.Length);
            }

            if (!changed)
                break;

            yield return new AlgorithmSuggestion(sb.ToString());
            foreach (var (strip, newPath) in stripsToUpdate)
                strip.EdgePath = newPath;
        }

        // Set the images of the new vertices
        foreach (var boundaryWord in boundaryWords)
        {
            Junction lastVertexImage = null;
            foreach (var strip in boundaryWord.Reverse().CyclicShift(e => Right(e).Dg != null))
            {
                var newStrip = Right(strip);
                if (newStrip.Dg == null)
                    // this is a pretrivial edge, but the last non-pretrivial edge was part of the same vertex group.
                    newStrip.Source.image = lastVertexImage;
                else      
                    // this is the first edge 
                    newStrip.Source.image = lastVertexImage = Right(newStrip.Dg).Source;
            }
        }
        
        // Update EdgePaths: Replace preserved edges with the correct L/R copy based on vertex consistency.
        foreach (var strip in Strips)
        {
            if (preservedEdges.Contains(strip))
                continue; // Don't update the preserved edges themselves yet
            
            var newEdgePath = new List<Strip>();
            foreach (var edgeInPath in strip.EdgePath)
            {
                if (!preservedEdges.Contains(edgeInPath.UnderlyingEdge))
                    newEdgePath.Add(edgeInPath);
                else
                {
                    // Replace with the copy whose Source matches the previous edge's Target. Start where you have to; we assigned images above.
                    var previousTarget = newEdgePath.Count > 0 
                        ? newEdgePath[^1].Target 
                        : strip.Source.image;
                    
                    // Find which copy (Left or Right) has Source matching previousTarget
                    var leftCandidate = Left(edgeInPath);
                    var rightCandidate = Right(edgeInPath);

                    if (Equals(leftCandidate.Source, previousTarget))
                        newEdgePath.Add(leftCandidate);
                    else if (Equals(rightCandidate.Source, previousTarget))
                        newEdgePath.Add(rightCandidate);
                    else
                    {
                        HandleInconsistentBehavior(
                            $"EdgePath of {strip.ColorfulName} contains {edgeInPath.ColorfulName}, but neither L/R copy has Source matching previous Target {previousTarget?.ColorfulName}. Keeping left copy.");
                        newEdgePath.Add(leftCandidate);
                    }
                }
            }
            
            strip.EdgePath = new NormalEdgePath(newEdgePath);
        }

        var components = graph.ComponentGraphs();
        
        // Compute orbits of the connected components
        var componentOrbits = new List<List<FibredGraph>>();
        var visitedComponents = new HashSet<int>();
        
        for (int i = 0; i < components.Count; i++)
        {
            if (visitedComponents.Contains(i))
                continue;
            
            var orbit = new List<FibredGraph> { components[i] };
            visitedComponents.Add(i);
            
            // Find all components that map to the same component in the orbit
            var queue = new Queue<FibredGraph>();
            queue.Enqueue(components[i]);
            while (queue.Count > 0)
            {
                var currentComponent = queue.Dequeue();
                var imageVertex = currentComponent.Vertices.First().image;
                
                // Find the component containing the image vertex
                for (int j = 0; j < components.Count; j++)
                {
                    if (!visitedComponents.Contains(j) && components[j].Vertices.Contains(imageVertex))
                    {
                        orbit.Add(components[j]);
                        visitedComponents.Add(j);
                        queue.Enqueue(components[j]);
                        break;
                    }
                }
            }
            
            componentOrbits.Add(orbit);
        }
        
        // Create options grouped by orbit
        var options = new List<(object, string)>();
        foreach (var orbitIndex in Enumerable.Range(0, componentOrbits.Count))
        {
            var orbit = componentOrbits[orbitIndex];
            var nonPeripheralComponents = orbit.Where(c => !c.Edges.All(peripheralSubgraph.Contains)).ToList();
            
            if (nonPeripheralComponents.Count == 0)
                continue;
            
            var orbitDescription = nonPeripheralComponents.Count == 1
                ? nonPeripheralComponents[0].Edges.ToCommaSeparatedString(e => e.ColorfulName)
                : string.Join(" → ", nonPeripheralComponents.Select(c => 
                    $"{{{c.Edges.ToCommaSeparatedString(e => e.ColorfulName)}}}"));
            
            options.Add((orbitIndex, orbitDescription));
        }

        if (options.Count == 0)
        {
            HandleInconsistentBehavior($"After reduction, all components of the graph map to peripheral components. This should not happen. Aborting reduction.");
            yield break;
        }
            
        yield return new AlgorithmSuggestion(
            options: options,
            description: "Choose one orbit of connected components:",
            buttons: new[]{ AlgorithmSuggestion.generalSubroutineContinueButton }
        );
        
        // Get the selected orbit index from the user
        var selectedOrbitIndex = selectedOptionsDuringAlgorithmPause?.FirstOrDefault() as int? ?? 0;

        var selectedOrbit = componentOrbits[selectedOrbitIndex];
        if( selectedOrbit.Any(c => c.Edges.All(peripheralSubgraph.Contains) ) ) 
            HandleInconsistentBehavior("The selected orbit has a purely peripheral component. This should not happen.");
        
        

        var newGraph = selectedOrbit.First();
        
        // If the orbit has multiple non-peripheral components, ask the user to choose one
        if (selectedOrbit.Count > 1)
        {
            yield return new AlgorithmSuggestion(
                options: selectedOrbit.Select((component, index) =>
                        (index as object, 
                            component.Edges.ToCommaSeparatedString(e => e.ColorfulName))
                ),
                description: $"The selected orbit has {selectedOrbit.Count} components. Choose one:",
                buttons: new[]{ AlgorithmSuggestion.generalSubroutineContinueButton }
            );
            
            var selectedComponentIndex = selectedOptionsDuringAlgorithmPause?.FirstOrDefault() as int? ?? 0;
            newGraph = selectedOrbit[selectedComponentIndex];
        }
        
        graph.RemoveEdgeIf(e => !newGraph.Edges.Contains(e));
        graph.RemoveVertexIf(v => !newGraph.Vertices.Contains(v));
        peripheralSubgraph.RemoveWhere(e => !newGraph.Edges.Contains(e));
        
        // Replace the graph map with the k-th power, where k is the orbit size
        int k = selectedOrbit.Count;
        if (k > 1)
        {
            yield return new AlgorithmSuggestion(
                $"Replacing the graph map with its {k}-th power to make the selected component fixed.");
            
            // Save the original map (before modifying it)
            var originalMap = OrientedEdges.ToDictionary( edge => edge, edge => edge.EdgePath );
            
            // Compute g^k by composing the map with itself k-1 times using SetMap with Precompose
            for (int iter = 0; iter < k - 1; iter++) 
                SetMap(originalMap, GraphMapUpdateMode.Precompose);
        }
        // sanity check; see if the strip and vertex images are consistent
        foreach (var strip in Strips)
        {
            if (strip.Dg == null && !strip.Source.image.Equals(strip.Target.image))
                HandleInconsistentBehavior(
                    $"The vertices at the ends of strip {strip.ColorfulName} have different images under the graph map, but the strip itself is mapped to the trivial edge path. This should not happen.");
            else if (strip.Dg.Source != strip.Source || strip.Dg.Target != strip.Target)
                HandleInconsistentBehavior(
                    $"Strip {strip.ColorfulName} has inconsistent image after reduction. This should not happen.");
            if (!graph.Vertices.Contains(strip.Source) || !graph.Vertices.Contains(strip.Target))
                HandleInconsistentBehavior(
                    $"Strip {strip.ColorfulName} has source or target vertex that is not in the graph after reduction. This should not happen.");
        }
    }
}

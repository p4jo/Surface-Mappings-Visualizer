using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public partial class FibredSurface
{
    FibredGraph GetMaximalPeripheralSubgraph()
    {
        var Q = new FibredGraph(true);
        Q.AddVerticesAndEdgeRange(peripheralSubgraph);
        foreach (var strip in Strips)
        {
            if (Q.Edges.Contains(strip)) continue;
            var orbit = OrbitOfEdge(strip);
            if (IsPeripheryFriendlySubforest(orbit, Q.Edges.ToHashSet(), true, true))
                // this means that after adding the orbit to Q, it still deformation retracts onto Q (thus P).
                Q.AddVerticesAndEdgeRange(orbit);
        }

        return Q;
    }

    public AlgorithmSuggestion AbsorbIntoPeripherySuggestion()
    {
        var Q = GetMaximalPeripheralSubgraph();
        var qEdges = Q.Edges.ToHashSet();
        var valenceTwoVertices = Q.Vertices.Where(v => Star(v).Count() <= 2).ToArray();
        var starQ = SubgraphStar(Q).ToList();
        
        Q.RemoveEdges(peripheralSubgraph);
        StringBuilder sb = new ();
        if (Q.EdgeCount > 0)
        {
            var trees = Q.ComponentGraphs();
            // todo? we could sort these by component of P that they attach to. Is not really priority, mostly there will be only very few trees, often only one edge in total.
            sb.Append("Q = P \u22c3 {");
            sb.AppendJoin("} \u22c3 {",
                from tree in trees
                where tree.EdgeCount > 0
                select tree.Edges.ToCommaSeparatedString(e => e.ColorfulName)
            );
            sb.Append("} deformation retracts to P");
        }

        if (valenceTwoVertices.Length > 0)
        {
            sb.Append(Q.EdgeCount > 0 ? " and " : "P ");
            sb.Append("has valence two vertices: ");
            sb.AppendJoin(", ", valenceTwoVertices.Select(v => v.ColorfulName));
        }
        
        var nonMaximalStrips = starQ.Where(strip => qEdges.Contains(strip.Dg!.UnderlyingEdge)).ToList();
        if (nonMaximalStrips.Count > 0)
        {
            if (sb.Length > 0) sb.Append(".\n");
            string qName = (Q.EdgeCount > 0 ? "Q" : "P");
            sb.Append($"The following edges in Star({qName}) start with edges in {qName}: ");
            sb.AppendJoin(", ", nonMaximalStrips.Select(e => e.ColorfulName));
        }

        if (sb.Length == 0)
            return null; // nothing to do.

        return new AlgorithmSuggestion(
            description: "Absorb into the periphery",
            options: new[] { ((object)Q, sb.ToString()) }, 
            buttons: new[] { AlgorithmSuggestion.absorbPAtOnceButton, AlgorithmSuggestion.absorbPInStepsButton }
        );
    }

    public IEnumerable<AlgorithmSuggestion> AbsorbIntoPeriphery(FibredGraph QwithoutP)
    {

        // var trees = QwithoutP.ComponentGraphs(out var componentDict);
        
        // collapse the trees exactly like collapsing subforests, but keep track of the edge images
        using var enumerator = CollapseSubforest(QwithoutP);
        while (enumerator.MoveNext())
            yield return enumerator.Current;
        
        
        var oldPeripheralEdges = peripheralSubgraph.ToHashSet();
        var oldPeripheralVertices = peripheralSubgraphVertices.ToHashSet(); // is already HashSet actually
        
        var peripheralBoundaryWordsCounterClockwise = (
            from w in BoundaryWords() 
            where w.All(e => oldPeripheralEdges.Contains(e.UnderlyingEdge))
            select w.Inverse
        ).ToArray();
        
        var previousEdgeOld = new Dictionary<Junction, Strip>(graph.VertexCount); // counterclockwise
        var nextEdgeOld = new Dictionary<Junction, Strip>(graph.VertexCount); // counterclockwise
        var pathsInP = new Dictionary<Strip, Strip[]>(graph.EdgeCount); // for each edge in the star of a vertex in Q \ P, the initial segment of its edge path that is in P.
        var componentDict = new Dictionary<Junction, int>();
        for (int i = 0; i < peripheralBoundaryWordsCounterClockwise.Length; i++)
        {
            var lastEdge = peripheralBoundaryWordsCounterClockwise[i].LastOrDefault();
            foreach (var e in peripheralBoundaryWordsCounterClockwise[i])
            {
                componentDict[e.Source] = i;
                previousEdgeOld[e.Source] = lastEdge.Reversed();
                nextEdgeOld[e.Source] = e;
                lastEdge = e;

                
                foreach (var strip in Star(e.Source))
                {
                    if (oldPeripheralEdges.Contains(strip.UnderlyingEdge)) continue;
                    
                    // we do this way before changing the other edge paths, because we want to determine the gates by the first edges in the edge paths that are not in Q!
                    var pathInP = strip.EdgePath.TakeWhile(edge => oldPeripheralEdges.Contains(edge.UnderlyingEdge)).ToArray();
                    pathsInP[strip] = pathInP;
                    if (pathInP.Length == strip.EdgePath.Count)
                        throw new Exception($"The strip {strip} only contains Q edges but is not absorbed into the periphery. This should not happen, as we assumed that Q was maximal among invariant subgraphs deformation retracting to P.");
                    if (pathInP.Length > 0)
                        strip.EdgePath = strip.EdgePath.Skip(pathInP.Length); 
                }
            }
        }
        
        
        var gates = Gate.FindGates(graph, vertex => componentDict.GetValueOrDefault(vertex, -1)); 
        // this groups together gates from different vertices outside of P, but we don't care about them anyways.
        
        // takes a St(Q) edge and gives the original linear order of the star of its source vertex (component of Q \ P)
        var linearStars = new Dictionary<Junction, List<Strip>>(graph.EdgeCount);
        var oldSource = new Dictionary<Strip, Junction>(graph.EdgeCount);
        var gatesOrderedByComponent = new List<List<Gate<int>>>(peripheralBoundaryWordsCounterClockwise.Length);
        
        var componentStars = new List<List<Strip>>(peripheralBoundaryWordsCounterClockwise.Length);
        var newVertices = new Dictionary<Gate<int>, Junction>(gates.Count);
        var nextEdgeNew = new Dictionary<Junction, Strip>(gates.Count);
        var previousEdgeNew = new Dictionary<Junction, Strip>(gates.Count);
        // 
        for (int i = 0; i < peripheralBoundaryWordsCounterClockwise.Length; i++)
        {
            var newComponentVertices = new List<Junction>(gates.Count);
            var component = peripheralBoundaryWordsCounterClockwise[i]; // runs around counterclockwise, matching the cyclic order.
            var componentVertices = new List<Junction>();
            var starC = new List<Strip>();
            foreach (var pEdge in component)
            {
                var v = pEdge.Source;
                var star = StarOrdered(v, pEdge).Skip(2).ToList();
                // since the component is the boundary word, c = boundaryWord.Inverse = ... rev(e_{k+1}) | rev(e_{k}) ..., the cyclic order here is by def. of the boundary word: rev(e_k), e_{k+1}, a, ... , z and we will add a...z. e = rev(e_{k})

                linearStars[v] = star;
                foreach (var e in star)
                {
                    oldSource[e] = v;
                }
                if (star.Count > 0)
                    componentVertices.Add(v); // todo: check if the valence two vertices are handled correctly
                starC.AddRange(star);
            } 
            componentStars.Add(starC);
            
            var gatesC = gates
                .Where(gate => gate.junctionIdentifier == i && !oldPeripheralEdges.IsSupersetOf(gate.Edges.Select(e => e.UnderlyingEdge)))
                .OrderBy(gate => gate.Edges.First().OrderIndexStart).ToList();
            gatesOrderedByComponent.Add(gatesC);
            
            
            var usedJunctions = new List<float>();// this will contain the position indices of the new junctions that will be used by gates, the indices are counted from indexOffset overflowing verticesOfGate.Count (at most) once.
            var usedOldJunctions = new List<int>(); // this will contain the indices of the old junctions that will be used by gates, the indices are counted from indexOffset overflowing verticesOfGate.Count (at most) once.
            int lastIndexOffset = 0; 
            
            
            foreach (var gate in gatesC)
            {
                List<Strip> gateInOrder;
                if (gatesC.Count == 1)
                {
                    // If there is only one gate, there are multiple inequivalent ways to choose the first edge. The following is a lemma.
                    var (firstEdgeIndex, _) = starC.ArgMaxIndex( // starC = gate.Edges in cyclic order
                        edge =>
                        {
                            var pathInP = CancelBacktracking(pathsInP[edge]);
                            var counterclockwise = Equals(pathInP.FirstOrDefault(), nextEdgeOld[edge.Source.image]);
                            return counterclockwise ? pathInP.Count : -pathInP.Count;
                        }
                    );
                    gateInOrder = starC.CyclicShift(firstEdgeIndex).ToList();
                }
                else
                {
                    gateInOrder = SortConnectedSet(starC, gate.Edges).ToList();
                }
                
                if (!gateInOrder.ToHashSet().SetEquals(gate.Edges)) 
                    throw new Exception($"The peripheral gate {gate} is not connected in the cyclic order of the star of the peripheral subgraph (after collapsing the trees of Q\\P into their components).");

                foreach (var (index,strip) in gateInOrder.Enumerate())
                {
                    strip.OrderIndexStart = index; // this will remain the same also throughout the movements.
                }
                
                var verticesOfGate = gateInOrder.Select(e => e.Source).Distinct().ToList();
                // the following assumes that all edges of a gate at the same vertex are moved along P together, which is true if the linear order of the gate agrees with the linear order of that vertex. The only problem that can happen is if the first and last edges of the gate are at the same vertex (we have to split the vertex and move the corresponding edges in different directions around P). So we split that vertex in this case:
                
                var lastVertex = gateInOrder[^1].Source; 
                var linearStarOfLastVertex = linearStars[lastVertex];
                var wronglyOrderedEdgesAtLastVertex = gateInOrder.TakeWhile(e => linearStarOfLastVertex.IndexOf(e) > 0).ToArray(); // if the lastVertex is not the first vertex, there is no problem, and this will be empty.
                if (wronglyOrderedEdgesAtLastVertex.Length > 0)
                {
                    var oppositeWronglyOrderedEdges = linearStarOfLastVertex.TakeWhile(e => e != gateInOrder[0]).ToArray();
                    
                    yield return new AlgorithmSuggestion(
                        description: $"The peripheral gate {{{gateInOrder.ToCommaSeparatedString(e => e.ColorfulName)}}} starts and ends at the vertex {lastVertex.ColorfulName}, wrapping around the component {component} of P: The linear order of the star of {lastVertex.ColorfulName} is {{{linearStarOfLastVertex.ToCommaSeparatedString(e => e.ColorfulName)}}}, differing from the gate's linear order. ",
                        options: new[]
                        {
                            ((object)true, $"Move the edges {oppositeWronglyOrderedEdges.ToCommaSeparatedString(e => e.ColorfulName)} along {previousEdgeOld[lastVertex].ColorfulName}"),
                            ((object)false, $"Move the edges {wronglyOrderedEdgesAtLastVertex.ToCommaSeparatedString(e => e.ColorfulName)} along {nextEdgeOld[lastVertex].ColorfulName}")
                        },
                        buttons: new[] { AlgorithmSuggestion.generalSubroutineContinueButton }
                    );
                    
                    var opposite = Equals(selectedOptionsDuringAlgorithmPause?.FirstOrDefault(), true);
                    Strip[] edgesToMove;
                    Strip direction;
                    MoveJunctionShiftType shiftType;
                    if (opposite)
                    {
                        edgesToMove = oppositeWronglyOrderedEdges;
                        direction = previousEdgeOld[lastVertex];
                        shiftType = MoveJunctionShiftType.StrictlyToTheLeft;
                    }
                    else
                    {
                        edgesToMove = wronglyOrderedEdgesAtLastVertex;
                        direction = nextEdgeOld[lastVertex];
                        shiftType = MoveJunctionShiftType.StrictlyToTheRight;
                    }
                    var temporarySplitJunction = new Junction(lastVertex.fibredSurface, lastVertex.Position, $"temporary junction for edges {edgesToMove.ToCommaSeparatedString(e => e.Name)}", color: Color.black);
                    
                    // we could update the order, but we actually need this to be the old order to determine clockwise/counterclockwise for the edges mapping through P'
                    // linearStars[lastVertex].RemoveAll(edgesToMove.Contains);
                    // linearStars[direction.Target].InsertRange(opposite ? linearStars[direction.Target].Count : 0, edgesToMove); 
                    // if opposite, they go to the end, else to the beginning.
                    
                    foreach (var strip in edgesToMove) 
                        strip.Source = temporarySplitJunction;
                    MoveJunction(temporarySplitJunction, direction.Curve, direction.Curve.Length, shiftType: shiftType);
                    foreach (var strip in edgesToMove) 
                        strip.Source = direction.Target;
                    graph.RemoveVertex(temporarySplitJunction);
                }
                
                int indexOffset = componentVertices.IndexOf(verticesOfGate.First()); // this is the index of the first vertex of the gate in the cyclic order of the star of the component, i.e. the vertex set of the gate is {componentVertices[indexOffset], componentVertices[indexOffset + 1], ...} (indices mod componentVertices.Count). Two gates might have the same vertices, but in different orders, with different indexOffsets, so that we actually wrap around correctly. IndexOffset should increase everytime
                if (indexOffset < lastIndexOffset)
                    indexOffset += componentVertices.Count;
                lastIndexOffset = indexOffset; 
                
                float Badness(float k) => gateInOrder.Sum(e => Mathf.Abs(verticesOfGate.IndexOf(e.Source) - (k - indexOffset))); // same as the distance in componentVertices, but handles the wraparound better.
                
                var movements = new List<(float, (object, string))>();
                var startIndex = Math.Max(usedOldJunctions.Count > 0 ? usedOldJunctions[^1] : int.MinValue, indexOffset);
                    // we have to ignore the first few vertices if the last gate was already moved there, because we have to maintain the cyclic order
                var stopIndex = Math.Min(usedOldJunctions.Count > 0 ? usedOldJunctions[0] + componentVertices.Count : int.MaxValue, indexOffset + verticesOfGate.Count - 1); 
                for (int k = startIndex; k <= stopIndex; k++)
                {
                    // k will be the vertexIndex, which will be usedOldJunctions[^1].
                    var kN = k % componentVertices.Count;
                    var v = componentVertices[kN];
                    
                    var badness = Badness(k);
                    if (!usedOldJunctions.Contains(kN))
                        movements.Add((badness, (k, $"{(k - indexOffset).ToOrdinal()} vertex {v.ColorfulName}. Movement has to be over {badness} edges.")));
                    else
                    {
                        if (k == startIndex)
                        {
                            var nextHigherIndex = 0.5f;
                            while (usedJunctions.Contains(kN + nextHigherIndex)) nextHigherIndex /= 2;
                            badness = Badness(k + nextHigherIndex);
                            movements.Add((badness, (k + nextHigherIndex,
                                $"ε = {nextHigherIndex} after the {(k - indexOffset).ToOrdinal()} vertex {v.ColorfulName}. Movement has to be over {badness} edges.")));
                        }
                        else if (k == stopIndex)
                        {
                            if (kN == 0)
                                kN = componentVertices.Count;
                            var nextLowerIndex = 0.5f;
                            while (usedJunctions.Contains(kN - nextLowerIndex)) nextLowerIndex /= 2;
                            badness = Badness(k - nextLowerIndex);
                            movements.Add((badness, (k - nextLowerIndex,
                                $"ε = {nextLowerIndex} before the {(k - indexOffset).ToOrdinal()} vertex {v.ColorfulName}. Movement has to be over {badness} edges.")));
                        }
                        else
                        {
                            throw new Exception(
                                "Weird behavior: It seems there was a junction used when creating the gate's vertices in the free part, i.e. not in order.");
                        }
                    }
                }
                
                var options = movements.OrderBy(m => m.Item1).Select(m => m.Item2);
                if (movements.All(tuple => tuple.Item1 != 0)) // if there is a "movement" with badness 0, this means that the entire gate is already contained in a vertex and that vertex is also not taken, so nothing will happen below, no need to confuse the user.
                {
                    yield return new AlgorithmSuggestion(
                        description:
                        $"Create new vertex for {gate} in the star of the component P_{i} = {component.ToColorfulString(150, 10)}. The vertices along the gate are {verticesOfGate.ToCommaSeparatedString(j => j.ToColorfulString())}. Move the origin of the edges along P_{i} to one of the selected vertices (or to an intermediate position)",
                        options: options,
                        buttons: new[] { AlgorithmSuggestion.generalSubroutineContinueButton }
                    );
                }
                else
                    selectedOptionsDuringAlgorithmPause = null; 

                var selection = selectedOptionsDuringAlgorithmPause?.FirstOrDefault() ?? movements.FirstOrDefault().Item2.Item1;

                float vertexIndex = selection switch
                {
                    float a => a,
                    int b => b,
                    _ => throw new InvalidOperationException(
                        $"Selected vertex {selection} is not a valid option for the new vertex of the gate {gate} in the absorption into the periphery.")
                };
                
            #region Move the edges along P to the new vertex
                
                var vertexIndexInt = Mathf.RoundToInt(vertexIndex);
                bool isInt = Math.Abs(vertexIndex - vertexIndexInt) < 1e-8;
                if (isInt) vertexIndex = vertexIndexInt;
                usedJunctions.Add(vertexIndex);
                if (isInt) usedOldJunctions.Add(vertexIndexInt);
                
                Junction newJunction = isInt ? 
                    componentVertices[vertexIndexInt % verticesOfGate.Count] : // reuse old junction
                    new Junction(this, verticesOfGate[0].Position, NextVertexName(), color: NextVertexColor()); // create new junction
                    
                for (var indexInGate = 0; indexInGate < verticesOfGate.Count; indexInGate++)
                {
                    var oldVertexIndex = indexInGate + indexOffset;
                    var junction = verticesOfGate[indexInGate]; // = componentVertices[oldVertexIndex % componentVertices.Count]
                    
                    var movementDistance = (vertexIndex - oldVertexIndex); // integer if isInt
                    if (movementDistance == 0)
                        continue;
                    
                    Junction temporaryJunctionForMovement;
                    if (indexInGate == 0 && !isInt)
                    {
                        temporaryJunctionForMovement = newJunction; 
                    }
                    else
                    {
                        temporaryJunctionForMovement = new Junction(this, junction.Position,
                            $"temporary junction {vertexIndex}", color: Color.black);
                    }

                    foreach (var strip in gateInOrder)
                    {
                        if (strip.Source == junction)
                            strip.Source = temporaryJunctionForMovement;
                    }

                    var componentToMoveAlong = component;
                    var firstEdgeIndex = oldVertexIndex % componentVertices.Count; 
                    var moveJunctionShiftType = MoveJunctionShiftType.StrictlyToTheRight; // shift to the outside, component is counterclockwise
                    if (movementDistance < 0)
                    {
                        componentToMoveAlong = componentToMoveAlong.Inverse; 
                        movementDistance = -movementDistance;
                        moveJunctionShiftType = MoveJunctionShiftType.StrictlyToTheLeft;
                        firstEdgeIndex = componentVertices.Count - firstEdgeIndex;
                    }
                    if (componentToMoveAlong[firstEdgeIndex].Source != junction)
                        throw new Exception($"The first edge to move along does not start at the junction we want to move. This should not happen. Junction: {junction}, first edge index: {firstEdgeIndex}, first edge: {componentToMoveAlong[firstEdgeIndex]}, component: {componentToMoveAlong}");
                    var movementDistanceFractionalReverse = Mathf.CeilToInt(movementDistance) - movementDistance; // 0 if integer, otherwise the distance to the next integer
                    

                    var movementEdges = componentToMoveAlong.EndlessLoop()
                        .Skip(firstEdgeIndex).Take(Mathf.CeilToInt(movementDistance));
                    var movementCurves = movementEdges.Select(e => e.Curve).ToList();
                    var movementCurve = new ConcatenatedCurve(movementCurves);
                    
                    var movementCurveLength = movementCurve.Length - movementCurves[^1].Length * movementDistanceFractionalReverse;
                    
                    MoveJunction(temporaryJunctionForMovement, movementCurve, movementCurveLength, shiftType: moveJunctionShiftType);

                    foreach (var strip in gateInOrder)
                    {
                        if (strip.Source == temporaryJunctionForMovement)
                            strip.Source = newJunction;
                    }
                    
                    if (indexInGate != 0 || isInt)
                        graph.RemoveVertex(temporaryJunctionForMovement);
                }

                newComponentVertices.Add(newJunction);
                newVertices[gate] = newJunction;
            #endregion
            }
            
            #region Define new Edges 
            
            foreach (var e in component)
            {
                graph.RemoveEdge(e.UnderlyingEdge);
                peripheralSubgraph.Remove(e.UnderlyingEdge);
                if (!newComponentVertices.Contains(e.Source))
                    graph.RemoveVertex(e.Source);
            }

            var lastPosition = usedJunctions[^1];
            var lastJunction = newComponentVertices[^1];
            for (int j = 0; j < usedJunctions.Count; j++)
            {
                var currentPosition = usedJunctions[j];
                var currentJunction = newComponentVertices[j];
                if (currentPosition <= lastPosition) // the usedJunctions should be strictly increasing, but around the circle (counterclockwise)
                    currentPosition += componentVertices.Count; 

                int lastPosInt = Mathf.FloorToInt(lastPosition);
                var lastPosFrac = lastPosition - lastPosInt;
                int currentPosInt = Mathf.CeilToInt(currentPosition);
                var currentPosFracRev = currentPosInt - currentPosition;
                var followedEdges = component.Concat(component).Skip(lastPosInt).Take(currentPosInt - lastPosInt);
                if (followedEdges.Count == 1 && currentPosFracRev == 0 && lastPosFrac == 0)
                {
                    // if we are moving exactly from one vertex to another, we can just reuse the edge between them, no need to create a new one and split it.
                    var edgeToReuse = followedEdges.First();
                    edgeToReuse.Source = lastJunction; // this adds the curve to the graph again (it still has the reference to this)
                    edgeToReuse.Target = currentJunction;
                    edgeToReuse.OrderIndexStart = graph.EdgeCount; 
                    edgeToReuse.OrderIndexEnd = -1; 
                    peripheralSubgraph.Add(edgeToReuse.UnderlyingEdge);
                    nextEdgeNew[lastJunction] = edgeToReuse;
                    previousEdgeNew[currentJunction] = edgeToReuse.Reversed();
                    continue;
                }
                var followedCurves = followedEdges.Select(e => e.Curve).ToList();
                var followedCurve = new ConcatenatedCurve(followedCurves);

                var curveStart = followedCurves[0].Length * lastPosFrac;
                var curveEnd = followedCurve.Length - followedCurves[^1].Length * currentPosFracRev;
                
                var newCurve = followedCurve.Restrict(curveStart, curveEnd); // counterclockwise, from lastJunction to currentJunction
                
                var newEdge = new UnorientedStrip(newCurve, lastJunction, currentJunction, EdgePath.Empty, this, orderIndexStart: graph.EdgeCount, orderIndexEnd: -1, newColor: true, addToGraph: true);
                newEdge.Name = NextEdgeNameGreek();
                
                peripheralSubgraph.Add(newEdge.UnderlyingEdge);
                nextEdgeNew[lastJunction] = newEdge;
                previousEdgeNew[currentJunction] = newEdge.Reversed();

                lastPosition = currentPosition;
                lastJunction = currentJunction;
            }
            #endregion
        }
        
        #region assign g on P'
        foreach (var (gate, junction) in newVertices)
        {
            var imageGate = gates.First(g => g.Edges.Contains(gate.Edges.First().Dg));
            junction.image = newVertices[imageGate];
            
            nextEdgeNew[junction].EdgePath = new NormalEdgePath(
                nextEdgeNew[junction.image]
            ); 
            if (nextEdgeNew[junction.image].Target != nextEdgeNew[junction].Target.image)
                throw new Exception($"The new graph map does not seem to act as a graph automorphism on the new peripheral graph.");
            
        }
        #endregion


        #region isotope junctions mapping into Q
        

        var newPeripheralVertices = newVertices.Values.ToHashSet();
        foreach (var junction in graph.Vertices)
        {
            if (!oldPeripheralVertices.Contains(junction.image)) continue;
            if (newPeripheralVertices.Contains(junction)) continue; // this can only happen if junction.image is both a new and an old peripheral vertex.
            var previousEdge = previousEdgeOld[junction];
            var nextEdge = nextEdgeOld[junction];
            Dictionary<Strip, int> gateIndices = new Dictionary<Strip, int>();

            var star = Star(junction).ToArray();
            
            foreach (var strip in star)
            {
                if (strip.Dg == previousEdge)
                    gateIndices[strip] = gates.Count; // just some high number
                else if (strip.Dg == nextEdge)
                    gateIndices[strip] = -1; // just some low number
                else
                {
                    var gate = GetGate(strip.Dg);
                    var component = gate.junctionIdentifier;
                    gateIndices[strip] = gatesOrderedByComponent[component].IndexOf(gate);
                }
            }

            var (j, badness) = Enumerable.Range(0, gates.Count).ArgMin(j => Badness(j));
            junction.image = newVertices[gates[j]];
            var a = gates[j].Edges.First();

            foreach (var strip in star)
            {
                var originalPathInP = strip.EdgePath.TakeWhile(e => oldPeripheralEdges.Contains(e.UnderlyingEdge)).ToArray();
                strip.EdgePath = GetEdgePath(a, strip.Dg, originalPathInP).Concat(
                    strip.EdgePath.Skip(originalPathInP.Length)
                );
            }

            continue;

            int Badness(int j) => gateIndices.Values.Sum(i => Math.Abs(j - i));
        }
        #endregion

        #region Define g on edge interior points mapping to Q
        foreach (var strip in graph.Edges)
        {
            var edgePath = strip.EdgePath.ToList();
            for (int i = 1; i < edgePath.Count; i++) // should not be able to start or end in old vertices / edges anymore.
            {
                if (!newPeripheralVertices.Contains(edgePath[i].Source)) continue; // look at edges in St(P')
                var originalPathInP = new List<Strip>();
                while (oldPeripheralEdges.Contains(edgePath[i].UnderlyingEdge))
                {
                    originalPathInP.Add(edgePath[i]);
                    edgePath.RemoveAt(i);
                }

                var intermediateEdgePath = GetEdgePath(edgePath[i - 1].Reversed(), edgePath[i], originalPathInP);
                edgePath.InsertRange(i, intermediateEdgePath);
                i += intermediateEdgePath.Count; // edgePath[i] is still the previous edgePath[i].
            }
            strip.EdgePath = new NormalEdgePath(edgePath);
        }

        #endregion
        yield break;

        Gate<int> GetGate(Strip edge) => gates.FirstOrDefault(g => g.Edges.Contains(edge));

        EdgePath GetEdgePath(Strip a, Strip b, IEnumerable<Strip> originalPathInP)
        {
            bool clockwise;
            var tightPathInP = CancelBacktracking(originalPathInP);
            bool nontrivial = tightPathInP.Count > 0;
            if (nontrivial)
            {
                Junction v = a.Source;
                var previousEdge = previousEdgeOld[v];
                var nextEdge = nextEdgeOld[v];
                if (tightPathInP[0] == previousEdge)
                    clockwise = true;
                else if (tightPathInP[0] == nextEdge)
                    clockwise = false;
                else
                    throw new Exception($"The original path in P should start with either the previous or the next edge of the vertex where we are attaching the new edge, but it starts with {tightPathInP[0]} instead of {previousEdge} or {nextEdge}.");
            }
            else
            {
                var linearOrder = linearStars[oldSource[a]];
                int indexA = linearOrder.IndexOf(a);
                int indexB = linearOrder.IndexOf(b);
                if (indexA == -1 || indexB == -1)
                    throw new Exception($"Both edges should be in the star of the vertex where we are attaching the new edge, but {a} is at index {indexA} and {b} is at index {indexB} in the linear order of the star.");
                clockwise = indexA > indexB;
            }

            return new NormalEdgePath(FollowAroundP(a, b, clockwise, nontrivial));
        }

        List<Strip> CancelBacktracking(IEnumerable<Strip> edgePath)
        {
            List<Strip> result = new List<Strip>();
            foreach (var e in edgePath)
            {
                if (result.Count > 0 && result[^1].Equals(e.Reversed()))
                    result.RemoveAt(result.Count - 1);
                else
                    result.Add(e);
            }
            return result;
        }

        IEnumerable<Strip> FollowAroundP(Strip a, Strip b, bool clockwise, bool nontrivial)
        {
            var startGate = GetGate(a);
            var startJunction = newVertices[startGate];
            var targetGate = GetGate(b);
            var targetJunction = newVertices[targetGate];
            int overflowCounter = 0;
            if (!nontrivial && Equals(startJunction, targetJunction))
            {
                var linearOrder = startGate.Edges;
                int indexA = linearOrder.IndexOf(a);
                int indexB = linearOrder.IndexOf(b);
                if (indexA == -1 || indexB == -1)
                    throw new Exception($"Both edges should be in the star of the vertex where we are attaching the new edge, but {a} is at index {indexA} and {b} is at index {indexB} in the linear order of the star.");
                var clockwiseInGate = indexA > indexB;
                nontrivial = clockwise != clockwiseInGate; 
            }
            while ((nontrivial && overflowCounter == 0) || !Equals(startJunction, targetJunction))
            {
                var nextEdge = clockwise ? previousEdgeNew[startJunction] : nextEdgeNew[startJunction];
                yield return nextEdge;
                startJunction = nextEdge.Target;
                overflowCounter++;
                if (overflowCounter > graph.VertexCount)
                    throw new Exception($"We seem to be going in circles when following around the periphery from {a} to {b}. This should not happen.");
            }
        }
    }

}
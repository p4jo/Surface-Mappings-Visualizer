using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using MathNet.Numerics.LinearAlgebra;
using QuikGraph;
using QuikGraph.Algorithms;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;
using Object = UnityEngine.Object;

/// <summary>
/// This implements the Bestvina-Handel algorithm.
/// All methods are in-place. Copy the graph before.
/// </summary>
public class FibredSurface : IPatchedDrawnsformable
{
    public readonly FibredGraph graph;

    /// <summary>
    /// The peripheral subgraph P contains one loop around every puncture of the surface apart from one orbit of punctures.
    /// P is assumed to have only valence-two vertices (that have higher valence in the main graph).
    /// We also assume that g acts on it as a graph automorphism.
    /// </summary>
    public readonly FibredGraph peripheralSubgraph;

    public readonly GeodesicSurface surface;

    public IEnumerable<IDrawnsformable> Patches =>
        graph.Vertices.Concat<IDrawnsformable>(from edge in Strips select edge.Curve);

    public IEnumerable<UnorientedStrip> Strips => graph.Edges;
    
    public IEnumerable<UnorientedStrip> StripsOrdered => graph.Vertices.SelectMany(
        v => StarOrdered(v).Select(e => e.UnderlyingEdge)
    ).Distinct().OrderByDescending(
        edge => edge.EdgePath.Count    
    );

    public IEnumerable<Strip> OrientedEdges => Strips.Concat<Strip>(Strips.Select(edge => edge.Reversed()));

    public IEnumerable<NamedEdgePath> UsedNamedEdgePaths => Strips.SelectMany(e => e.EdgePath.NamedEdgePaths).Distinct().
        OrderBy(v => v.name);

    #region Constructors

    public FibredSurface(FibredGraph graph, GeodesicSurface surface, FibredGraph peripheralSubgraph = null)
    {
        this.graph = graph;
        this.surface = surface;
        this.peripheralSubgraph = peripheralSubgraph ?? new FibredGraph(true);
    }

    public FibredSurface(IList<(Curve, string, string, string)> edgeDescriptions, GeodesicSurface surface,
        IEnumerable<string> peripheralEdges = null, IDictionary<string, IDrawnsformable> junctionDrawables = null)
    {
        this.surface = surface;
        junctionDrawables ??= new Dictionary<string, IDrawnsformable>();
        peripheralEdges ??= new List<string>();
        graph = new FibredGraph(true); // up here, so that the junctions reference this

        var vertexNames =
            (from v in edgeDescriptions select v.Item2)
            .Concat(from v in edgeDescriptions select v.Item3).ToHashSet();
        var junctions = new Dictionary<string, Junction>(
            from name in vertexNames
            select new KeyValuePair<string, Junction>(name,
                new Junction(this,
                    junctionDrawables.TryGetValue(name, out var displayable)
                        ? displayable
                        : edgeDescriptions.FirstOrDefault(tuple => tuple.Item2 == name).Item1?.StartPosition
                          ?? edgeDescriptions.FirstOrDefault(tuple => tuple.Item3 == name).Item1?.EndPosition, 
                    name: name,
                    color: NextVertexColor()) // image is set below
            )
        );
        var strips = (
            from tuple in edgeDescriptions
            let curve = tuple.Item1
            let source = tuple.Item2
            let target = tuple.Item3
            let startVector = curve.StartVelocity.Coordinates(surface)
            let endVector = -curve.EndVelocity.Coordinates(surface)
            select new UnorientedStrip(
                curve,
                junctions[source],
                junctions[target],
                EdgePath.Empty,
                this,
                startVector.Angle(),
                endVector.Angle(),
                addToGraph: true
            )
        ).ToList();
        SetMap(edgeDescriptions.ToDictionary(tuple => tuple.Item1.Name, tuple => tuple.Item4), GraphMapUpdateMode.Replace);
        // fix vertex targets
        foreach (var strip in OrientedEdges)
        {
            if (strip.Source.image != null && strip.Source.image != strip.Dg?.Source)
                Debug.LogError(
                    $"Two edges at the same vertex have images that don't start at the same vertex! g({strip.Name}) = {string.Join(' ', strip.EdgePath.Select(e => e.Name))} starts at o(g({strip.Name})) = {strip.Dg?.Source}, but we already set g(o({strip.Name})) = {strip.Source.image}.");
            strip.Source.image ??= strip.Dg?.Source;
        }


        peripheralSubgraph = new FibredGraph(true);
        var peripheralEdgesSet = peripheralEdges.Select(n => n.ToLower()).ToHashSet();
        peripheralSubgraph.AddVerticesAndEdgeRange(
            Strips.Where(e => peripheralEdgesSet.Contains(e.Name) )
        );
    }

    public string Name { get; set; }

    public Color Color
    {
        get => graph.Vertices.First().Color;
        set => Debug.LogError("The graph color should not be set.");
    }

    public bool Copyable => currentAlgorithmRun is null;
    
    IPatchedDrawnsformable IDrawnsformable<IPatchedDrawnsformable>.Copy() => Copy();

    public FibredSurface Copy()
    {
        var newGraph = new FibredGraph(true);
        var newPeripheralSubgraph = new FibredGraph(true);
        var newFibredSurface = new FibredSurface(newGraph, surface, newPeripheralSubgraph) { ignoreBeingReducible = ignoreBeingReducible };

        var newVertices = new Dictionary<Junction, Junction>(
            from oldJunction in graph.Vertices
            select new KeyValuePair<Junction, Junction>(oldJunction, 
                oldJunction.Copy(newFibredSurface)
            )
        );

        var newEdges = new Dictionary<UnorientedStrip, UnorientedStrip>(
            from oldStrip in Strips
            select new KeyValuePair<UnorientedStrip, UnorientedStrip>(oldStrip, 
                oldStrip.CopyUnoriented(newFibredSurface,
                    source: newVertices[oldStrip.Source],
                    target: newVertices[oldStrip.Target]
                )
            )
        );

        foreach (var (oldJunction, newJunction) in newVertices) 
            if (oldJunction.image != null)
                newJunction.image = newVertices[oldJunction.image];

        foreach (var (edge, newEdge) in newEdges)
            newEdge.EdgePath = edge.EdgePath.Replace(strip => newEdges[strip]);

        newGraph.AddVerticesAndEdgeRange(newEdges.Values);

        newPeripheralSubgraph.AddVerticesAndEdgeRange(
            from edge in peripheralSubgraph.Edges select newEdges[edge]
        );

        return newFibredSurface;
    }

    public Dictionary<Strip, EdgePath> ParseMap(string text)
    {
        string[] lines = text.Split(',', '\n');
        var graphMap = new Dictionary<string, string>();
        foreach (string line in lines)
        {
            string trim = line.Trim();
            if (trim == "") 
                continue;
            var match = Regex.Match(trim, @"g\s*\((.+)\)\s*=(.*)");
            if (!match.Success)
                match = Regex.Match(trim, @"(.+)->(.*)");
            if (!match.Success)
                match = Regex.Match(trim, @"(.+)↦(.*)");
            if (!match.Success)
                match = Regex.Match(trim, @"(.+):=(.*)");
            if (!match.Success)
                match = Regex.Match(trim, @"(.+)=(.*)");
            if (!match.Success)
                throw new ArgumentException("The input should be in the form \"g(a) = a B A, g(b) = ...\" or \" a -> a B A, b -> ...\", plus potentially definitions like \"x := a B A\".");

            string name = match.Groups[1].Value.Trim();
            string imageText = match.Groups[2].Value;
            graphMap[name] = imageText;
        }
        return ParseMap(graphMap);
    }

    public Dictionary<Strip, EdgePath> ParseMap(IReadOnlyDictionary<string, string> map)
    {
        var stripNames = Strips.Select(s => s.Name).ToHashSet();
        var definitionNames = map.Keys.Select(k => k.ToLower()).ToHashSet();
        
        var missingNames = stripNames.ToHashSet();
        missingNames.ExceptWith(definitionNames);
        definitionNames.ExceptWith(stripNames);
        
        var edgeDict = OrientedEdges.ToDictionary(e => e.Name);
        var definitions = new List<NamedEdgePath>();
        foreach (var definitionName in definitionNames)
            definitions.Add(new NamedEdgePath(
                    EdgePath.FromString(map[definitionName], Strips, definitions),
                    definitionName
                )
            );

        var result = new Dictionary<Strip, EdgePath>();
        foreach (var (name, edgePathText) in map)
            if (edgeDict.TryGetValue(name, out var strip))
                result[strip] = EdgePath.FromString(edgePathText, Strips, definitions);
        foreach (var missingKey in missingNames) 
            result[edgeDict[missingKey]] = new NormalEdgePath(edgeDict[missingKey]); 
            // identity on the other edges
        return result;
    }

    public void SetMap(string map, GraphMapUpdateMode mode) =>
        SetMap(ParseMap(map), mode);
    
    public void SetMap(IReadOnlyDictionary<string, string> map, GraphMapUpdateMode mode) =>
        SetMap(ParseMap(map), mode);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="enteredMap">This must contain exactly one of {e, e.Reversed()} for any edge of the graph</param>
    /// <param name="mode"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetMap(IReadOnlyDictionary<Strip, EdgePath> enteredMap, GraphMapUpdateMode mode)
    {
        if (enteredMap.Keys.Any(e => e.graph != graph))
            throw new ArgumentException("The edges in the map must be from the same graph as this fibred surface.");
        switch (mode)
        {
            case GraphMapUpdateMode.Replace:
                foreach (var edge in enteredMap.Keys)
                    edge.EdgePath = enteredMap[edge];
                break;
            case GraphMapUpdateMode.Precompose:
                var oldMap = OrientedEdges.ToDictionary(e => e, e => e.EdgePath);
                foreach (var edge in enteredMap.Keys) 
                    edge.EdgePath = enteredMap[edge].Replace(e => oldMap[e]);
                break;
            case GraphMapUpdateMode.Postcompose:
                var fullDict = new Dictionary<Strip, EdgePath>(enteredMap);
                foreach (var (edge, image) in enteredMap) 
                    fullDict[edge.Reversed()] = image.Inverse;
                foreach (var edge in Strips) 
                    edge.EdgePath = edge.EdgePath.Replace(e => fullDict[e]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    #endregion

    #region Algorithm with suggestion system

    public class AlgorithmSuggestion
    {
        
        public AlgorithmSuggestion(){}

        public AlgorithmSuggestion(string description)
        {
            this.description = description;
            this.buttons = new[] { generalSubroutineContinueButton };
            this.options = Array.Empty<(object, string)>();
        }
        
        // TODO: Feature. Add a way to highlight the edges that are selected in the options or even add an animation
        public readonly IReadOnlyCollection<(object, string)> options;
        public readonly string description;
        public readonly IReadOnlyCollection<string> buttons;
        public readonly bool allowMultipleSelection;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options">A list of options, each of which will be displayed as a toggleable button. Each option is of the form (technical option needed for the ApplySuggestions method, (colorful) description for display on the button).</param>
        /// <param name="description">The description for the suggestion at the top of the list.</param>
        /// <param name="buttons">The buttons at the bottom of the list.</param>
        /// <param name="allowMultipleSelection">If one can select multiple options.</param>
        public AlgorithmSuggestion(IEnumerable<(object, string)> options, string description, IEnumerable<string> buttons, bool allowMultipleSelection = false)
        {
            this.options = options as IReadOnlyCollection<(object, string)> ?? options.ToArray();
            this.description = description;
            this.buttons = buttons as IReadOnlyCollection<string> ?? buttons.ToArray();
            this.allowMultipleSelection = allowMultipleSelection;
        }

        public const string peripheralInefficiencyButton = "Fold Selected";
        public const string generalSubroutineContinueButton = "Continue";
        public const string collapseInvariantSubforestContinueButton = "Contract component";
        public const string trainTrackButton = "Convert to train track";
        public const string subforestAtOnceButton = "Collapse at once";
        public const string subforestInStepsButton = "Collapse in steps";
        public const string valence1Button = "Remove valence-one junction";
        public const string inefficiencyAtOnceButton = "Remove at once";
        public const string inefficiencyInStepsButton = "Remove in steps";
        public const string inefficiencyInFineStepsButton = "Remove in fine steps";
        public const string foldMoveButton = "Move";
        public const string foldMoveAltButton = "Move, but ignore the edge along which we move";
        public const string tightenSelectedButton = "Tighten selected";
        public const string tightenAllButton = "Tighten all";
        public const string absorbPAtOnceButton = "Absorb at once";
        public const string absorbPInStepsButton = "Absorb in steps";
        public const string valence2SelectedButton = "Remove Selected";
        public const string valence2AllButton = "Remove All";
        public const string ignoreReducibleButton = "Continue anyways";
    }

    IEnumerator<AlgorithmSuggestion> currentAlgorithmRun;
    IEnumerable<object> selectedOptionsDuringAlgorithmPause; // this is to communicate with the currentAlgorithmicRun.
    string selectedButtonDuringAlgorithmPause; // this is to communicate with the currentAlgorithmicRun.
    public AlgorithmSuggestion NextSuggestion()
    {
        // todo? add a set of ITransformables to highlight with each option?
        // todo? OnHover?
        
        if (currentAlgorithmRun != null)
            return currentAlgorithmRun.Current;
        
        var nextSuggestion = InvariantSubforestsSuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        nextSuggestion = TighteningSuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        nextSuggestion = ValenceOneJunctionsSuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        nextSuggestion = AbsorbIntoPeripherySuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        if (!ignoreBeingReducible)
        {
            nextSuggestion = ReducibilitySuggestion();
            if (nextSuggestion != null)
                return nextSuggestion;
        }

        nextSuggestion = ValenceTwoSuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        nextSuggestion = RemovePeripheralInefficiencySuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        nextSuggestion = RemoveInefficiencySuggestion();
        if (nextSuggestion != null)
            return nextSuggestion;
        
        if (!isTrainTrack /*&& !ignoreBeingReducible*/)
            return new AlgorithmSuggestion(
                options: new (object, string)[] { },
                description: "The algorithm is finished. The graph map is a train track map and we can thus turn the fibred surface into a train track.",
                buttons: new[] { AlgorithmSuggestion.trainTrackButton }
            );
                    
        return new AlgorithmSuggestion(
            options: new (object, string)[] { },
            description: "The algorithm is finished.",
            buttons: new string[] { }
        );
    }

    private bool ignoreBeingReducible;
    private bool isTrainTrack = false;
    public void ApplySuggestion(IEnumerable<object> suggestion, string button)
    {
        switch (button)
        {
            case AlgorithmSuggestion.foldMoveButton:
            case AlgorithmSuggestion.foldMoveAltButton: // continue the algorithmic steps started with AlgorithmSuggestion.inefficiencyInStepsButton
            case AlgorithmSuggestion.generalSubroutineContinueButton:
            case AlgorithmSuggestion.collapseInvariantSubforestContinueButton:
            // TODO: Add the ones for collapsing into Q
                if (currentAlgorithmRun == null)
                    throw new ArgumentException("There is no current algorithmic list to continue.");

                selectedOptionsDuringAlgorithmPause = suggestion;
                selectedButtonDuringAlgorithmPause = button;
                
                // the MoveNext() runs the RemoveInefficiencyInSteps method until the next suggestion is returned or until the end.
                if (!currentAlgorithmRun.MoveNext()) 
                    currentAlgorithmRun = null; // done
                
                selectedOptionsDuringAlgorithmPause = null;
                selectedButtonDuringAlgorithmPause = null;
                break;
            case AlgorithmSuggestion.subforestAtOnceButton:
                if (suggestion.FirstOrDefault() is not IEnumerable<string> subforestEdges)
                    break;
                using (var localAlgorithmicRun = CollapseInvariantSubforest(subforestEdges))
                {
                    while (localAlgorithmicRun.MoveNext())
                    { } // run until the end
                }

                break;
            case AlgorithmSuggestion.subforestInStepsButton:
                if (suggestion.FirstOrDefault() is not IEnumerable<string> subforestEdges2)
                    break;
                // only select one subforest at a time because they might intersect and then the other subforests wouldn't be inside the new graph anymore.
                
                
                currentAlgorithmRun = CollapseInvariantSubforest(subforestEdges2);
                if (!currentAlgorithmRun.MoveNext())
                {
                    currentAlgorithmRun.Dispose();
                    currentAlgorithmRun = null; // if it had just one step 
                }

                break;
            case AlgorithmSuggestion.tightenSelectedButton:
                foreach (var o in suggestion)
                {
                    if (o is not string name)
                    {
                        Debug.LogError($"Weird type of suggestion for Tighten Selected: {o}");
                        continue;
                    }
                    PullTightAll(name);
                }
                break;
            case AlgorithmSuggestion.tightenAllButton:
                PullTightAll();
                break;
            case AlgorithmSuggestion.valence1Button:
                foreach (var o in suggestion)
                    if (o is string name)
                        RemoveValenceOneJunction(graph.Vertices.First(v => v.Name == name));    
                    else
                        Debug.LogError($"Weird type of suggestion for Valence-1 Removal: {o}");
                break;
            case AlgorithmSuggestion.absorbPAtOnceButton:
                if (suggestion.FirstOrDefault() is not FibredGraph QwithoutP)
                    break;
                foreach (var _ in AbsorbIntoPeriphery(QwithoutP))
                {} // run until the end
                break;
            case AlgorithmSuggestion.absorbPInStepsButton:
                if (suggestion.FirstOrDefault() is not FibredGraph QwithoutP2)
                    break;
                currentAlgorithmRun = AbsorbIntoPeriphery(QwithoutP2).GetEnumerator();
                if (!currentAlgorithmRun.MoveNext())
                {
                    currentAlgorithmRun.Dispose();
                    currentAlgorithmRun = null; // if it had just one step 
                }
                break;
            case AlgorithmSuggestion.valence2SelectedButton: // valence 2 junctions
                foreach (var o in suggestion.ToArray())
                {
                    if (o is string name)
                        RemoveValenceTwoJunction(graph.Vertices.First(v => v.Name == name), null);
                    else
                        Debug.LogError($"Weird type of suggestion for Remove Selected Valence-2 Vertex: {o}");
                    
                    // at some point there might be invariant subforests!
                    if (GetInvariantSubforests().Any())
                        break;
                }

                break;
            case AlgorithmSuggestion.valence2AllButton: // valence 2 junctions
                var maxSteps = graph.VertexCount;
                for (int i = 0; i < maxSteps; i++)
                {
                    var junction = GetValenceTwoJunctions().FirstOrDefault();
                    // at some point there might be invariant subforests!
                    if (GetInvariantSubforests().Any())
                        break;
                    
                    if (junction == null) break;
                    RemoveValenceTwoJunction(junction);
                }

                break;
            case AlgorithmSuggestion.peripheralInefficiencyButton: // peripheral inefficiencies
                var firstOption = suggestion.FirstOrDefault();
                if (firstOption is not IEnumerable<string> edges)
                {
                    Debug.LogError($"Weird type of option for Fold Selected: {firstOption}");
                    break;
                }
                var edgeDict = OrientedEdges.ToDictionary(e => e.Name);
                var edgesToFold = edges.Select(name => edgeDict[name]).ToArray();
                RemovePeripheralInefficiency(edgesToFold); // todo!
                break;
            case AlgorithmSuggestion.inefficiencyAtOnceButton: 
                var inefficiency = ExtractInefficiencyFromSuggestion();                
                var algorithmicList = RemoveInefficiencyInSteps(inefficiency, fineSteps: false);
                while (algorithmicList.MoveNext())
                { }
                break;
            case AlgorithmSuggestion.inefficiencyInStepsButton: 
                inefficiency = ExtractInefficiencyFromSuggestion();
                currentAlgorithmRun = RemoveInefficiencyInSteps(inefficiency, fineSteps: false);
                if (!currentAlgorithmRun.MoveNext()) 
                    currentAlgorithmRun = null; // if it had just one step (degree zero, so shouldn't happen)
                break;
            case AlgorithmSuggestion.inefficiencyInFineStepsButton:
                inefficiency = ExtractInefficiencyFromSuggestion();
                currentAlgorithmRun = RemoveInefficiencyInSteps(inefficiency, fineSteps: true);
                if (!currentAlgorithmRun.MoveNext()) 
                    currentAlgorithmRun = null; // if it had just one step (degree zero, so shouldn't happen)
                break;
            case AlgorithmSuggestion.ignoreReducibleButton:
                ignoreBeingReducible = true;
                break;
            case AlgorithmSuggestion.trainTrackButton:
                ConvertToTrainTrack();
                isTrainTrack = true;
                break;
            default:
                HandleInconsistentBehavior("Unknown button.");
                break;
        }
        // sanity tests
        var edgeWithBrokenEdgePath = Strips.FirstOrDefault(s =>
            s.EdgePath.Count != 0 && !Enumerable.Range(0, s.EdgePath.Count - 1)
                .All(i => s.EdgePath[i].Target == s.EdgePath[i + 1].Source));
        if (edgeWithBrokenEdgePath != null)
            HandleInconsistentBehavior($"The edge {edgeWithBrokenEdgePath} has a broken edge path.");
        var brokenSelfEdge = Strips.FirstOrDefault(s => s.Source == s.Target && s.EdgePath.Count == 0);
        if (brokenSelfEdge != null)
            HandleInconsistentBehavior(
                $"The edge {brokenSelfEdge} is a self-edge that gets mapped into a vertex! This should not happen as we assume that the fibred surface is embedded as a deformation retract of the surface and thus no loop should be mapped to a vertex (no non-forest into a forest).");
        var duplicateName = graph.Edges.FirstDuplicate(e => e.Name);
        if (duplicateName != null)
            HandleInconsistentBehavior($"The name of {duplicateName} is used twice.");
        var weirdCurveName = Strips.FirstOrDefault(s => s.Reversed().Curve.Name != s.Name + "'");
        if (weirdCurveName != null)
            HandleInconsistentBehavior($"The curve {weirdCurveName} has a weird name.");
        var brokenVertex = graph.Vertices.FirstOrDefault(v => v.fibredSurface != this);
        if (brokenVertex != null)
            HandleInconsistentBehavior($"The vertex {brokenVertex} doesn't refer to this graph.");
        var brokenVertexMapEdge = BrokenVertexMapEdge();
        if (brokenVertexMapEdge != null)
            HandleInconsistentBehavior($"The edge {brokenVertexMapEdge.Name} starts at {brokenVertexMapEdge.Source} with g({brokenVertexMapEdge.Source}) = {brokenVertexMapEdge.Source.image}, but g({brokenVertexMapEdge.Name}) starts at o(Dg({brokenVertexMapEdge.Name})) = o({brokenVertexMapEdge.Dg?.Name}) = {brokenVertexMapEdge.Dg?.Source}");
        var brokenVertexGraphAssociation = graph.Vertices.FirstOrDefault(v => v.fibredSurface != this);
        if (brokenVertexGraphAssociation != null)
            HandleInconsistentBehavior($"The vertex {brokenVertexGraphAssociation} doesn't refer to this graph.");
        var brokenEdgeGraphAssociation = Strips.FirstOrDefault(e => e.graph != graph);
        if (brokenEdgeGraphAssociation != null)
            HandleInconsistentBehavior($"The edge {brokenEdgeGraphAssociation} doesn't refer to this graph.");
        for (int k = 0; k < 4; k++)
        {
            foreach (var vertex in graph.Vertices)
            {
                var star = StarOrdered(vertex).ToList();
                var starts = (from edge in star select edge.EdgePath.Take(k)).ToHashSet();
                foreach (var start in starts)
                {
                    var edges = (from edge in star where edge.EdgePath.Take(k).SequenceEqual(start) select edge).ToArray();
                    if (!IsConnectedSet(star, edges))
                        HandleInconsistentBehavior($"The edges {string.Join(", ", edges.Select(e => e.Name))} are not connected in the star of {vertex.Name}, but all start with the same edge path {string.Join(" ", start.Select(e => e.Name))}.");
                }
            }
        }
        var brokenJumpPoints = Strips.FirstOrDefault(e => e.Curve.VisualJumpPoints.Count() != e.Curve.VisualJumpTimes.Count());
        if (brokenJumpPoints != null)
            HandleInconsistentBehavior($"The edge {brokenJumpPoints} has a broken jump point.");
        return;

        Inefficiency ExtractInefficiencyFromSuggestion()
        {
            var selectedOption = suggestion.FirstOrDefault();
            if (selectedOption is not string inefficiencySerializationString)
                throw new ArgumentException($"Weird type of suggestion for Remove Inefficiency: {selectedOption}");
            
            return new Inefficiency(EdgePoint.Deserialize(inefficiencySerializationString, OrientedEdges));
        }
    }


    private Strip BrokenVertexMapEdge() => OrientedEdges.FirstOrDefault(e => e.Dg != null && e.Source.image != e.Dg.Source);


    
    public event Action<string> OnError;
    private void HandleInconsistentBehavior(string errorMessage)
    {
        OnError?.Invoke(errorMessage);
    }
    public bool ApplyNextSuggestion()
    {
        var suggestion = NextSuggestion();
        if (suggestion == null) return false; // done!
        ApplySuggestion(from tuple in suggestion.options select tuple.Item1, suggestion.buttons.First()); // selected ones (all!)
        return true;
    }

    public void BestvinaHandelAlgorithm()
    {
        var limit = 20 * graph.EdgeCount;
        for (int i = 0; i < limit; i++)
        {
            if (!ApplyNextSuggestion()) break;
        }
        // todo: save result (reducible / growth)
    }

    #endregion

    #region Graph operations

    public string GraphString()
    {
        var vertexString = graph.Vertices.ToLineSeparatedString(vertex => vertex.ToColorfulString(Gate.FindGates(graph)));
        var stripsOrdered = StripsOrdered.ToList();
        var edgeString = stripsOrdered.ToLineSeparatedString(edge => edge.ToColorfulString());
        var boundaryWords = BoundaryWords().ToHashSet();
        var peripheralBoundaryWords = boundaryWords.Where(w => 
            w.All(e => peripheralSubgraph.ContainsEdge(e.UnderlyingEdge))
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
        where !subgraph.ContainsEdge(edge.UnderlyingEdge)
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
            if (subgraph.ContainsEdge(edge!.UnderlyingEdge))
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
        if (startIndex == -1) HandleInconsistentBehavior("The connected set is not in the star.");
        if (startIndex == 0) startIndex = sortedStar.FindLastIndex(e => !connectedSet.Contains(e)) + 1;
        // if the connected set is all of the star, then this is -1 + 1 = 0.
        return sortedStar.CyclicShift(startIndex).Take(connectedSet.Count);
    }
    private IEnumerable<T> SortConnectedSet<T>(List<T> sortedStar, ICollection<T> connectedSet, out int startIndex)
    {
        startIndex = sortedStar.FindIndex(connectedSet.Contains);
        // this should be the index in the cyclic order where the connected set starts
        if (startIndex == -1) HandleInconsistentBehavior("The connected set is not in the star.");
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

    #endregion

    #region Invariant subforests

    /// <summary>
    /// only subforests whose collapse doesn't destroy the peripheral subgraph, i.e. whose components contain at most one vertex of the peripheral subgraph.
    /// components that are only vertices are missing!
    /// (but that doesn't matter because we only use this in the CollapseInvariantSubforest and MaximalPeripheralGraph methods)
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<FibredGraph> GetInvariantSubforests()
    {
        Dictionary<UnorientedStrip, FibredGraph> subforests = new();

        foreach (var strip in Strips)
        {
            if (subforests.Values.Any(subforest => subforest.Edges.Contains(strip))) continue;
            // there already is a larger subforest containing the one defined from this strip.

            FibredGraph orbitOfEdge = OrbitOfEdgeGraph(strip);

            if (!IsPeripheryFriendlySubforest(orbitOfEdge)) continue;

            foreach (var oldEdge in subforests.Keys.ToArray())
                if (orbitOfEdge.Edges.Contains(oldEdge))
                    subforests.Remove(oldEdge); // the new subforest contains the old one.
            subforests[strip] = orbitOfEdge;
        }

        // todo? See if a union of two subforests is a subforest. If so, we can merge them.
        return subforests.Values;
    }

    public AlgorithmSuggestion InvariantSubforestsSuggestion()
    {
        var a = GetInvariantSubforests();
        if (a.Count == 0) return null;
        return new AlgorithmSuggestion(
            options: from subforest in a
            select (
                subforest.Edges.Select(e => e.Name) as object,
                string.Join(", ", subforest.Edges.Select(e => e.ColorfulName))
            ), 
            description: "Collapse an invariant subforest.", 
            buttons: new[] { AlgorithmSuggestion.subforestAtOnceButton, AlgorithmSuggestion.subforestInStepsButton }
        );
    }

    /// <summary>
    /// Use PreservedSubgraph_Reducibility instead
    /// </summary>
    /// <returns></returns>
    public HashSet<UnorientedStrip> InvariantSubgraph() => 
        Strips.Select(edge => OrbitOfEdge(edge)).FirstOrDefault(
            orbit => orbit.Count < graph.EdgeCount
        );

    /// <summary>
    /// This checks if the graph is a subforest at each component touches the peripheral subgraph in at most one vertex.
    /// Thus if touching and remove are true, this checks if subgraph deformation retracts to the peripheral subgraph.
    /// </summary>
    /// <param name="subgraph"></param>
    /// <param name="peripheralSubgraph">If not present, the saved peripheral subgraph P is taken.</param>
    /// <param name="remove">If remove is true, subgraph is replaced by (subgraph \ peripheralSubgraph)</param>
    /// <param name="touching">If touching is true, each component must touch the peripheral subgraph in exactly one vertex.</param>
    /// <returns></returns>
    bool IsPeripheryFriendlySubforest(FibredGraph subgraph, FibredGraph peripheralSubgraph = null, bool remove = false,
        bool touching = false)
    {
        if (remove)
            return IsPeripheryFriendlySubforest(subgraph.Edges, peripheralSubgraph, false, touching);

        if (!subgraph.IsUndirectedAcyclicGraph()) return false;

        var components = new Dictionary<Junction, int>();
        int numberOfComponents = subgraph.ConnectedComponents(components);
        int[] componentIntersections = new int[numberOfComponents];

        peripheralSubgraph ??= this.peripheralSubgraph;
        foreach (var vertex in peripheralSubgraph.Vertices)
        {
            if (components.TryGetValue(vertex, out var comp))
                componentIntersections[comp]++;
            if (componentIntersections[comp] > 1) return false;
            // one component of the subforest contains more than one vertex of the peripheral subgraph. Collapsing it would destroy the peripheral subgraph.
            // this is what we call "not periphery-friendly"
        }

        return !touching || componentIntersections.All(i => i == 1);
    }

    bool IsPeripheryFriendlySubforest(IEnumerable<UnorientedStrip> edges, FibredGraph peripheralSubgraph = null,
        bool remove = false, bool touching = false)
    {
        peripheralSubgraph ??= this.peripheralSubgraph;

        if (remove)
            return IsPeripheryFriendlySubforest(edges.Except(peripheralSubgraph.Edges), peripheralSubgraph, false,
                touching);

        FibredGraph subgraph = new FibredGraph();
        subgraph.AddVerticesAndEdgeRange(edges);
        return IsPeripheryFriendlySubforest(subgraph, peripheralSubgraph, remove, touching);
    }

    public IEnumerator<AlgorithmSuggestion> CollapseInvariantSubforest(IEnumerable<string> edges)
    {
        var subforest = new FibredGraph(true);
        var edgeDict = OrientedEdges.ToDictionary(e => e.Name);
        subforest.AddVerticesAndEdgeRange(
            from edgeName in edges
            select edgeDict[edgeName].UnderlyingEdge
        );
        return CollapseInvariantSubforest(subforest);
    }

    public IEnumerator<AlgorithmSuggestion> CollapseInvariantSubforest(FibredGraph subforest)
    {
        var subforestEdges = subforest.Edges.ToHashSet();

        var componentList = subforest.ComponentGraphs(out var componentDict);

        var newVertices = new List<Junction>();
        foreach (var component in componentList)
        {
            // Direct Union of the edges and vertices of the component to a vertex that is large (using the "drawables")
            // var newVertex = new Junction(
            //     graph,
            //     drawables: component.Edges.Select(e => e.Curve).Concat<IDrawnsformable>(component.Vertices),
            //     // yes this is component.Patches but component is a FibredGraph, not FibredSurface... 
            //     name: NextVertexName(),
            //     color: NextVertexColor()
            // );
            if (component.EdgeCount == 0)
                continue; // nothing to collapse
            yield return new AlgorithmSuggestion(
                options: from v in component.Vertices.OrderByDescending(
                        v => graph.AdjacentDegree(v)
                    )
                    select (v.Name as object, v.ColorfulName),
                description: $"Pull component {{{component.Edges.ToCommaSeparatedString(e => e.ColorfulName)}}} towards vertex",
                buttons: new[] { AlgorithmSuggestion.collapseInvariantSubforestContinueButton }    
            ); 
            // this pauses execution and asks the user by giving an algorithm suggestion (unless they chose to skip these in-between steps)
            var selectedOption = selectedOptionsDuringAlgorithmPause?.FirstOrDefault();
            Junction newVertex = null;
            if (selectedOption is string selectedVertexName)
                newVertex = component.Vertices.FirstOrDefault(v => v.Name == selectedVertexName);
            newVertex ??= component.Vertices.ArgMax(v => graph.AdjacentDegree(v)).Item1;
            
            newVertices.Add(newVertex);
            
            // ReplaceVertices(SubgraphStarOrdered(component), newVertex);

            if (!component.IsUndirectedAcyclicGraph())
                // would probably give a terrible infinite loop in PullTowardsCenter
                throw new InvalidOperationException(
                    "The component is not a tree. This should not happen as we only call this on invariant subforests.");

            PullTowardsCenter(newVertex, null, 1f);

            // Moves the source of the strip along the strip (removing it) and returns the cyclic order. This is done recursively, edge for edge, so that the strips are parallel but not at the same place.
            IEnumerable<Strip> PullTowardsCenter(Junction junction, Strip stripToCenter, float orderIndexWidth)
            {
                junction ??= stripToCenter.Source;
                var star = StarOrdered(junction, stripToCenter, removeFirstEdgeIfProvided: true).ToList();
                var newStar = new List<Strip>(4 * star.Count);
                
                for (int i = 0; i < star.Count; i++)
                {
                    var edgeToChild = star[i];
                    if (component.ContainsEdge(edgeToChild.UnderlyingEdge))
                    {
                        var orderIndexWidthChild = star[(i + 1) % star.Count].OrderIndexStart - edgeToChild.OrderIndexStart;
                        if (orderIndexWidthChild < 0) // edgeToChild has the highest OrderIndexStart
                            orderIndexWidthChild = 2f;
                        newStar.AddRange(PullTowardsCenter(null, edgeToChild.Reversed(), orderIndexWidthChild));
                    }
                    else
                        newStar.Add(edgeToChild);
                }
                
                if (!newStar.SequenceEqual(StarOrdered(junction, stripToCenter, removeFirstEdgeIfProvided: true)))
                    throw new Exception("The cyclic order or origin map weren't set correctly in PullTowardsCenter. " +
                                        $"The new star {string.Join(", ", StarOrdered(junction, stripToCenter, removeFirstEdgeIfProvided: true).Select(e => e.Name))} is not equal to the expected list of edges {string.Join(", ", newStar.Select(e => e.Name))}.");

                if (stripToCenter == null)
                    return newStar;
                
                MoveJunction(stripToCenter); 
                // this changes all the curves in newStar = StarOrdered(junction, stripToCenter).Skip(1).ToList(),
                // prolonging them at the start by stripToCenter.Reversed().Curve
                // the cyclic order needs to be correct
                // we then have to set the source and order index:

                var scale = orderIndexWidth / newStar.Count;
                foreach (var (index, edge) in newStar.Enumerate())
                {
                    edge.Source = stripToCenter.Target;
                    edge.OrderIndexStart = stripToCenter.OrderIndexEnd + scale * index; 
                    // we needed the orderIndexWidth, so that the edge.OrderIndexStart is in [stripFromCenter.OrderIndexStart, stripFromCenter.NextInCyclicOrder.OrderIndexStart).
                }
                graph.RemoveEdge(stripToCenter.UnderlyingEdge);
                return newStar;
            }
            
            // var orderIndex = 0;
            // foreach (var strip in SubgraphStarOrdered(component).ToList())
            // {
            //     strip.Source = newVertex;
            //     strip.OrderIndexStart = orderIndex++;
            // }
            
            foreach (var junction in component.Vertices)
            {
                if (junction != newVertex)
                    graph.RemoveVertex(junction); 
                // this also removes the edges of component (because they have both ends in component.Vertices, and not both are newVertex) 
            }

        }

        foreach (var strip in Strips)
        {
            strip.EdgePath = strip.EdgePath.Replace(edge => subforestEdges.Contains(edge.UnderlyingEdge) ? null : edge);
        }

        foreach (var junction in graph.Vertices)
        {
            if (junction.image == null) continue;
            if (componentDict.TryGetValue(junction.image, out var index))
                junction.image = newVertices[index];
        }

        for (var index = 0; index < newVertices.Count; index++)
        {
            var newVertex = newVertices[index];
            var absorbedJunction = componentList[index].Vertices.First();
            if (componentDict.TryGetValue(absorbedJunction.image, out var imageIndex))
                newVertex.image = newVertices[imageIndex];
            else
                newVertex.image = absorbedJunction.image;

            graph.AddVertex(newVertex); 
            // shouldn't be necessary because we assign edge.Source for edges in the subgraph star (if that is empty, then our entire graph was a tree, i.e. the surface was a disk, which is too trivial)
        }
    }


    #endregion

    #region Valence one and two junctions

    public IEnumerable<Junction> GetValenceOneJunctions() =>
        from vertex in graph.Vertices where Star(vertex).Count() == 1 select vertex;
    
    
    public AlgorithmSuggestion ValenceOneJunctionsSuggestion()
    {
        var options = (
            from v in GetValenceOneJunctions()
            select (v.Name as object, v.ColorfulName)
        ).ToArray(); 
        if (options.Length == 0) return null;
        return new AlgorithmSuggestion(
            options: options,
            description: "Remove a valence-one junction. WHAT THE HECK?", buttons: new[] { AlgorithmSuggestion.valence1Button },
            allowMultipleSelection: true
        );
    }

    public void RemoveValenceOneJunction(Junction junction)
    {
        var star = Star(junction).ToArray();
        if (star.Length != 1) throw new($"Supposed valence-one junction has valence {star.Length}");
        var removedStrip = star.First();
        Junction otherJunction = removedStrip.Target;
        graph.RemoveVertex(junction);
        graph.RemoveEdge(removedStrip.UnderlyingEdge);
        foreach (var strip in Strips)
            strip.EdgePath = strip.EdgePath.Replace(edge => Equals(edge.UnderlyingEdge, removedStrip.UnderlyingEdge) ? null : edge); 
        foreach (var vertex in graph.Vertices)
            if (vertex.image == junction)
                vertex.image = otherJunction;
        // todo: isotopy or make the junction bigger to include the removed strip
    }

    public IEnumerable<Junction> GetValenceTwoJunctions() =>
        from vertex in graph.Vertices 
        let star = Star(vertex)
        where star.Count() == 2 && !star.First().Equals(star.Last().Reversed()) // no self-edge
        select vertex;

    public AlgorithmSuggestion ValenceTwoSuggestion()
    {
        var valenceTwoJunctions = GetValenceTwoJunctions().ToArray();
        if (valenceTwoJunctions.Length <= 0) return null;
        return new AlgorithmSuggestion(
            options: from v in valenceTwoJunctions select (v.Name as object, v.ToColorfulString()),
            description: "Remove a valence-two junction.", 
            buttons: new[] { AlgorithmSuggestion.valence2SelectedButton, AlgorithmSuggestion.valence2AllButton },
            allowMultipleSelection: true
        );
    }

    /// <summary>
    /// Removes a valence two junction by keeping one of the two strips and make it replace the concatenation of the two strips.
    /// The strip to be removed is chosen as the one in the prePeriphery, or the one with the larger width in the Frobenius-Perron eigenvector.
    /// </summary>
    /// <param name="junction"></param>
    /// <param name="removeStrip"></param>
    public void RemoveValenceTwoJunction(Junction junction, Strip removeStrip = null)
    {
        var star = Star(junction).ToArray();
        if (star.Length != 2) throw new($"Supposed valence-two junction has valence {star.Length}");
        if (star[0].Equals(star[1].Reversed()))
            throw new Exception("Cannot remove a valence-two junction that has a self-edge");

        if (removeStrip == null)
        {
            var prePeriphery = PrePeriphery();
            if (prePeriphery.Contains(star[0].UnderlyingEdge)) removeStrip = star[0];
            else if (prePeriphery.Contains(star[1].UnderlyingEdge)) removeStrip = star[1];
            else
            {
                FrobeniusPerron(true, out var λ, out var widths, out var lengths, out var matrix);
                removeStrip = widths[star[0].UnderlyingEdge] > widths[star[1].UnderlyingEdge] ? star[0] : star[1];
            }
        }

        Strip keptStrip = Equals(star[0].UnderlyingEdge, removeStrip.UnderlyingEdge) ? star[1] : star[0];
        Junction enlargedJunction = removeStrip.Source == junction ? removeStrip.Target : removeStrip.Source;
        // two options: a) isotope along removeStrip to move junctions that map there to the "enlargedJunction" (but don't actually enlarge it)
        // and enlarge keptStrip to include the removed strip: keptStrip' = removedStrip.Reverse u removeJunction u keptStrip.
        // for better visuals (fewer curves in junctions), we start with this option
        // or: b) include removedStrip and junction into the enlargedJunction and isotope along f(removeStrip), so that it lands in f(enlJun).
        // This isotopy could be faked pretty well! Just display f(keptStrip) as f(removedStrip).Reverse u f(removeJunction) u f(keptStrip)
        // and f(enlJun) as what it was before enlarging.
        // In both cases, the same thing happens combinatorially
        var name = keptStrip.Name; 
        if (name.ToLower().StartsWith(removeStrip.Name.ToLower()))
            name = removeStrip.Name;
        var newStrip = keptStrip.Copy(name: name,
            source: enlargedJunction,
            orderIndexStart: removeStrip.OrderIndexEnd,
            curve: removeStrip.Curve.Reversed().Concatenate(keptStrip.Curve),
            edgePath: removeStrip.EdgePath.Inverse.Concat(keptStrip.EdgePath)
        );
        newStrip.Color = keptStrip.Color;

        graph.RemoveVertex(junction); // removes the old edges as well
        graph.AddVerticesAndEdge(newStrip.UnderlyingEdge);

        foreach (var strip in Strips)
            strip.EdgePath = strip.EdgePath.Replace(e => 
                    Equals(e.UnderlyingEdge, removeStrip.UnderlyingEdge) ?
                        null : 
                        e.Equals(keptStrip) ?
                            newStrip :
                            e.Equals(keptStrip.Reversed()) ?
                                newStrip.Reversed() :
                                e
                );
        foreach (var vertex in graph.Vertices)
            if (vertex.image == junction)
                vertex.image = enlargedJunction;
    }

    #endregion

    #region Pulling tight

    /// <summary>
    /// These are the positions where the graph map is not tight, so we can pull tight here.
    /// We have to assume that there are no invariant subforests because the isotopy would touch them!
    /// </summary>
    public IEnumerable<(Strip, EdgePoint[], Junction[])> GetLoosePositions() =>
        from strip in OrientedEdges
        let backTracks = GetBackTracks(strip).ToArray()
        let extremalVertices = GetExtremalVertices(strip).ToArray()
        where backTracks.Length != 0 || extremalVertices.Length != 0
        select (strip, backTracks, extremalVertices);

    public (Strip, EdgePoint[], Junction[]) GetLoosePositions(Strip strip)
    {
        var backTracks = GetBackTracks(strip).ToArray();
        var extremalVertices = GetExtremalVertices(strip).ToArray();
        return (strip, backTracks, extremalVertices);
    }

    public AlgorithmSuggestion TighteningSuggestion()
    {
        var options = (
            from loosePosition in GetLoosePositions()
            select (loosePosition.Item1.Name as object, PullTightString(loosePosition))
        ).ToArray();
        if (options.Length == 0) return null;
        return new AlgorithmSuggestion(
            options, 
            description: "Pull tight at one or more edges.",
            buttons: new[] { AlgorithmSuggestion.tightenAllButton, AlgorithmSuggestion.tightenSelectedButton }, 
            allowMultipleSelection: true
        );
    }

    private static string PullTightString((Strip, EdgePoint[], Junction[]) loosePositions) =>
        $"In edge {loosePositions.Item1.ColorfulName} there are the {loosePositions.Item2.Length} backtracks " +
        loosePositions.Item2.Select(edgePoint => edgePoint.ToShortString(3, 2, colorful: true)).Take(50).ToCommaSeparatedString() + 
        (loosePositions.Item2.Length > 50 ? "..." : "") +
        " and " + (loosePositions.Item3.Length > 0 ? "the" : "no") +
        (loosePositions.Item3.Length > 1 ? loosePositions.Item3.Length + " extremal vertices " : " extremal vertex ") +
        loosePositions.Item3.Take(50).ToCommaSeparatedString(v => v.ToColorfulString()) +
        (loosePositions.Item3.Length > 50 ? "..." : "");

    IEnumerable<Junction> GetExtremalVertices(Strip edge = null)
    {
        if (edge != null)
            return from vertex in graph.Vertices
                let star = Star(vertex)
                // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
                where star.Any() && star.All(strip => Equals(strip.Dg, edge))
                select vertex;
        return from vertex in graph.Vertices
            let star = Star(vertex)
            let firstOutgoingEdge = star.FirstOrDefault()
            // only null if vertex has valence 0, but then the graph is only a vertex and the surface is a disk.
            where firstOutgoingEdge != null && firstOutgoingEdge.Dg != null &&
                  star.All(strip => Equals(strip.Dg, firstOutgoingEdge.Dg))
            select vertex;
    }

    IEnumerable<EdgePoint> GetBackTracks(Strip edge = null)
    {
        // FirstOrDefault() gets called > 300 times on this in a typical call to PullTightAll (takes > 1 second) 
        if (edge != null)
            return from edgePoint in GetBackTracks()
                where Equals(edgePoint.DgAfter(), edge)
                select edgePoint;

        return from strip in Strips
            where strip.EdgePath.Count > 1
            from i in Enumerable.Range(1, strip.EdgePath.Count - 1)
            // only internal points: Valence-2 extremal vertices are found in parallel anyways.
            let edgePoint = new EdgePoint(strip, i)
            // gets called > 20000 times in a typical call to PullTightAll (takes > 1 second)
            where Equals(edgePoint.DgBefore(), edgePoint.DgAfter())
            select edgePoint; 
    }

    private void PullTightExtremalVertex(Junction vertex)
    {
        vertex.image = null;
        foreach (var strip in Star(vertex))
        {
            strip.EdgePath = strip.EdgePath.Skip(1);
            vertex.image ??= strip.Dg?.Source;
            // for self-loops, this takes one from both ends.
        }
        // isotopy: Move vertex and shorten the strips (only the homeomorphism is changed, not the graph)
        // todo? update EdgePoints?
    }

    private void PullTightBackTrack(EdgePoint backTrack, IList<EdgePoint> updateEdgePoints = null)
    {
        updateEdgePoints ??= new List<EdgePoint>();
        if (!Equals(backTrack.DgBefore(), backTrack.DgAfter()))
        {
            Debug.LogWarning($"Assumed Backtrack at {backTrack} is not a backtrack (anymore)!");
            return;
        }

        var strip = backTrack.edge;
        var i = backTrack.index;
        if (i == 0)
        {
            PullTightExtremalVertex(strip.Source);
            return;
        }

        strip.EdgePath = strip.EdgePath.Take(i - 1).Concat(strip.EdgePath.Skip(i + 1));

        for (int k = 0; k < updateEdgePoints.Count; k++)
        {
            var j = updateEdgePoints[k].AlignedIndex(strip, out var reverse);
            if (j < i) continue;
            var res = j == i ? new EdgePoint(strip, j - 1) : new EdgePoint(strip, j - 2);
            updateEdgePoints[k] = reverse ? res.Reversed() : res;
        }

        // todo? isotopy to make the strip shorter
    }


    public void PullTightAll(string edgeName) => 
        PullTightAll(OrientedEdges.FirstOrDefault(e => e.Name == edgeName));

    public void PullTightAll(Strip edge = null)
    {
        var limit = Strips.Sum(e => e.EdgePath.Count) + graph.Vertices.Count();
        for (int i = 0; i < limit; i++)
        {
            var extremalVertex = GetExtremalVertices(edge).FirstOrDefault();
            if (extremalVertex != null)
            {
                PullTightExtremalVertex(extremalVertex);
                continue;
            }

            var backTrack = GetBackTracks(edge).FirstOrDefault();
            if (backTrack != null)
            {
                PullTightBackTrack(backTrack);
                continue;
            }

            break;
        }
    }

    #endregion

    #region Moving Vertices

    class MovementForFolding
    {
        public readonly IList<Strip> edges;
        public readonly Strip preferredEdge;
        public readonly IReadOnlyDictionary<Junction, IEnumerable<(string, bool)>> vertexMovements; // the vertices that are folded and how they are moved
        public readonly int l; // number of side crossings along f that the resulting edge will have
        private readonly List<(string, bool)> c;
        private int badness = -1;
        private readonly Dictionary<Strip,int> edgeCancellations;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="edges">edges to fold as ordered in the cyclic order of the star</param>
        /// <param name="preferredEdge"></param>
        /// <param name="l"></param>
        /// <exception cref="ArgumentException"></exception>
        public MovementForFolding(IList<Strip> edges, Strip preferredEdge, int l)
        {
            this.edges = edges; 
            this.preferredEdge = preferredEdge;
            this.l = l;
            
            c = preferredEdge.Curve.SideCrossingWord.Take(l).ToList();
            if (c.Count < l)
                throw new ArgumentException($"The edge {preferredEdge} has not enough side crossings: {c.Count} < {l}", nameof(preferredEdge));

            edgeCancellations =
                edges.ToDictionary(edge => edge, edge => edge.Curve.SideCrossingWord.SharedInitialSegment(c));
            
            vertexMovements = new Dictionary<Junction, IEnumerable<(string, bool)>>(
                from edge in edges
                let sharedInitialSegment= edgeCancellations[edge]
                select new KeyValuePair<Junction, IEnumerable<(string, bool)>>(
                    edge.Target, // vertex
                    edge.Curve.SideCrossingWord.Skip(sharedInitialSegment).Inverse()
                        .Concat( c.Skip(sharedInitialSegment) )
                    // the movement of the vertex as a word in the sides of the model surface that it crosses
                    // with the skips this Concat is exactly ConcatWithCancellation
                )
            );
            
        }

        public int Badness {
            get
            {
                if (badness != -1)
                    return badness;
                
                var count = 0;
                foreach (var edge in edges.First().graph.Edges)
                {
                    if (edges.Contains(edge)) continue;
                    
                    var sideCrossingWord = edge.Curve.SideCrossingWord;
                    if (vertexMovements.TryGetValue(edge.Source, out var movement)) 
                        sideCrossingWord = movement.Inverse().ConcatWithCancellation(sideCrossingWord);
                    if (vertexMovements.TryGetValue(edge.Target, out var movement2))
                        sideCrossingWord = sideCrossingWord.ConcatWithCancellation(movement2);
                    count += sideCrossingWord.Count();
                }

                count += l; // l is the number of side crossings along the edge that results from folding
                badness = count;
                return count;
            }
        }

        public void MoveVerticesForFolding(bool removeEdges = false, bool ignoreGivenEdges = false) 
        {
            var fibredSurface = edges.First().fibredSurface;
            // shorten the preferred curve if necessary
            var preferredCurve = preferredEdge.Curve;
            var (t0l, t1l) = preferredCurve.VisualJumpTimes.Prepend(0f).Skip(l);
            var timeFEnd = (t1l + t0l) / 2; 
            if (t1l == 0) // preferredCurve.VisualJumpTimes has only l elements, i.e. the curve ends before crossing another side
                timeFEnd = preferredCurve.Length;

            if (timeFEnd < preferredCurve.Length)
                fibredSurface.MoveJunction(preferredEdge.Reversed(), preferredCurve.Length - timeFEnd);
            preferredCurve = preferredEdge.Curve; // = preferredCurve.Restrict(0, timeFEnd);
            
            var stringWidths = (
                from edge in edges 
                select baseShiftStrength * MathF.Sqrt(Star(edge.Target).Count()) * Mathf.Clamp(edge.Curve.Length, 0.01f, 1f)
            ).ToArray();
            var preferredEdgeIndex = edges.IndexOf(preferredEdge);
            var edgeIndex = -1;

            foreach (var edge in edges)
            {
                edgeIndex++;
                if (Equals(edge, preferredEdge))
                    continue;
                var vertex = edge.Target;
                var backwardsCurve = edge.Curve.Reversed();
                
                
                var sharedSegment = edgeCancellations[edge];
                var n = backwardsCurve.SideCrossingWord.Count() - sharedSegment; 
                // pull back the vertex for n steps (through n sides) along the edge.
                // Then the edge agrees with the shared initial segment with the preferred edge.
            
                var timeX = 0f; // the time along the backwards curve "close" to the preferred edge, where the edges attached to the vertex will turn to the preferred edge
                if (n > 0)
                {
                    var (t0x, t1x) = backwardsCurve.VisualJumpTimes.Skip(n - 1);
                    if (t1x == 0) // backwardsCurve.VisualJumpTimes has only n elements, i.e. the curve ends before crossing another side (sharedSegment = 0)
                        t1x = backwardsCurve.Length;
                    timeX = (t0x + 2 * t1x) / 3; // a bit closer to the last side crossing along the original edge that is shared with the preferred edge 
                    // todo: fixed distance to last crossing (or the source vertex if there is no crossing)? Shorter for the preferred edge in that case for better visuals?

                    fibredSurface.MoveJunction(vertex, backwardsCurve, timeX, edge.Reversed(), ignoreGivenEdge: ignoreGivenEdges, movingAlongGivenEdge: true);
                    // moves along backwardsCurve and shortens the edge itself (unless we removed it)
                }

                var (t0, t1) = preferredCurve.VisualJumpTimes.Prepend(0f).Skip(sharedSegment); 
                // the same as the Skip(sharedSegment - 1) but with the first element 0 for the case sharedSegment = 0
                var timeF = (t0 * 3 + t1) / 4; // a bit closer to the last side crossing along the preferred edge that is shared with the original edge 
                if (t1 == 0) // preferredCurve.VisualJumpTimes has only sharedSegment elements, i.e. the curve ends before crossing another side (l = sharedSegment = length of preferredCurve.VisualJumpPoints)
                    timeF = preferredCurve.Length;

                Curve restCurve = null;
                Point timeFPoint = preferredCurve[timeF];
                if (l > sharedSegment)
                {
                    float relativeShiftStrength;
                    if (edgeIndex < preferredEdgeIndex)
                        relativeShiftStrength = stringWidths[edgeIndex] / 2f + stringWidths[(edgeIndex + 1)..preferredEdgeIndex].Sum();
                    else 
                        relativeShiftStrength = - stringWidths[edgeIndex] / 2f - stringWidths[(preferredEdgeIndex + 1)..edgeIndex].Sum();
                        
                    restCurve = relativeShiftStrength != 0
                        ? new ShiftedCurve(preferredCurve.Restrict(timeF), relativeShiftStrength, ShiftedCurve.ShiftType.FixedEndpoint)
                        : preferredCurve.Restrict(timeF);
                    timeFPoint = restCurve.StartPosition;
                }
                
                // turning from the backwards curve to the preferred curve
                var intermediateCurve = fibredSurface.GetBasicGeodesic(
                    backwardsCurve[timeX],
                    timeFPoint,
                    "intermediate");

                Curve forwardMovementCurve = intermediateCurve;
                if (restCurve != null)
                    forwardMovementCurve = new ConcatenatedCurve(new[]
                    {
                        intermediateCurve,
                        restCurve
                    }, smoothed: true);

                
                if (forwardMovementCurve.Length > 1e-3) 
                    fibredSurface.MoveJunction(vertex, forwardMovementCurve, forwardMovementCurve.Length, edge.Reversed(), ignoreGivenEdge: ignoreGivenEdges, movingAlongGivenEdge: false); 
                // this prolongs the edge (unless we removed it) 
                
                // todo: addedShiftStrength positive or negative depending on the direction of the edge and the cyclic order.
                // More than one (better: add up the widths) if several vertices are moved along this edge, so that all of the edges are disjoint (hard)
                
                if (removeEdges)
                    fibredSurface.graph.RemoveEdge(edge.UnderlyingEdge);
            }
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.AppendLine($"Preferred edge: {preferredEdge.Name} - Keep the first {l}/{preferredEdge.Curve.SideCrossingWord.Count()} side crossings.");
            // sb.AppendLine($"  {string.Join(", ", c)}");
            sb.AppendLine($"Vertex movements to converge at {preferredEdge.Target.Name}");
            foreach (var (vertex, movement) in vertexMovements)
                if (movement.Any())
                    sb.AppendLine($"Move vertex {vertex} across {(from sideCrossing in movement select sideCrossing.Item1 + (sideCrossing.Item2 ? "'" : "")).ToCommaSeparatedString()}");
            sb.AppendLine($"Total number of side crossings afterwards: {Badness}");
            return sb.ToString(); 
        }
    }

    public void MoveJunction(Strip e, float? length = null) => MoveJunction(e.Source, e.Curve, length ?? e.Curve.Length, e);

    
    const float baseShiftStrength = 0.07f;

    public enum MoveJunctionShiftType
    {
        ToTheRight,
        ToTheLeft,
        Symmetric
    }
    public void MoveJunction(Junction v, Curve curve, float length, Strip e = null, bool ignoreGivenEdge = false, bool movingAlongGivenEdge = true, MoveJunctionShiftType shiftType = MoveJunctionShiftType.Symmetric) 
    {
        v.Patches = new []{ curve[length] };
        
        var precompositionCurve = curve.Restrict(0, length).Reversed();
        var star = StarOrdered(v, e, removeFirstEdgeIfProvided: ignoreGivenEdge || movingAlongGivenEdge ).ToList();
        if (e != null && !ignoreGivenEdge && movingAlongGivenEdge)
        {
            var name = e.Name;
            e.Curve = e.Curve.Restrict(length);
            e.Name = name;
        }
 
        float shift = baseShiftStrength / MathF.Sqrt(star.Count) * Mathf.Clamp(precompositionCurve.Length, 0.01f, 1f);
        
        for (var i = 0; i < star.Count; i++)
        {
            var edge = star[i];

            var shiftStrength = shift * shiftType switch
            {   
                MoveJunctionShiftType.ToTheRight => i / 2f,
                MoveJunctionShiftType.ToTheLeft => -i / 2f,
                MoveJunctionShiftType.Symmetric => (star.Count - 1f) / 2f - i
            };
            var shiftedCurve = shiftStrength != 0 ?
                new ShiftedCurve(precompositionCurve, shiftStrength, ShiftedCurve.ShiftType.SymmetricFixedEndpoints)
                : precompositionCurve;

            var name = edge.Name;
            edge.Curve = new ConcatenatedCurve(new[]
            {
                shiftedCurve,
                // surface.GetGeodesic(shiftedCurve.EndPosition, edge.Curve.StartPosition, ""),
                edge.Curve
            }) { Color = edge.Color }; // smooth?
            edge.Name = name;
        }
        
    }

    
    #endregion
    
    #region Folding initial segments

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
                if (!Equals(edge.UnderlyingEdge, newEdge.UnderlyingEdge) && graph.ContainsEdge(edge.UnderlyingEdge))
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

    #endregion

    #region (Essential) Inefficiencies

    private static IEnumerable<(Strip, Strip)> InefficientConcatenations<T>(IEnumerable<Gate<T>> gates)
    {
        foreach (var gate in gates)
        {
            var edges = gate.Edges;
            for (var i = 0; i < edges.Count; i++)
            for (var j = i; j < edges.Count; j++)
                yield return (edges[i].Reversed(), edges[j]);
        }
    }

    public IEnumerable<Inefficiency> GetInefficiencies()
    {
        var gates = Gate.FindGates(graph);

        foreach (var (edge1, edge2) in InefficientConcatenations(gates))
        {
            var (edge1Rev, edge2Rev) = (edge1.Reversed(), edge2.Reversed());
            foreach (var strip in Strips)
            {
                for (int i = 1; i < strip.EdgePath.Count; i++)
                {
                    if (
                        (!strip.EdgePath[i - 1].Equals(edge1) || !strip.EdgePath[i].Equals(edge2)) && 
                        (!strip.EdgePath[i - 1].Equals(edge2Rev) || !strip.EdgePath[i].Equals(edge1Rev))
                    ) continue;
                    yield return new Inefficiency(new EdgePoint(strip, i));
                    goto found;
                }
            }
            // todo: valence two vertices with only two gates

            found: ;
        }
        
        // foreach (var edge in Strips) // inefficient!!
        // {
        //     for (int i = 0; i <= edge.EdgePath.Count; i++)
        //     {
        //         var inefficiency = CheckEfficiency(edge[i], gates);
        //         if (inefficiency != null)
        //         {
        //             if (i == 0 || i == edge.EdgePath.Count)
        //                 Debug.Log("Interpreted a valence-two gate-wise extremal vertex as an inefficiency.");
        //             yield return inefficiency;
        //         }
        //     }
        // }
    }

    AlgorithmSuggestion RemoveInefficiencySuggestion()
    {
        var f = GetInefficiencies().ToArray();
        if (f.Length == 0) return null;
        return new AlgorithmSuggestion(
            options: from inefficiency in f.OrderBy(inefficiency =>
                inefficiency.order + (inefficiency.FullFold ? 0 : 0.5f))
                select (inefficiency.ToSerializationString() as object, inefficiency.ToColorfulString()),
            description: "Remove an inefficiency.",
            buttons: new[] { AlgorithmSuggestion.inefficiencyAtOnceButton, AlgorithmSuggestion.inefficiencyInStepsButton, AlgorithmSuggestion.inefficiencyInFineStepsButton }
        );
    }

    [CanBeNull]
    public Inefficiency CheckEfficiency(EdgePoint edgePoint, List<Gate<Junction>> gates)
    {
        var a = edgePoint.DgBefore();
        var b = edgePoint.DgAfter();
        if (a == null || b == null) return null; // the edgePoints is actually a vertex of valence other than two

        if (!Equals(a.Source, b.Source))
            throw new($"The edge path is not an actual edge path at {edgePoint}!");
        if (!FindGate(a).Edges.Contains(b)) return null; // not inefficient

        return new Inefficiency(edgePoint);

        Gate<Junction> FindGate(Strip edge) => gates.First(gate => gate.Edges.Contains(edge));
        // The gates should form a partition of the set of oriented edges, so this should be well-defined.
    }

    /// <summary>
    /// At this point, we assume that the fibred surface is irreducible, i.e. that all inefficiencies are in H and that there are no
    /// valence-one junctions or edges that get mapped to junctions.
    /// </summary>
    public IEnumerator<AlgorithmSuggestion> RemoveInefficiencyInSteps(Inefficiency p, bool fineSteps = false)
    {
        if (p.order == 0)
        {
            // The inefficiency is a back track or a valence two extremal vertex
            if (fineSteps)
                yield return new AlgorithmSuggestion(PullTightString(GetLoosePositions(p.DgAfter())));
            PullTightAll(p.DgAfter());
            yield break;
        }

        var initialSegment = p.initialSegmentToFold;
        var edgesToFold = p.edgesToFold;

        while (true)
        {
            var edgeWithProblematicSplitPoint = edgesToFold.FirstOrDefault(edge =>
                Equals(p, new EdgePoint(edge, initialSegment))
            );
            if (edgeWithProblematicSplitPoint == null) break;
            if (fineSteps) // todo: feature. better text here
                yield return new AlgorithmSuggestion($"Decrease the length of the folded initial segment ({initialSegment} -> {initialSegment - 1}) because else the subdivision {edgeWithProblematicSplitPoint[initialSegment].ToColorfulString()} equals the inefficiency point at {p.ToShortString(colorful: true)}.");
            Debug.Log($"When folding the inefficiency (illegal turn) \"{p}\" we had to decrease the initial segment because the point where the illegal turn happened was the same as the subdivision / folding point");
            initialSegment--;
        }

        var updateEdgePoints = new List<EdgePoint>() { p };

        if (initialSegment == 0)
        {
            // the special case when one of the split points is at the point p
            // Split the edge c = Dg^{p.order}(p.edge) = Dg(p.edgesToFold[*]) at the first edge point.
            // If there is none, split g(c) = Dg(c) at the first edge point. If there is none ...
            var c = edgesToFold[0].Dg;
            var edgesToSplit = new List<Strip> { c };
            while (edgesToSplit[0].EdgePath.Count == 1 && edgesToSplit.Count <= 2 * graph.EdgeCount)
                edgesToSplit.Insert(0, edgesToSplit[0].Dg);
            if (edgesToSplit[0].EdgePath.Count == 1)
            {
                HandleInconsistentBehavior(
                    "Weird: This edge only gets mapped to single edges under g. This shouldn't happen at this stage unless f is efficient and permutes the edges, g is a homeo, the growth is λ=1 and f is periodic. But it isn't efficient because we are in this method.");
                yield break;
            }
            if (fineSteps) 
                yield return new AlgorithmSuggestion(
                    $"Split the edge c = {c!.ColorfulName}" + 
                        ( edgesToSplit.Count <= 1 ? "" :
                            " and its iterates " + edgesToSplit.Skip(1).Select(e => e.ColorfulName).ToCommaSeparatedString()
                        ) + " to shorten the initial segment to fold.");
            updateEdgePoints.AddRange(edgesToFold.Select(edge => new EdgePoint(edge, 0)));

            foreach (var edge in edgesToSplit) // we split the last edge first, so that we can split the one before and so on.
                SplitEdge(edge[1], updateEdgePoints);

            edgesToFold = updateEdgePoints.Skip(1).Select(edgePoint => edgePoint.edge).ToList();
            initialSegment = 1;
            // this relies on the EdgePath of these edges having updated during the split of the edge c. 
            // They started with edge.EdgePath = c,..., now they have edge.EdgePath = c1,c2,...
        }

        foreach (var suggestion in FoldInitialSegmentInSteps(edgesToFold, initialSegment, updateEdgePoints))
        {
            if (fineSteps)
                yield return suggestion;
        }

        // Now p is an inefficiency of degree one lower.

        var pNew = updateEdgePoints[0];
        var newInefficiency = CheckEfficiency(pNew, Gate.FindGates(graph));

        if (newInefficiency == null || newInefficiency.order != p.order - 1)
           throw new Exception($"Bug: The inefficiency was not turned into an efficiency of order one less: {p} was turned into {newInefficiency ?? pNew}");
        
        if (newInefficiency.order > 0)
            yield return new AlgorithmSuggestion($"Remove inefficiency {newInefficiency}");
        
        var subEnumerator = RemoveInefficiencyInSteps(newInefficiency, fineSteps);
        while (subEnumerator.MoveNext())
            if (fineSteps)
                yield return subEnumerator.Current;
    }

    #endregion

    #region Absorbing into the periphery

    FibredGraph GetMaximalPeripheralSubgraph()
    {
        var Q = new FibredGraph(true);
        Q.AddVerticesAndEdgeRange(peripheralSubgraph.Edges);
        foreach (var strip in Strips)
        {
            if (Q.Edges.Contains(strip)) continue;
            var orbit = OrbitOfEdge(strip);
            if (IsPeripheryFriendlySubforest(orbit, Q, true, true))
                // this means that after adding the orbit to Q, it still deformation retracts onto Q (thus P).
                Q.AddVerticesAndEdgeRange(orbit);
        }

        return Q;
    }

    public AlgorithmSuggestion AbsorbIntoPeripherySuggestion()
    {
        var Q = GetMaximalPeripheralSubgraph();
        var valenceTwoVertices = Q.Vertices.Where(v => Star(v).Count() <= 2).ToArray();
        var starQ = SubgraphStar(Q).ToList();
        
        Q.RemoveEdges(peripheralSubgraph.Edges);
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
        
        var nonMaximalStrips = starQ.Where(strip => Q.ContainsEdge(strip.Dg!.UnderlyingEdge)).ToList();
        if (nonMaximalStrips.Count > 0)
        {
            if (sb.Length > 0) sb.Append(".\n");
            string qName = (Q.EdgeCount > 0 ? "Q" : "P");
            sb.Append($"The following edges in Star({qName}) start with edges in {qName}: ");
            sb.AppendJoin(", ", nonMaximalStrips.Select(e => e.ColorfulName));
        }

        return new AlgorithmSuggestion(
            description: "Absorb into the periphery",
            options: new[] { ((object)Q, sb.ToString()) }, 
            buttons: new[] { AlgorithmSuggestion.absorbPAtOnceButton, AlgorithmSuggestion.absorbPInStepsButton }
        );
    }

    public IEnumerable<AlgorithmSuggestion> AbsorbIntoPeriphery(FibredGraph QwithoutP)
    {

        // var trees = QwithoutP.ComponentGraphs(out var componentDict);
        
        // collapse the trees exactly like collapsing subforests!!
        using var enumerator = CollapseInvariantSubforest(QwithoutP);
        while (enumerator.MoveNext())
            yield return enumerator.Current;
        
        
        var oldPeripheralEdges = peripheralSubgraph.Edges.ToHashSet();
        var oldPeripheralVertices = peripheralSubgraph.Vertices.ToHashSet();
        
        var peripheralBoundaryWords = BoundaryWords().Where(w =>
            w.All(e => oldPeripheralEdges.Contains(e.UnderlyingEdge))
        ).ToArray();
        
        var previousEdgeOld = new Dictionary<Junction, Strip>(graph.VertexCount); 
        var nextEdgeOld = new Dictionary<Junction, Strip>(graph.VertexCount);
        
        var componentDict = new Dictionary<Junction, int>();
        for (int i = 0; i < peripheralBoundaryWords.Length; i++)
        {
            var lastEdge = peripheralBoundaryWords[i].LastOrDefault();
            foreach (var e in peripheralBoundaryWords[i])
            {
                componentDict[e.Source] = i;
                previousEdgeOld[e.Source] = lastEdge.Reversed();
                nextEdgeOld[e.Source] = e;
                lastEdge = e;
            }
        }
        var gates = Gate.FindGates(graph, vertex => componentDict.GetValueOrDefault(vertex, -1)); // this groups together gates from different vertices outside of P, but we don't care about them anyways.
        // takes a St(Q) edge and gives the original linear order of the star of its source vertex (component of Q \ P)
        var linearStars = new Dictionary<Strip, List<Strip>>(graph.EdgeCount); 
        var gatesOrderedByComponent = new List<List<Gate<int>>>(peripheralBoundaryWords.Length);
        
        var componentStars = new List<List<Strip>>(peripheralBoundaryWords.Length);
        var newVertices = new Dictionary<Gate<int>, Junction>(gates.Count);
        var nextEdgeNew = new Dictionary<Junction, UnorientedStrip>(gates.Count);
        var previousEdgeNew = new Dictionary<Junction, Strip>(gates.Count);
        // var sampleStarEdgesInGates = new Dictionary<Junction, Strip>(peripheralBoundaryWords.Length); // to construct the map on the new vertices
        for (int i = 0; i < peripheralBoundaryWords.Length; i++)
        {
            var newComponentVertices = new List<Junction>(gates.Count);
            var component = peripheralBoundaryWords[i].Inverse; // so that it runs around counterclockwise, matching the cyclic order.
            var componentVertices = new List<Junction>();
            var starC = new List<Strip>();
            foreach (var e in component)
            {
                var v = e.Source;
                var star = StarOrdered(v, e).Skip(2).ToList();
                // since the component is the boundary word, c = boundaryWord.Inverse = ... rev(e_{k+1}) | rev(e_{k}) ..., the cyclic order here is by def. of the boundary word: rev(e_k), e_{k+1}, a, ... , z and we will add a...z. e = rev(e_{k})
                linearStars[e] = star;
                if (star.Count > 2)
                    componentVertices.Add(v);
                starC.AddRange(star);
            } 
            componentStars.Add(starC);
            var gatesC = gates.Where(gate => gate.junctionIdentifier == i)
                .OrderBy(gate => starC.IndexOf(gate.Edges.First())).ToList();
            gatesOrderedByComponent.Add(gatesC);
            var usedJunctions = new List<float>();
            var usedOldJunctions = new List<int>();
            foreach (var gate in gatesC)
            {
                var gateInOrder = SortConnectedSet(starC, gate.Edges).ToList();
                if (!gateInOrder.ToHashSet().SetEquals(gate.Edges)) 
                    throw new Exception($"The peripheral gate {gate} is not connected in the cyclic order of the star of the peripheral subgraph (after collapsing the trees of Q\\P into their components).");

                var verticesOfGate = SortConnectedSet(componentVertices, gateInOrder.Select(e => e.Source).ToHashSet(),
                    out int indexOffset).ToList();
                
                float Badness(float k) => gateInOrder.Sum(e => Mathf.Abs(verticesOfGate.IndexOf(e.Source) - (k - indexOffset))); // same as the distance in componentVertices, but handles the wraparound better.
                
                var movements = new List<(float, (object, string))>();
                var startIndex = Math.Min(usedOldJunctions.Count > 0 ? usedOldJunctions[^1] : int.MaxValue, indexOffset);
                    // we have to ignore the first few vertices if the last gate was already moved there, because we have to maintain the cyclic order
                var stopIndex = Math.Max((usedOldJunctions.Count > 0 ? usedOldJunctions[0] : int.MinValue) + componentVertices.Count, indexOffset + verticesOfGate.Count - 1);
                for (int k = startIndex; k <= stopIndex; k++)
                {
                    var kN = k % componentVertices.Count;
                    var badness = Badness(k);
                    var v = componentVertices[kN];
                    if (!usedOldJunctions.Contains(kN))
                        movements.Add((badness, (k, $"{k.ToOrdinal()} vertex {v.ColorfulName}. Movement has to be over {badness} edges.")));
                    else
                    {
                        if (k == startIndex)
                        {
                            var nextHigherIndex = 0.5f;
                            while (usedJunctions.Contains(kN + nextHigherIndex)) nextHigherIndex /= 2;
                            badness = Badness(k + nextHigherIndex);
                            movements.Add((badness, (kN + nextHigherIndex,
                                $"ε = {nextHigherIndex} after the {(k - indexOffset).ToOrdinal()} vertex {v.ColorfulName}. Movement has to be over {badness} edges.")));
                        }
                        else if (k == stopIndex)
                        {
                            var nextLowerIndex = 0.5f;
                            while (usedJunctions.Contains(kN - nextLowerIndex)) nextLowerIndex /= 2;
                            badness = Badness(k - nextLowerIndex);
                            movements.Add((badness, (kN - nextLowerIndex,
                                $"ε = {nextLowerIndex} before the {(k - indexOffset).ToOrdinal()} vertex {v.ColorfulName}. Movement has to be over {badness} edges.")));
                        }
                        else
                        {
                            throw new Exception(
                                "Weird behavior: It seems there was a junction used when creating the gate's vertices in the free part, i.e. not in order.");
                        }
                    }
                }
                
                yield return new AlgorithmSuggestion(
                    description:
                    $"Create new vertex for {gate} in the star of the component P_{i} = {component.ToColorfulString(150, 10)}. The vertices along the gate are {verticesOfGate.ToCommaSeparatedString(j => j.ToColorfulString())}. Move the origin of the edges along P_{i} to one of the selected vertices (or to an intermediate position)",
                    options: movements.OrderBy(m => m.Item1).Select(m => m.Item2),
                    buttons: new[] { AlgorithmSuggestion.generalSubroutineContinueButton }
                ); 
                
                var selection = selectedOptionsDuringAlgorithmPause?.FirstOrDefault() ?? movements.FirstOrDefault().Item1;
                if (selection is not float vertexIndex)
                    throw new InvalidOperationException($"Selected vertex {selection} is not a valid option for the new vertex of the gate {gate} in the absorption into the periphery.");
                
                var vertexIndexInt = Mathf.RoundToInt(vertexIndex);
                bool isInt = Math.Abs(vertexIndex - vertexIndexInt) < 1e-8;
                if (isInt) vertexIndex = vertexIndexInt;
                usedJunctions.Add(vertexIndex);
                if (isInt) usedOldJunctions.Add(vertexIndexInt);
                
                Junction newJunction = isInt ? 
                    verticesOfGate[vertexIndexInt % verticesOfGate.Count] : // reuse old junction
                    new Junction(this, verticesOfGate[0].Position, NextVertexName(), color: NextVertexColor()); // create new junction
                    
                for (var oldIndex = 0; oldIndex < verticesOfGate.Count; oldIndex++)
                {
                    var junction = verticesOfGate[oldIndex];
                    Junction temporaryJunctionForMovement;
                    if (oldIndex == 0 && !isInt)
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

                    var movementDistance = (vertexIndex - oldIndex); // integer if isInt
                    var componentToMoveAlong = component;
                    var moveJunctionShiftType = MoveJunctionShiftType.ToTheRight; // shift to the outside, component is counterclockwise
                    if (movementDistance < 0)
                    {
                        componentToMoveAlong = componentToMoveAlong.Inverse; 
                        movementDistance = -movementDistance;
                        moveJunctionShiftType = MoveJunctionShiftType.ToTheLeft;
                    }

                    var movementDistanceFractionalReverse = Mathf.CeilToInt(movementDistance) - movementDistance; // 0 if integer, otherwise the distance to the next integer
                    

                    var movementEdges = componentToMoveAlong.Concat(componentToMoveAlong)
                        .SkipWhile(e => e.Source != junction).Take(Mathf.CeilToInt(movementDistance));
                    var movementCurves = movementEdges.Select(e => e.Curve).ToList();
                    var movementCurve = new ConcatenatedCurve(movementCurves);
                    
                    var movementCurveLength = movementCurve.Length - movementCurves[^1].Length * movementDistanceFractionalReverse;
                    
                    if (movementDistance != 0)
                        MoveJunction(temporaryJunctionForMovement, movementCurve, movementCurveLength, shiftType: moveJunctionShiftType);

                    for (var orderIndex = 0; orderIndex < gateInOrder.Count; orderIndex++)
                    {
                        var strip = gateInOrder[orderIndex];
                        if (strip.Source == temporaryJunctionForMovement)
                            strip.Source = newJunction;
                        strip.OrderIndexStart = orderIndex;
                    }
                    
                    if (oldIndex != 0 || isInt)
                        graph.RemoveVertex(temporaryJunctionForMovement);
                }

                newComponentVertices.Add(newJunction);
                newVertices[gate] = newJunction;
            }
            
            foreach (var e in component)
            {
                graph.RemoveEdge(e.UnderlyingEdge);
                peripheralSubgraph.RemoveEdge(e.UnderlyingEdge);
                if (!newComponentVertices.Contains(e.Source))
                    graph.RemoveVertex(e.Source);
            }

            var lastPosition = usedJunctions[^1];
            var lastJunction = newComponentVertices[^1];
            for (int j = 0; j < usedJunctions.Count; j++)
            {
                var currentPosition = usedJunctions[j];
                var currentJunction = newComponentVertices[j];

                int lastPosInt = Mathf.FloorToInt(lastPosition);
                var lastPosFrac = lastPosition - lastPosInt;
                int currentPosInt = Mathf.CeilToInt(currentPosition);
                var currentPosFracRev = currentPosInt - currentPosition;
                var movementEdges = component.Concat(component).Skip(lastPosInt).Take(currentPosInt - lastPosInt);
                var movementCurves = movementEdges.Select(e => e.Curve).ToList();
                var movementCurve = new ConcatenatedCurve(movementCurves);

                var movementCurveStart = movementCurves[0].Length * lastPosFrac;
                var movementCurveEnd = movementCurve.Length - movementCurves[^1].Length * currentPosFracRev;

                
                var newCurve = movementCurve.Restrict(movementCurveStart, movementCurveEnd).Reversed(); // we invert again, so that the boundary word will contain all of these in the right direction.
                var newEdge = new UnorientedStrip(newCurve, currentJunction, lastJunction, EdgePath.Empty, this, orderIndexStart: -1, orderIndexEnd: graph.EdgeCount, newColor: true, newName: true, addToGraph: true);
                peripheralSubgraph.AddVerticesAndEdge(newEdge.UnderlyingEdge);
                nextEdgeNew[currentJunction] = newEdge;
                previousEdgeNew[lastJunction] = newEdge.Reversed();

                lastPosition = currentPosition;
                lastJunction = currentJunction;
            }
            
        }
        
        // assign g on P'
        foreach (var (gate, junction) in newVertices)
        {
            var imageGate = gates.First(g => g.Edges.Contains(gate.Edges.First().Dg));
            junction.image = newVertices[imageGate];
            
            nextEdgeNew[junction].EdgePath = new NormalEdgePath(
                nextEdgeNew[junction.image]
            ); 
            if (nextEdgeNew[junction.image].Target != nextEdgeNew[junction].Target.image)
                throw new Exception($"The new graph map does not seem to act as a graph automorphism on the new peripheral graph.");
            
            foreach (var strip in gate.Edges)
            {
                int stripIndex = strip.EdgePath.TakeWhile(e => !oldPeripheralEdges.Contains(e)).Count();
                if (stripIndex == strip.EdgePath.Count)
                    throw new Exception($"The strip {strip} only contains Q edges but is not absorbed into the periphery. This should not happen.");
                if (stripIndex > 0)
                    strip.EdgePath = strip.EdgePath.Skip(stripIndex); // here we remove the beginning part of the edge paths of edges starting in Q that might be wrong from the collapseInvariantSubforest move (because it was not actually invariant). The vertices whose image is not well-defined are all removed as well.
            }
        }

        // isotope junctions mapping into Q
        foreach (var junction in graph.Vertices)
        {
            if (!oldPeripheralVertices.Contains(junction.image)) continue;
            var previousEdge = previousEdgeOld[junction];
            var nextEdge = nextEdgeOld[junction];
            Dictionary<Strip, int> gateIndices = new Dictionary<Strip, int>();

            var star = Star(junction);
            
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
                var originalPathInP = strip.EdgePath.TakeWhile(e => oldPeripheralEdges.Contains(e)).ToArray();
                strip.EdgePath = GetEdgePath(a, strip.Dg, originalPathInP).Concat(
                    strip.EdgePath.Skip(originalPathInP.Length)
                );
            }

            continue;

            int Badness(int j) => gateIndices.Values.Sum(i => Math.Abs(j - i));
        }

        var newPeripheralVertices = newVertices.Values.ToHashSet();
        // define g on edge interior points mapping to Q
        foreach (var strip in graph.Edges)
        {
            var edgePath = strip.EdgePath.ToList();
            for (int i = 1; i < edgePath.Count; i++) // should not be able to start or end in old vertices / edges anymore.
            {
                if (!newPeripheralVertices.Contains(edgePath[i].Source)) continue;
                var originalPathInP = new List<Strip>();
                while (oldPeripheralEdges.Contains(edgePath[i]))
                {
                    originalPathInP.Add(edgePath[i]);
                    edgePath.RemoveAt(i);
                }

                var intermediateEdgePath = GetEdgePath(edgePath[i - 1].Reversed(), edgePath[i], originalPathInP);
                edgePath.InsertRange(i, intermediateEdgePath);
                i += intermediateEdgePath.Count; // edgePath[i] is still the previous edgePath[i].
            }

        }

        yield break;

        Gate<int> GetGate(Strip edge) => gates.FirstOrDefault(g => g.Edges.Contains(edge));

        EdgePath GetEdgePath(Strip a, Strip b, IEnumerable<Strip> originalPathInP)
        {
            bool clockwise;
            var tightPathInP = CancelBacktracking(originalPathInP);
            if (tightPathInP.Count > 0)
            {
                Junction v = a.Source;
                var previousEdge = previousEdgeOld[v];
                var nextEdge = nextEdgeOld[v];
                if (tightPathInP[0] == previousEdge)
                    clockwise = false;
                else if (tightPathInP[0] == nextEdge)
                    clockwise = true;
                else
                    throw new Exception($"The original path in P should start with either the previous or the next edge of the vertex where we are attaching the new edge, but it starts with {tightPathInP[0]} instead of {previousEdge} or {nextEdge}.");
            }
            else
            {
                var linearOrder = linearStars[a];
                int indexA = linearOrder.IndexOf(a);
                int indexB = linearOrder.IndexOf(b);
                if (indexA == -1 || indexB == -1)
                    throw new Exception($"Both edges should be in the star of the vertex where we are attaching the new edge, but {a} is at index {indexA} and {b} is at index {indexB} in the linear order of the star.");
                clockwise = indexA > indexB;
            }

            return new NormalEdgePath(FollowAroundP(GetGate(a), GetGate(b), clockwise));
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

        IEnumerable<Strip> FollowAroundP(Gate<int> startGate, Gate<int> targetGate, bool clockwise)
        {
            var startJunction = newVertices[startGate];
            var targetJunction = newVertices[targetGate];
            int overflowCounter = 0;
            while (!Equals(startJunction, targetJunction))
            {
                var nextEdge = clockwise ? nextEdgeNew[startJunction] : previousEdgeNew[startJunction];
                yield return nextEdge;
                startJunction = nextEdge.Target;
                overflowCounter++;
                if (overflowCounter > graph.VertexCount)
                    throw new Exception($"We seem to be going in circles when following around the periphery from {startGate} to {targetGate}. This should not happen.");
            }
        }
    }

    #endregion

    #region Removing peripheral inefficiencies

    public IEnumerable<List<Strip>> GetPeripheralInefficiencies()
    {
        HashSet<UnorientedStrip> prePeriphery = PrePeriphery();
        foreach (var edge in OrientedEdges)
        {
            if (!prePeriphery.Contains(edge.Dg))
                continue;
            var otherEdges = Star(edge.Source).Where(
                e => Equals(e.Dg, edge.Dg)).ToList();
            if (otherEdges.Count > 1)
                yield return otherEdges;
        }
    }

    public AlgorithmSuggestion RemovePeripheralInefficiencySuggestion()
    {
        var e = GetPeripheralInefficiencies().ToArray();
        if (e.Length == 0) return null;
        return new AlgorithmSuggestion(
            options: from l in e
            // l is a list of edges with the same Dg and this Dg(l) is in the periphery.
            select (l.Select(edge => edge.Name) as object,
                    l.Select(edge => edge.ColorfulName).ToCommaSeparatedString() +
                    $" are all mapped to {l.First()!.Dg!.ColorfulName} ∈ P."
                ),
            description: "Fold initial segments of edges that map to edges in the pre-periphery.",
            buttons: new[] { AlgorithmSuggestion.peripheralInefficiencyButton }
        );
    }

    public IEnumerable<AlgorithmSuggestion> RemovePeripheralInefficiency(IReadOnlyList<Strip> foldableEdges) => 
        FoldInitialSegmentInSteps(foldableEdges); 

    #endregion

    #region Subgraphs related to the periphery

    private List<HashSet<UnorientedStrip>> PrePeripheralDegrees()
    {
        var degrees = new List<HashSet<UnorientedStrip>> { peripheralSubgraph.Edges.ToHashSet() };
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

    #endregion

    #region Names and Colors

    static readonly List<string> edgeNames = new()
    {
        "a", "b", "c", "d", /*"e", "f", "g",*/ "x", "y", "z", "u", "h", "i", "j", "k", "l", "m", "n", "o",
        "α", "β", "γ", "δ", "ε", "ζ", "θ", "κ", "λ", "μ", "ξ", "π", "ρ", "σ", "τ", "φ", "ψ", "ω",
    };

    static readonly List<string> vertexNames = new()
    {
        "v", "w", "p", "q", "r", "s", "t",
    };
    
    static readonly List<Color> edgeColors = Curve.colors;

    static readonly List<Color> vertexColors = new()
    {
        Color.black, new Color32(26, 105, 58, 255), new Color32(122, 36, 0, 255),
        new Color(0.3f, 0.3f, 0.3f), new Color32(50,6,99, 255)
    };


    private HashSet<string> usedEdgeNames;

    private void UpdateUsedEdgeNames()
    {
        usedEdgeNames = graph.Edges.Select(edge => edge.Name).Concat(
            UsedNamedEdgePaths.Select(edgePath => edgePath.name)).ToHashSet();
    }

    public string NextEdgeName()
    {
        UpdateUsedEdgeNames();
        return edgeNames.Concat(from i in Enumerable.Range(1, 1000) select $"e{i}").First(
            name => !usedEdgeNames.Contains(name)
        );
    }

    public string NextEdgeNameGreek() {
        UpdateUsedEdgeNames();
        return edgeNames.Concat(from i in Enumerable.Range(1, 1000) select $"ε{i}").First(
            name => !usedEdgeNames.Contains(name) && name[0] > 'z'
        );
    }

    public string NextVertexName()
    {
        var usedVertexNames = graph.Vertices.Select(vertex => vertex.Name).ToHashSet();
        return vertexNames.Concat(from i in Enumerable.Range(1, 1000) select $"v{i}").First(
            name => !usedVertexNames.Contains(name)    
        );
    }

    public Color NextEdgeColor()
    {
        var colorUsage = edgeColors.ToDictionary(c => c, c => 0);
        foreach (var strip in graph.Edges)
        {
            if (colorUsage.ContainsKey(strip.Color))
                colorUsage[strip.Color]++;
            else
            {
                colorUsage[strip.Color] = 1;
                Debug.LogWarning($"The color {strip.Color} of edge {strip} is not in the list of edge colors.");
            }
        }
        var (leastUsedColor, _) = colorUsage.Keys.ArgMin(c => colorUsage[c]);
        return leastUsedColor;
    }

    Color NextVertexColor()
    {
        var colorUsage = vertexColors.ToDictionary(c => c, c => 0);
        foreach (var junction in graph.Vertices)
        {
            if (colorUsage.ContainsKey(junction.Color))
                colorUsage[junction.Color]++;
            else
            {
                colorUsage[junction.Color] = 1;
                Debug.LogWarning($"The color {junction.Color} of junction {junction} is not in the list of vertex colors.");   
            }
        }
        var (leastUsedColor, _) = colorUsage.Keys.ArgMin(c => colorUsage[c]);
        return leastUsedColor;
    }

    #endregion

    #region Transition matrix and weigths

    /// <summary>
    /// The transition matrix is a combinatorial representation of the graph map g: E -> E* forgetting order and direction.
    /// The column corresponding to an edge e is how often g(e) crosses each unoriented edge e' in the graph.
    /// The surface homeomorphism described by this FibredSurface is reducible if the transition matrix of H is reducible, as this means that there is a non-trivial subgraph of G that does not only consist of pre-periphery. The boundary of the induced subgraph is preserved by an isotoped version of the homeomorphism and consists at least in part of essential edges, thus determining a reduction in the sense of the Thurston-Nielsen classification.
    /// </summary>
    /// <returns></returns>
    public Dictionary<UnorientedStrip, Dictionary<UnorientedStrip, int>> TransitionMatrix(
        IEnumerable<UnorientedStrip> strips = null)
    {
        var stripArray = (strips ?? Strips).ToArray();
        return stripArray.ToDictionary(
            edgeToCross => edgeToCross,
            edgeToCross => stripArray.ToDictionary(
                edge => edge, 
                edge => edge.EdgePath.Count(e => e.UnderlyingEdge == edgeToCross))
        );
    }

    public Dictionary<UnorientedStrip, Dictionary<UnorientedStrip, int>> TransitionMatrixEssential() =>
        TransitionMatrix(EssentialSubgraph());

    /// <summary>
    /// Tests whether the (essential) transition matrix M_H is reducible, hence if there is a non-trivial essential preserved subgraph.
    /// This is the case whenever the surface homeomorphism is reducible or if there are invariant subforests.
    /// 
    /// When the transition matrix is irreducible, we can use Perron-Frobenius theory. 
    /// The "growth" of the graph map is the spectral radius (Frobenius-Perron eigenvalue) and the associated positive eigenvector is interpreted as the widths of the edges (in the train track sense). 
    /// </summary>
    /// <returns> A non-trivial essential preserved subgraph or null if it doesn't exist, i.e. the transition matrix is irreducible. </returns>
    public HashSet<UnorientedStrip> PreservedSubgraph_Reducibility()
    {
        var prePeriphery = PrePeriphery();
        var essentialSubgraph = Strips.ToHashSet();
        essentialSubgraph.ExceptWith(prePeriphery);
        var edgesKnownToHaveFullOrbit = new HashSet<UnorientedStrip>();

        foreach (var edge in essentialSubgraph)
        {
            if (!edgesKnownToHaveFullOrbit.Contains(edge))
            {
                var orbit = OrbitOfEdge(edge, edgesKnownToHaveFullOrbit);
                if (!orbit.IsSupersetOf(essentialSubgraph))   
                    return orbit;
            }
            edgesKnownToHaveFullOrbit.Add(edge);
        }
        return null; 
    }

    public AlgorithmSuggestion ReducibilitySuggestion()
    {
        var cd = PreservedSubgraph_Reducibility();
        if (cd == null) return null;
        return new AlgorithmSuggestion(
            options: new (object, string)[] { },
            description: "The map is reducible because there is an invariant essential subgraph, with edges "
                         + cd.Select(e =>
                                 peripheralSubgraph.ContainsEdge(e)
                                     ? e.ColorfulName
                                     : $"<b>{e.ColorfulName}</b>")
                             .ToCommaSeparatedString()
                         + ".\nYou can continue with the algorithm, but it will not necessarily terminate.", 
            // todo? Find a "reduction" in the sense of the definition - being a boundary word of the preserved subgraph?
            buttons: new[] { AlgorithmSuggestion.ignoreReducibleButton }
        );
    }

    /// <summary>
    /// Finds the left and right eigenvectors of the essential transition matrix M_H associated to the Frobenius-Perron eigenvalue (growth) λ.
    /// </summary>
    public void FrobeniusPerron(bool essentialSubgraph, out double λ, out Dictionary<UnorientedStrip, double> widths,
        out Dictionary<UnorientedStrip, double> lengths,
        out Dictionary<UnorientedStrip, Dictionary<UnorientedStrip, int>> transitionMatrix)
    {
        var matrix = transitionMatrix = essentialSubgraph ? TransitionMatrixEssential() : TransitionMatrix();
        var strips = matrix.Keys.ToArray();
        Matrix<double> M = Matrix<double>.Build.Dense(strips.Length, strips.Length,
            (i, j) => matrix[strips[i]][strips[j]]
        );
        if (strips.Length == 0)
        { // M.Evd throws an exception if the matrix is empty
            λ = 1;
            widths = new Dictionary<UnorientedStrip, double>();
            lengths = new Dictionary<UnorientedStrip, double>();
            return;
        }
        var eigenvalueDecomposition = M.Evd();
        int
            columnIndex; // the index of the largest eigenvalue - should be the last index, as the eigenvalues are sorted by magnitude (?)
        (columnIndex, λ) = eigenvalueDecomposition.EigenValues.ArgMaxIndex(c => (float)c.Real);
        var eigenvector = eigenvalueDecomposition.EigenVectors.Column(columnIndex);
        if (eigenvector[0] < 0) eigenvector = eigenvector.Negate();
        var rightEigenvector = eigenvalueDecomposition.EigenVectors.Inverse().Row(columnIndex);
        if (rightEigenvector[0] < 0) rightEigenvector = rightEigenvector.Negate();

        eigenvector *= λ / eigenvector.GeometricMean();
        var relativeLengths = ((IEnumerable<double>)rightEigenvector).Enumerate().Select(tuple =>
        {
            var (i, length) = tuple;
            if (Math.Abs(length) < 1e-6) return 0;
            var strip = strips[i];
            var expectedLength = strip.EdgePath.Count;
            return expectedLength / length;
        });
        rightEigenvector *= relativeLengths.Where(x => x > 0).GeometricMean();

        widths = new Dictionary<UnorientedStrip, double>(from i in Enumerable.Range(0, strips.Length)
            select new KeyValuePair<UnorientedStrip, double>(strips[i], eigenvector[i]));
        lengths = new Dictionary<UnorientedStrip, double>(from i in Enumerable.Range(0, strips.Length)
            select new KeyValuePair<UnorientedStrip, double>(strips[i], rightEigenvector[i] < 1e-12 ? 0 : rightEigenvector[i]));
    }

    // todo: Extend weights to the prePeriphery. Implement the train tracks.

    #endregion

    #region Train Tracks

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


    #endregion
    public Curve GetBasicGeodesic(Point start, Point end, string name)
    {
        var surface = this.surface;
        if (surface is ModelSurface modelSurface)
            return modelSurface.GetBasicGeodesic(start, end, name);
        Debug.LogWarning($"The surface {surface} is not a model surface.");
        return surface.GetGeodesic(start, end, name);
    }
}
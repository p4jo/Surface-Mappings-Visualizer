using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QuikGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MenuEdge = QuikGraph.TaggedEdge<FibredSurfaceMenu.MenuVertex, string>;



public class FibredSurfaceMenu : MonoBehaviour
{
    public class MenuVertex
    {
        public FibredSurface fibredSurface;
        public FibredSurface.AlgorithmSuggestion suggestion;

        public MenuVertex(FibredSurface fibredSurface, FibredSurface.AlgorithmSuggestion suggestion)
        {
            this.fibredSurface = fibredSurface;
            this.suggestion = suggestion;
        }
    }
    
    [SerializeField] private SurfaceMenu surfaceMenu;
    
    [SerializeField] private Transform forwardButtonList;
    [SerializeField] private GameObject forwardButtonPrefab;
    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject suggestionButtonList;
    [SerializeField] private GameObject suggestionButtonPrefab;
    [SerializeField] private GameObject optionList;
    [SerializeField] private GameObject optionTogglePrefab;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text graphStatusText;
    
    private readonly AdjacencyGraph<MenuVertex, MenuEdge> fibredSurfaces = new();
    public FibredSurface FibredSurface => currentVertex?.fibredSurface;
    
    private MenuVertex currentVertex;
    private MenuEdge ParentEdge() => 
        fibredSurfaces.Edges.FirstOrDefault(e => e.Target == currentVertex);

    public void Initialize(FibredSurface fibredSurface, SurfaceMenu surfaceMenu)
    {
        this.surfaceMenu = surfaceMenu;

        MenuVertex vertex = new MenuVertex(fibredSurface, null);
        fibredSurfaces.AddVertex(vertex);
        
        currentVertex = vertex;
        UpdateUI();
    }

    private void UpdateSelectedSurface(MenuVertex newVertex)
    {
        ClearUI();
        this.currentVertex = newVertex;
        UpdateUI();
    }

    private void ClearUI()
    {
        foreach (Transform child in forwardButtonList.transform.Cast<Transform>().ToList()) 
            DestroyImmediate(child.gameObject);
        foreach (Transform child in suggestionButtonList.transform.Cast<Transform>().ToList())
            DestroyImmediate(child.gameObject);
        foreach (Transform child in optionList.transform.Cast<Transform>().ToList())
            DestroyImmediate(child.gameObject);
        surfaceMenu.Display(FibredSurface, remove: true);
    }

    private IEnumerator suggestionCoroutine;
    private void UpdateUI()
    {
        StopCoroutine(nameof(LoadSuggestionLate));
        backButton.SetActive(ParentEdge() != null);
        foreach (var edge in fibredSurfaces.OutEdges(currentVertex))
        {
            var button = Instantiate(forwardButtonPrefab, forwardButtonList.transform);
            button.GetComponentInChildren<TextMeshProUGUI>().text = edge.Tag;
            var edgeTarget = edge.Target; 
            // avoid closure problem (https://stackoverflow.com/a/271440/2687128) // this was entirely suggested by Copilot (with the link!)
            button.GetComponent<Button>().onClick.AddListener(() => UpdateSelectedSurface(edgeTarget));
        }
        
        graphStatusText.text = FibredSurface.GraphString();
        if (suggestionCoroutine != null)
        {
            StopCoroutine(suggestionCoroutine);
        }
        suggestionCoroutine = LoadSuggestionLate();
        StartCoroutine(suggestionCoroutine);

        
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    IEnumerator LoadSuggestionLate()
    {
        descriptionText.text = "Displaying new fibred surface...";
        yield return new WaitForEndOfFrame();
        surfaceMenu.Display(FibredSurface);
        yield return new WaitForEndOfFrame();
        
        var suggestion = currentVertex.suggestion;
        if (suggestion == null)
        {
            descriptionText.text = "Loading next steps...";
            yield return new WaitForEndOfFrame();
            suggestion = currentVertex.suggestion = FibredSurface.NextSuggestion();
        }
        descriptionText.text = "Displaying next steps...";
        yield return new WaitForEndOfFrame();
        
        descriptionText.text = suggestion.description;
        if (suggestion == FibredSurface.AlgorithmSuggestion.Finished) yield break;
            
        var optionToggles = new Dictionary<Toggle, (object, string)>();
        foreach (var option in suggestion.options)
        {
            var toggleGameObject = Instantiate(optionTogglePrefab, optionList.transform);
            toggleGameObject.GetComponentInChildren<TextMeshProUGUI>().text = option.Item2;
            var toggle = toggleGameObject.GetComponent<Toggle>();
            optionToggles[toggle] = option;
        }

        foreach (var buttonText in suggestion.buttons)
        {
            var button = Instantiate(suggestionButtonPrefab, suggestionButtonList.transform);
            button.GetComponentInChildren<TextMeshProUGUI>().text = buttonText.AddDotsMiddle(250, 30);
            button.GetComponent<Button>().onClick.AddListener(() =>
            {
                var selection = (from toggleOptionPair in optionToggles
                    where toggleOptionPair.Key.isOn
                    select toggleOptionPair.Value).ToList();
                if (selection.Count == 0) selection = new () { optionToggles.First().Value };
                DoSuggestion(buttonText, selection);
            });
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    // called from UI 
    private void DoSuggestion(object buttonText, IReadOnlyCollection<(object, string)> selection)
    {
        var newFibredSurface = FibredSurface.Copy();
        var tag = buttonText + "\n" + selection.Select(e => e.Item2.AddDots(50)).Take(4).ToCommaSeparatedString() + (selection.Count > 4 ? "..." : "");
        
        newFibredSurface.ApplySuggestion(selection, buttonText); // often takes several seconds. Blocks the UI.
        
        MenuVertex newVertex = new(newFibredSurface, null);
        fibredSurfaces.AddVerticesAndEdge(new MenuEdge( 
            currentVertex, newVertex, tag
        ));
        
        UpdateSelectedSurface(newVertex);
    }

    public bool DoNextSuggestion()
    {
        var suggestion = currentVertex.suggestion ?? (currentVertex.suggestion = FibredSurface.NextSuggestion());
        if (suggestion == FibredSurface.AlgorithmSuggestion.Finished) return false;
        // todo: Performance. Also wait for a frame here?
        DoSuggestion(suggestion.buttons.First(), new []{ suggestion.options.First() });
        return true;
    }
    private Coroutine algorithmCoroutine;

    // called from UI
    public void StartAlgorithm()
    {
        algorithmCoroutine ??= StartCoroutine(Run());
        return;

        IEnumerator Run()
        {
            var limit = 20 * FibredSurface.graph.EdgeCount;
            for (int i = 0; i < limit; i++)
            {
                if(!DoNextSuggestion()) 
                    break;
                yield return null; // Wait for the next frame
            }
        }
    }

    // called from UI
    public void StopAlgorithm()
    {
        if (algorithmCoroutine == null) return;
        StopCoroutine(algorithmCoroutine);
        algorithmCoroutine = null;
    }
    
    public void ToggleAlgorithm()
    {
        if (algorithmCoroutine == null) StartAlgorithm();
        else StopAlgorithm();
    }

    // called from UI
    public void BackButtonPressed()
    {
        var parent = ParentEdge()?.Source;
        if (parent != null)
            UpdateSelectedSurface(parent);
    }

    public void UpdateGraphMap(IDictionary<string, string[]> map, bool reset = false, GraphMapUpdateMode mode = GraphMapUpdateMode.Replace)
    {
        var edgeNames = (from strip in FibredSurface.Strips select strip.Name).ToHashSet();
        edgeNames.ExceptWith(map.Keys);
        foreach (var missingKey in edgeNames)
        {
            map[missingKey] = new[] { missingKey }; // identity on the other edges
        }
        var newFibredSurface = FibredSurface.Copy();
        newFibredSurface.SetMap(map, mode);
        MenuVertex newVertex = new(newFibredSurface, null);
        
        if (reset)
        {
            fibredSurfaces.Clear();
            fibredSurfaces.AddVertex(newVertex);
        }
        else
        {
            var text = string.Join(", ",
                from kvp in map
                select kvp.Key + " -> " +
                       string.Join(" ", kvp.Value).AddDots(50)
            );
            fibredSurfaces.AddVerticesAndEdge(new(currentVertex, newVertex, $"{mode} map with {text}"));
        }
        UpdateSelectedSurface(newVertex);
    }

    public void UpdateGraphMap(string text, bool reset = false, GraphMapUpdateMode mode = GraphMapUpdateMode.Replace)
    {
        string[] lines = text.Split(',', '\n');
        var graphMap = new Dictionary<string, string[]>();
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
                throw new ArgumentException("The input should be in the form \"g(a) = a B A, g(b) = ...\" or \" a -> a B A, b -> ...\"");

            string[] parts = match.Groups[2].Value.Split(' ').Select(s => s.Trim()).Where(t => t != "").ToArray();
            graphMap[match.Groups[1].Value.Trim()] = parts;
        }
        UpdateGraphMap(graphMap, reset, mode);
    }

    #region Referenced from UI
    private string graphMap = "";
    public void SetGraphMap(string text) => graphMap = text;
    
    public void ReplaceWithGraphMap() => UpdateGraphMap(graphMap, reset: false, mode: GraphMapUpdateMode.Replace);
    public void PrecomposeWithGraphMap() => UpdateGraphMap(graphMap, reset: false, mode: GraphMapUpdateMode.Precompose);
    public void PostcomposeWithGraphMap() => UpdateGraphMap(graphMap, reset: false, mode: GraphMapUpdateMode.Postcompose);
    #endregion
}

public enum GraphMapUpdateMode
{
    Precompose,
    Replace,
    Postcompose
}

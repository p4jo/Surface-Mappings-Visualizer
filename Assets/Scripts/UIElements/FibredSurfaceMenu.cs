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
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private TMP_Text graphStatusText;
    [SerializeField] private ToggleGroup toggleGroup;

    private readonly AdjacencyGraph<MenuVertex, MenuEdge> fibredSurfaces = new();
    public FibredSurface FibredSurface => currentVertex?.fibredSurface;
    
    private MenuVertex currentVertex;
    private MenuEdge ParentEdge() => 
        fibredSurfaces.Edges.FirstOrDefault(e => e.Target == currentVertex);

    public void Initialize(FibredSurface fibredSurface, SurfaceMenu surfaceMenu)
    {
        this.surfaceMenu = surfaceMenu;

        MenuVertex vertex = new MenuVertex(fibredSurface, null);
        fibredSurface.OnError += HandleError; // handle errors in the fibred surface
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
        foreach (var toggle in toggleGroup.ActiveToggles().ToList())
            toggleGroup.UnregisterToggle(toggle);
        foreach (Transform child in forwardButtonList.transform.Cast<Transform>().ToList()) 
            Destroy(child.gameObject);
        foreach (Transform child in suggestionButtonList.transform.Cast<Transform>().ToList())
            Destroy(child.gameObject);
        foreach (Transform child in optionList.transform.Cast<Transform>().ToList())
            Destroy(child.gameObject);
        surfaceMenu.Display(FibredSurface, FibredSurface.surface.Name, remove: true);
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
    
    private void HandleError(string message)
    {
        StopCoroutine(suggestionCoroutine);
        errorText.text += message + "\n";
        StartCoroutine(ClearErrorText());
        return;

        IEnumerator ClearErrorText()
        {
            yield return new WaitForSeconds(20f);
            errorText.text = errorText.text.Substring(message.Length + 1); // clear the error text after 10 seconds
        }
    }

    /// <summary>
    /// This is part of UpdateUI
    /// </summary>
    IEnumerator LoadSuggestionLate()
    {
        descriptionText.text = "Displaying new fibred surface...";
        yield return new WaitForEndOfFrame();
        surfaceMenu.Display(FibredSurface, FibredSurface.surface.Name);
        yield return new WaitForEndOfFrame();
        
        var suggestion = currentVertex.suggestion;
        if (suggestion == null)
        {
            descriptionText.text = "Loading next steps...";
            yield return new WaitForEndOfFrame();
            try
            {
                suggestion = currentVertex.suggestion = FibredSurface.NextSuggestion();
            }
            catch (Exception e)
            {
                HandleError(e.Message);
                yield break; // stop the coroutine if an error occurs
            }
        }
        descriptionText.text = "Displaying next steps...";
        yield return new WaitForEndOfFrame();
        
        descriptionText.text = suggestion.description ?? "";
            
        var optionToggles = new Dictionary<Toggle, (object, string)>();
        bool first = true;
        foreach (var option in suggestion.options ?? Array.Empty<(object, string)>())
        {
            var toggleGameObject = Instantiate(optionTogglePrefab, optionList.transform);
            toggleGameObject.GetComponentInChildren<TextMeshProUGUI>().text = option.Item2;
            var toggle = toggleGameObject.GetComponent<Toggle>();
            optionToggles[toggle] = option;
            if (!suggestion.allowMultipleSelection)
                // toggleGroup.RegisterToggle(toggle);
                toggle.group = toggleGroup; 
            if (first)
                toggle.isOn = true; // select the first option by default
            first = false;
        }
        // if (first) // suggestion.IsFinished, but without multiple enumeration
        //     yield break;

        foreach (var buttonText in suggestion.buttons ?? Array.Empty<string>())
        {
            var button = Instantiate(suggestionButtonPrefab, suggestionButtonList.transform);
            button.GetComponentInChildren<TextMeshProUGUI>().text = buttonText.AddDotsMiddle(250, 30);
            button.GetComponent<Button>().onClick.AddListener(() =>
            {
                var selection = (from toggleOptionPair in optionToggles
                    where toggleOptionPair.Key.isOn
                    select toggleOptionPair.Value).ToList();
                if (selection.Count == 0)
                    selection = new (optionToggles.Values.Take(1));
                
                DoSuggestion(buttonText, selection);
            });
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    // called from UI 
    private void DoSuggestion(object buttonText, IReadOnlyCollection<(object, string)> selection)
    {
        if (FibredSurface.Copyable)
        {
            var newFibredSurface = FibredSurface.Copy();
            newFibredSurface.OnError += HandleError; // handle errors in the fibred surface

            try
            {
                // often takes several seconds. Blocks the UI.
                newFibredSurface.ApplySuggestion(selection, buttonText); 
            }
            catch (Exception e)
            {
                HandleError(e.Message);
                return;
            }

            var newVertex = new MenuVertex(newFibredSurface, null);
            fibredSurfaces.AddVerticesAndEdge(new MenuEdge(
                source: currentVertex, 
                target: newVertex, 
                tag: buttonText + "\n" + selection.Select(
                         e => e.Item2.AddDots(50)).Take(4).ToCommaSeparatedString() +
                     (selection.Count > 4 ? "..." : "")
                )
            );
            UpdateSelectedSurface(newVertex);
        }
        else
        {
            ClearUI(); // also undraws the surface
            try
            {
                // often takes several seconds. Blocks the UI.
                FibredSurface.ApplySuggestion(selection, buttonText); 
            }
            catch (Exception e)
            {
                HandleError(e.Message);
                return;
            }
            currentVertex.suggestion = null; // reset the suggestion, so that it is recomputed
            UpdateUI();
        }

    }

    public bool DoNextSuggestion()
    {
        var suggestion = currentVertex.suggestion ?? (currentVertex.suggestion = FibredSurface.NextSuggestion());
        var selection = suggestion.options.FirstOrDefault();
        if (selection == default) // suggestion.IsFinished, but without multiple enumeration
            return false;
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
    public void SelectFibredSurface(FibredSurface fibredSurface, bool addIfNotPresent = false)
    {
        var menuVertex = fibredSurfaces.Vertices.FirstOrDefault(v => v.fibredSurface == fibredSurface);
        if (menuVertex == null)
        {
            if (!addIfNotPresent)
                throw new ArgumentException("The fibred surface is not present in the menu.");
            menuVertex = new MenuVertex(fibredSurface, null);
            fibredSurfaces.AddVerticesAndEdge(new MenuEdge(currentVertex, menuVertex, "Update fibred surface"));
        }
        UpdateSelectedSurface(menuVertex);
    }

    // called from UI
    public void BackButtonPressed()
    {
        var parent = ParentEdge()?.Source;
        if (parent != null)
            UpdateSelectedSurface(parent);
    }

    public void UpdateGraphMap(string text, bool reset = false, GraphMapUpdateMode mode = GraphMapUpdateMode.Replace) => 
        UpdateGraphMap(FibredSurface.ParseMap(text), reset, mode);

    public void UpdateGraphMap(IReadOnlyDictionary<string, string> map, bool reset = false,
        GraphMapUpdateMode mode = GraphMapUpdateMode.Replace) =>
        UpdateGraphMap(FibredSurface.ParseMap(map), reset, mode);

    public void UpdateGraphMap(IReadOnlyDictionary<Strip, EdgePath> map, bool reset = false,
        GraphMapUpdateMode mode = GraphMapUpdateMode.Replace, bool selectFibredSurface = false)
    {
        if (FibredSurface == null)
            throw new InvalidOperationException("Cannot change the original fibred surface if no fibred surface is selected. Did you initialize the menu with any fibred surface?");
        if (map.Count > 0)
        {
            var fibredGraphUsedInMap = map.Keys.First().graph;
            if (FibredSurface.graph != fibredGraphUsedInMap)
            {
                if (!selectFibredSurface)
                    throw new ArgumentException("The map keys must be strips of the current fibred surface.");
                currentVertex = fibredSurfaces.Vertices.First(v => v.fibredSurface.graph == fibredGraphUsedInMap);
            }
        }
        var edges = FibredSurface.Strips.ToHashSet();
        edges.ExceptWith(from k in map.Keys select k.UnderlyingEdge);
        if (edges.Count != 0)
        {
            var enteredMapNew = new Dictionary<Strip, EdgePath>(map);
            foreach (var missingEdge in edges)
                enteredMapNew[missingEdge] = new NormalEdgePath(missingEdge); // identity on the other edges
            map = enteredMapNew;
        }

        var fibredSurfaceCopy = FibredSurface.Copy();
        fibredSurfaceCopy.OnError += HandleError; // handle errors in the fibred surface
        FibredSurface.SetMap(map, mode);

        MenuVertex newVertex = new MenuVertex(FibredSurface, null);
        if (true)
        {
            currentVertex.fibredSurface = fibredSurfaceCopy;
            currentVertex.suggestion = null; // reset the suggestion, so that it is recomputed
        }
        
        if (reset)
        {
            fibredSurfaces.Clear();
            fibredSurfaces.AddVertex(newVertex);
        }
        else
        {
            var text = (
                from edge in map.Keys
                where map[edge].Count != 1 || !Equals(map[edge].First(), edge.UnderlyingEdge)  
                select ((IDrawable)edge).ColorfulName + " -> " + map[edge].ToColorfulString(50, 10)
            ).ToCommaSeparatedString();
            fibredSurfaces.AddVerticesAndEdge(new MenuEdge(
                currentVertex,
                newVertex,
                $"{mode} map with {text}"
            ));
        }
        UpdateSelectedSurface(newVertex);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QuikGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FibredSurfaceMenu : MonoBehaviour
{
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
    
    private readonly AdjacencyGraph<FibredSurface, TaggedEdge<FibredSurface, string>> fibredSurfaces = new();
    public FibredSurface FibredSurface { get; private set; }
    private FibredSurface fibredSurfaceCopy;
    
    private TaggedEdge<FibredSurface, string> ParentEdge() => 
        fibredSurfaces.Edges.FirstOrDefault(e => e.Target == FibredSurface);

    public void Initialize(FibredSurface fibredSurface, SurfaceMenu surfaceMenu)
    {
        this.surfaceMenu = surfaceMenu;
        
        fibredSurfaces.AddVertex(fibredSurface);
        
        FibredSurface = fibredSurface;
        UpdateUI();
    }
    
    public void UpdateSelectedSurface(FibredSurface fibredSurface)
    {
        ClearUI();
        FibredSurface = fibredSurface;
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

    private void UpdateUI()
    {
        backButton.SetActive(ParentEdge() != null);
        foreach (var edge in fibredSurfaces.OutEdges(FibredSurface))
        {
            var button = Instantiate(forwardButtonPrefab, forwardButtonList.transform);
            button.GetComponentInChildren<TextMeshProUGUI>().text = edge.Tag;
            var edgeTarget = edge.Target; 
            // avoid closure problem (https://stackoverflow.com/a/271440/2687128) // this was entirely suggested by Copilot (with the link!)
            button.GetComponent<Button>().onClick.AddListener(() => UpdateSelectedSurface(edgeTarget));
        }
        
        var fibredSurfaceCopy = FibredSurface.Copy();
        graphStatusText.text = fibredSurfaceCopy.GraphString();

        descriptionText.text = "Loading next steps...";
        var suggestions = fibredSurfaceCopy.NextSuggestion();
        if (suggestions == null)
            descriptionText.text = "The algorithm finishes: This is an efficient fibred surface.";
        else
        {
            descriptionText.text = suggestions.description;
            var optionToggles = new Dictionary<Toggle, object>();
            foreach (var option in suggestions.options)
            {
                var toggleGameObject = Instantiate(optionTogglePrefab, optionList.transform);
                toggleGameObject.GetComponentInChildren<TextMeshProUGUI>().text = option.ToString();
                var toggle = toggleGameObject.GetComponent<Toggle>();
                optionToggles[toggle] = option;
            }

            foreach (var buttonText in suggestions.buttons)
            {
                var button = Instantiate(suggestionButtonPrefab, suggestionButtonList.transform);
                button.GetComponentInChildren<TextMeshProUGUI>().text = buttonText.ToString();
                button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    var selection = (from toggleOptionPair in optionToggles
                        where toggleOptionPair.Key.isOn
                        select toggleOptionPair.Value).ToList();
                    if (selection.Count == 0) selection = new List<object> { optionToggles.First().Value };
                    DoSuggestion(buttonText, selection, fibredSurfaceCopy);
                });
            }
        }


        surfaceMenu.Display(FibredSurface);
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    // called from UI 
    private void DoSuggestion(object buttonText, IEnumerable<object> enumerable, FibredSurface newFibredSurface)
    {
        enumerable = enumerable.ToList();
        var tag = buttonText + "\n" + string.Join(", ", enumerable.Select(e => e.ToString().AddDots(50)).Take(4)) + (enumerable.Count() > 4 ? "..." : "");
        
        fibredSurfaces.AddVertex(newFibredSurface);
        
        fibredSurfaces.AddEdge(new TaggedEdge<FibredSurface, string>( FibredSurface, newFibredSurface, tag));
        
        newFibredSurface.ApplySuggestion(enumerable, buttonText);
        UpdateSelectedSurface(newFibredSurface);
    }

    // called from UI
    public void BackButtonPressed()
    {
        var parent = ParentEdge()?.Source;
        if (parent != null) UpdateSelectedSurface(parent);
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
        if (reset)
        {
            fibredSurfaces.Clear();
            fibredSurfaces.AddVertex(newFibredSurface);
        }
        else
        {
            fibredSurfaces.AddVerticesAndEdge(new(FibredSurface, newFibredSurface, "Update map"));
        }
        UpdateSelectedSurface(newFibredSurface);
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

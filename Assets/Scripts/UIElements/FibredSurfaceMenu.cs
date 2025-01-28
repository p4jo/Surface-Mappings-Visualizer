using System.Collections.Generic;
using System.Linq;
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

    public void ClearUI()
    {
        foreach (Transform child in forwardButtonList.transform.Cast<Transform>().ToList()) 
            DestroyImmediate(child.gameObject);
        foreach (Transform child in suggestionButtonList.transform.Cast<Transform>().ToList())
            DestroyImmediate(child.gameObject);
        foreach (Transform child in optionList.transform.Cast<Transform>().ToList())
            DestroyImmediate(child.gameObject);
        surfaceMenu.Display(FibredSurface, remove: true);
    }
    
    public void UpdateUI()
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
        
        graphStatusText.text = FibredSurface.GraphString();
        
        var fibredSurfaceCopy = FibredSurface.Copy();
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

    private void DoSuggestion(object buttonText, IEnumerable<object> enumerable, FibredSurface newFibredSurface)
    {
        enumerable = enumerable.ToList();
        var tag = buttonText + "\n" + string.Join(", ", enumerable.Select(e => e.ToString().AddDots(50)).Take(4)) + (enumerable.Count() > 4 ? "..." : "");
        
        fibredSurfaces.AddVertex(newFibredSurface);
        
        fibredSurfaces.AddEdge(new TaggedEdge<FibredSurface, string>( FibredSurface, newFibredSurface, tag));
        
        newFibredSurface.ApplySuggestion(enumerable, buttonText);
        UpdateSelectedSurface(newFibredSurface);
    }

    public void BackButtonPressed()
    {
        var parent = ParentEdge()?.Source;
        if (parent != null) UpdateSelectedSurface(parent);
    }

    private TaggedEdge<FibredSurface, string> ParentEdge() => 
        fibredSurfaces.Edges.FirstOrDefault(e => e.Target == FibredSurface);
}

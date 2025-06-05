
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu: MonoBehaviour
{
    
    [SerializeField] private GameObject surfaceMenuPrefab;
    public List<SurfaceMenu> surfaceMenus = new();
    [SerializeField] private RectTransform canvas;
    [SerializeField] private string surfaceParameters;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] public MenuMode mode = MenuMode.AddPoint;
    [SerializeField] private TMP_Dropdown curveDropdown;
    
    public event Action UIMoved;
    public virtual void OnUIMoved() => UIMoved?.Invoke();
    public string selectedCurve;
    [SerializeField] private FibredSurfaceMenu fibredSurfaceMenu;

    private void Start()
    {
        // var testSurface = SurfaceGenerator.ModelSurface4GGon(2, 0, "Genus-2 surface", 
        //         new string[] { "side d", "side c", "side a", "side b" } // labelling from 
        //     );
        // var surface = new AbstractSurface(testSurface);
        var parameters = from s in surfaceParameters.Split(";") select SurfaceParameter.FromString(s);
        var surface = SurfaceGenerator.CreateSurface(parameters);
        if (surface.drawingSurfaces.Values.FirstOrDefault() is ModelSurface modelSurface)
        {
            foreach (var side in modelSurface.AllSideCurves) 
                surface.AddHomeomorphism(
                    side.DeckTransformation(),
                    drawTargetInSameWindowAsSource: true
                );
            // var modelChange = modelSurface.SwitchHyperbolicModel();
            // if (modelChange != null)
            // {
            //     surface.AddHomeomorphism(modelChange);
            //     if (modelChange.target is ModelSurface modelChangeTarget)
            //         foreach (var side in modelChangeTarget.AllSideCurves) 
            //             surface.AddHomeomorphism(
            //                 side.DeckTransformation(),
            //                 drawTargetInSameWindowAsSource: true
            //             );
            // }
            // var toKleinModel = modelSurface.ToKleinModel();
            // if (toKleinModel != null)
            // {
            //     surface.AddHomeomorphism(toKleinModel);
            //     // if (ToKleinModel.target is ModelSurface modelChangeTarget)
            //     //     foreach (var side in modelChangeTarget.AllSideCurves) 
            //     //         surface.AddHomeomorphism(
            //     //             side.DeckTransformation(), // not implemented for Klein model. It would act as if it is a euclidean plane
            //     //             drawTargetInSameWindowAsSource: true
            //     //         );
            // }
        }
        Initialize(surface);
    }


    public void Initialize(AbstractSurface surface)
    {
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        var surfaceMenu = gameObject.GetComponent<SurfaceMenu>();
        surfaceMenu.Initialize(surface, canvas, cameraManager, this); 
        surfaceMenu.StuffShown += OnStuffShown;
        surfaceMenu.StuffDeleted += OnStuffDeleted;
        surfaceMenus.Add(surfaceMenu);
        
        foreach (var (surfaceName, drawingSurface) in surface.drawingSurfaces)
        {
            if (drawingSurface is not ModelSurface modelSurface) continue;
            foreach (ModelSurfaceSide side in modelSurface.sides)
            {
                surfaceMenu.Display(side, surfaceName, preview: false, propagateToDrawingSurfaces: false);
                // surfaceMenu.Display(side.other, surfaceName, preview: false);
            }
        }
        
        InitializeFibredSurface();
    }

    private void OnStuffShown(IDrawnsformable stuff, string surface)
    {
        if (stuff is Curve curve && curveDropdown.options.All(option => option.text != curve.Name))
        {
            curveDropdown.options.Add(new TMP_Dropdown.OptionData(curve.Name, null, curve.Color));
        }
    }
    
    private void OnStuffDeleted(IDrawnsformable stuff, string surface)
    {
        if (stuff is not Curve curve) return;
        var index = curveDropdown.options.FindIndex(option => option.text == curve.Name);
        if (index == -1) return;
        curveDropdown.options.RemoveAt(index);
        if (selectedCurve != curve.Name) return;
        selectedCurve = curveDropdown.options[0].text;
        curveDropdown.value = 0;
    }

    public void DropdownValueChanged()
    {
        selectedCurve = curveDropdown.options[curveDropdown.value].text;
    }

    public void DehnTwistButtonClicked()
    {
        var surfaceMenu = surfaceMenus[^1];
        var (res, surfaceName) = surfaceMenu.GetCurve(selectedCurve);
        if (res is not Curve curve) return;
        AddMenuFromAutomorphism(AutomorphismType.DehnTwist, surfaceName, curve);
    }

    private void AddMenuFromAutomorphism(AutomorphismType type, string surfaceName, params IDrawnsformable[] parameters)
    {
        var surfaceMenu = surfaceMenus[^1];
        Dictionary<string, Homeomorphism> automorphisms = new();
        foreach (var (drawingSurfaceName, drawingSurface) in surfaceMenu.surface.drawingSurfaces)
        {
            var homeomorphism = surfaceMenu.surface.GetHomeomorphism(surfaceName, drawingSurfaceName);
            var transformedParams = from s in parameters select s.ApplyHomeomorphism(homeomorphism);
            var automorphism = drawingSurface.GetAutomorphism(type, transformedParams.ToArray());
            if (automorphism == null) continue;
            automorphisms[drawingSurfaceName] = automorphism;
        }

        if (automorphisms.Count == 0)
        {
            Debug.Log($"No automorphisms found in the surfaces for the type {type} and parameters {parameters}");
            return;
        }
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        var menu = gameObject.GetComponent<SurfaceMenu>();
        menu.Initialize(surfaceMenu, automorphisms);
        surfaceMenus.Add(menu);
    }

    public void InitializeFibredSurface() // todo: Feature. Let the user draw / select own graph?
    {
        if (fibredSurfaceMenu.FibredSurface is not null) return;
        
        var surfaceMenu = surfaceMenus[0]; 
        fibredSurfaceMenu.gameObject.SetActive(true);
        var surface = surfaceMenu.geodesicSurface as ModelSurface;

        // FibredSurface fibredSurface = FibredSurfaceFactory.RoseSpine(surface, // [BH] example 6.1.
        //     map: new Dictionary<string, string>
        //     {
        //         ["a"] = "a B A b D C A",
        //         ["b"] = "a c d B a b c d B",
        //         ["c"] = "c c d B",
        //         ["d"] = "b c d B"
        //     },
        //     names: new Dictionary<string, string>
        //     {
        //         ["side a"] = "a",
        //         ["side b"] = "b",
        //         ["side c"] = "d",
        //         ["side d"] = "c"
        //     },
        //     reverse: new Dictionary<string, bool>
        //     {
        //         ["a"] = false,
        //         ["b"] = false,
        //         ["c"] = true,
        //         ["d"] = true
        //     }
        // );
        FibredSurface fibredSurface = FibredSurfaceFactory.RoseSpine(surface, 
            map: new Dictionary<string, string> 
            {
                ["a"] = "a",
                ["b"] = "b",
                ["c"] = "c",
                ["d"] = "d"
            }, 
            names: new Dictionary<string, string>
            {
                ["side a"] = "a",
                ["side b"] = "b",
                ["side c"] = "c",
                ["side d"] = "d"
            },
            reverse: new Dictionary<string, bool>
            {
                ["a"] = true,
                ["b"] = true,
                ["c"] = false,
                ["d"] = false
            }
        );
        
        fibredSurfaceMenu.Initialize(fibredSurface, surfaceMenu);
        
        fibredSurfaceMenu.UpdateGraphMap("a \u21a6 B a D c d C b", mode: GraphMapUpdateMode.Postcompose); // Push(α)
        fibredSurfaceMenu.UpdateGraphMap("c \u21a6 b A B a D c d", mode: GraphMapUpdateMode.Postcompose); // Push(γ)
        fibredSurfaceMenu.UpdateGraphMap("b \u21a6 c D C d A b a", mode: GraphMapUpdateMode.Postcompose); // Push(β rev)
        fibredSurfaceMenu.UpdateGraphMap("d \u21a6 c d C b A B a", mode: GraphMapUpdateMode.Postcompose); // Push(δ)

        var p = "d C b A B a D c";
        var P = reverse(p); 
        string reverse(string c) => new((from ch in c.Reverse() select char.IsUpper(ch) ? char.ToLower(ch) : char.ToUpper(ch)).ToArray()); 
        string conjugate(string e, string c) => $"({c})°({e})"; // c e c^{-1}
        fibredSurfaceMenu.UpdateGraphMap($"d \u21a6 {P} d\n" +
                                         $"b \u21a6 {conjugate(P, "a d")} b\n" +
                                         $"c \u21a6 c {conjugate(P,  P + " d C a d")}\n" +
                                         $"a \u21a6 a {conjugate(P,$"D {p} c {P} d {conjugate(P, "c a d")} {P} d C a D")}",
            mode: GraphMapUpdateMode.Replace);

        var PointPush = new PushingPath(EdgePath.FromString("C d A b a d' c d d c' a D", fibredSurface.Strips));


        var edges = fibredSurface.OrientedEdges.ToDictionary(strip => strip.Name);

        var correctPointPush = new PushingPath(new List<PushingPath.Entry>
        {
            new PushingPath.EdgeFollowing(edges["C"], 1, 2, 1, edges["d"], edges["d"])
        });
        Debug.Log(fibredSurface.Strips.ToLineSeparatedString(strip => PointPush.Image(strip).ToString()));
        
        
        // fibredSurfaceMenu.StartAlgorithm();
    }
}
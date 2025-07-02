
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

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
        var (surface, fibredSurface) = SurfaceGenerator.CreateSurface(parameters);
        if (surface.drawingSurfaces.Values.FirstOrDefault() is ModelSurface { geometryType: GeometryType.HyperbolicDisk or GeometryType.HyperbolicPlane } modelSurface)
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
        
        
        if (fibredSurfaceMenu.FibredSurface is not null) return;
        
        var surfaceMenu = surfaceMenus[0]; 
        fibredSurfaceMenu.gameObject.SetActive(true);
        fibredSurfaceMenu.Initialize(fibredSurface, surfaceMenu);

        // [BH] example 6.1. :
        // var c = fibredSurface.Strips.First(strip => strip.Name == "c");
        // var d = fibredSurface.Strips.First(strip => strip.Name == "d");
        // c.ReplaceWithInverseEdge();
        // d.ReplaceWithInverseEdge();
        // fibredSurfaceMenu.UpdateGraphMap(new Dictionary<string, string>
        // {
        //     ["a"] = "a B A b D C A",
        //     ["b"] = "a c d B a b c d B",
        //     ["c"] = "c c d B",
        //     ["d"] = "b c d B"
        // });
        
        if (fibredSurface.surface is not ModelSurface { Genus: 2 }) return;
        
        
        var a = fibredSurface.Strips.First(strip => strip.Name == "a");
        var b = fibredSurface.Strips.First(strip => strip.Name == "b");
        a.ReplaceWithInverseEdge();
        b.ReplaceWithInverseEdge();
        
        fibredSurfaceMenu.UpdateGraphMap("a \u21a6 B a D c d C b", mode: GraphMapUpdateMode.Postcompose); // Push(α)
        fibredSurfaceMenu.UpdateGraphMap("c \u21a6 b A B a D c d", mode: GraphMapUpdateMode.Postcompose); // Push(γ)
        fibredSurfaceMenu.UpdateGraphMap("b \u21a6 c D C d A b a", mode: GraphMapUpdateMode.Postcompose); // Push(β rev)
        fibredSurfaceMenu.UpdateGraphMap("d \u21a6 c d C b A B a", mode: GraphMapUpdateMode.Postcompose); // Push(δ)

        fibredSurfaceMenu.SelectFibredSurface(fibredSurface);
        fibredSurfaceMenu.UpdateGraphMap($"ρ := d C b A B a D c\n" +
                                         $"d \u21a6 Ρ d\n" +
                                         $"b \u21a6 (a d)°(Ρ) b\n" +
                                         $"c \u21a6 c (d C a d)°(Ρ)\n" +
                                         $"a \u21a6 a (D ρ c Ρ d (c a d)°(Ρ) Ρ d C a D)°(Ρ)",
            mode: GraphMapUpdateMode.Replace);

        var pointPush = new PushingPath(EdgePath.FromString("C b d' c d d c' a", fibredSurface.Strips), startLeft: true);
        var r = new Random();
        foreach (var variable in pointPush.variables)
        {
            variable.SetValue((float) r.NextDouble());
        }
        pointPush.CalculateSelfIntersections();

        // var edges = fibredSurface.OrientedEdges.ToDictionary(strip => strip.Name);

        Debug.Log(pointPush.ToString());

        var pushingMapString = fibredSurface.Strips.ToLineSeparatedString(strip => $"g({strip.Name}) = { pointPush.Image(strip)}");
        Debug.Log(pushingMapString);
        
        
        fibredSurfaceMenu.UpdateGraphMap(fibredSurface.Strips.ToDictionary(e => (Strip) e, e => pointPush.Image(e)),
            mode: GraphMapUpdateMode.Replace, selectFibredSurface: true); 
        
        
        // fibredSurfaceMenu.StartAlgorithm();
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
            foreach (var puncture in drawingSurface.punctures)
            {
                surfaceMenu.Display(puncture, surfaceName, preview: false, propagateToDrawingSurfaces: false); 
                // Todo: feature: Add styles for displayed points. Here: style: cross
            }
            
            if (drawingSurface is not ModelSurface modelSurface) continue;
            foreach (ModelSurfaceSide side in modelSurface.sides)
            {
                surfaceMenu.Display(side, surfaceName, preview: false, propagateToDrawingSurfaces: false);
                // Todo: feature: Add styles for displayed curves. Here: style: dotted
                // surfaceMenu.Display(side.other, surfaceName, preview: false);
            }

        }
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
}
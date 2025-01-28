
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
        var testSurface = SurfaceGenerator.ModelSurface4GGon(2, 0, "Genus-2 surface", 
                new string[] { "side d", "side c", "side a", "side b" } // labelling from [BH] example 6.1.
            );
        var surface = new AbstractSurface(testSurface);
        // var parameters = from s in surfaceParameters.Split(";") select SurfaceParameter.FromString(s);
        // var surface = SurfaceGenerator.CreateSurface(parameters);
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
                surfaceMenu.Display(side, surfaceName, preview: false);
        }
        
        InitializeFibredSurface();
    }

    private void OnStuffShown(ITransformable stuff, string surface)
    {
        if (stuff is Curve curve && curveDropdown.options.All(option => option.text != curve.Name))
        {
            curveDropdown.options.Add(new TMP_Dropdown.OptionData(curve.Name, null, curve.Color));
        }
    }
    
    private void OnStuffDeleted(ITransformable stuff, string surface)
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

    private void AddMenuFromAutomorphism(AutomorphismType type, string surfaceName, params ITransformable[] parameters)
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

    public void InitializeFibredSurface() // todo: let user draw / select own graph?
    {
        if (fibredSurfaceMenu.FibredSurface is not null) return;
        
        var surfaceMenu = surfaceMenus[0]; 
        fibredSurfaceMenu.gameObject.SetActive(true);
        
        // if (surface.Name)
        var map = new Dictionary<string, string> // todo: Menu for entering the map before this menu.
        {
            ["a"] = "a B A b D C A",
            ["b"] = "a c d B a b c d B",
            ["c"] = "c c d B",
            ["d"] = "b c d B"
        };
        fibredSurfaceMenu.Initialize(FibredSurfaceFactory.RoseSpine(surfaceMenu.geodesicSurface as ModelSurface, map), surfaceMenu);
    }
}
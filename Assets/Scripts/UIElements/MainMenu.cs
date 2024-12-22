
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
    
    private void Start()
    {
        var parameters = from s in surfaceParameters.Split(";") select SurfaceParameter.FromString(s);
        var surface = SurfaceGenerator.CreateSurface(parameters);
        
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        var surfaceMenu = gameObject.GetComponent<SurfaceMenu>();
        surfaceMenu.Initialize(surface, canvas, cameraManager, this); 
        surfaceMenu.StuffShown += OnStuffShown;
        surfaceMenus.Add(surfaceMenu);
        
        foreach (var (surfaceName, drawingSurface) in surface.drawingSurfaces)
        {
            if (drawingSurface is not ModelSurface modelSurface) continue;
            foreach (ModelSurfaceSide side in modelSurface.sides) 
                surfaceMenu.Display(side, surfaceName, false);
        }
    }

    private void OnStuffShown(ITransformable stuff, string surface)
    {
        if (stuff is Curve curve && curveDropdown.options.All(option => option.text != curve.Name))
        {
            curveDropdown.options.Add(new TMP_Dropdown.OptionData(curve.Name, null, curve.Color));
        }
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

}
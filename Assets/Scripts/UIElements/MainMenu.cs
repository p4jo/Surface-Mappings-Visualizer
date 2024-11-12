
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;

public class MainMenu: MonoBehaviour
{
    
    [SerializeField] private GameObject surfaceMenuPrefab;
    public List<SurfaceMenu> surfaceMenus = new();
    [SerializeField] private RectTransform canvas;
    [SerializeField] private string surfaceParameters;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] public MenuMode mode = MenuMode.AddPoint;
    
    public event Action UIMoved;
    public virtual void OnUIMoved() => UIMoved?.Invoke();
    
    
    private void Start()
    {
        var parameters = from s in surfaceParameters.Split(";") select SurfaceParameter.FromString(s);
        var surface = SurfaceGenerator.CreateSurface(parameters);
        
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        var surfaceMenu = gameObject.GetComponent<SurfaceMenu>();
        surfaceMenu.Initialize(surface, canvas, cameraManager, this); 
        surfaceMenus.Add(surfaceMenu);
        
        foreach (var (surfaceName, drawingSurface) in surface.drawingSurfaces)
        {
            if (drawingSurface is not ModelSurface modelSurface) continue;
            foreach (ModelSurfaceSide side in modelSurface.sides) 
                surfaceMenu.Display(side, surfaceName, false);
        }
    }

    void AddMenuFromAutomorphism(AutomorphismType type, params ITransformable[] parameters)
    {
        var surfaceMenu = surfaceMenus[^1];
        Dictionary<string, Homeomorphism> automorphisms = new();
        foreach (var (drawingSurfaceName, drawingSurface) in surfaceMenu.surface.drawingSurfaces)
        {
            var automorphism = drawingSurface.GetAutomorphism(type, parameters);
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
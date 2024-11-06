
using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;

public class MainMenu: MonoBehaviour
{
    
    [SerializeField] private GameObject surfaceMenuPrefab;
    private SurfaceMenu surfaceMenu;
    [SerializeField] private RectTransform canvas;
    [SerializeField] private string surfaceParameters;
    [SerializeField] private CameraManager cameraManager;
    private void Start()
    {
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        surfaceMenu = gameObject.GetComponent<SurfaceMenu>();
        var parameters = from s in surfaceParameters.Split(";") select SurfaceParameter.FromString(s);
        var surface = SurfaceGenerator.CreateSurface(parameters);
        surfaceMenu.Initialize(surface, canvas, cameraManager, this); 
        // todo
        var (torusName, torus) = surface.drawingSurfaces.FirstOrDefault(v => v.Value is ModelSurface);
        var modelTorus = torus as ModelSurface;
        var curve = modelTorus.sideCurves[0];
        surfaceMenu.AddCurve(curve);
    }

    public event Action UIMoved;

    public virtual void OnUIMoved()
    {
        UIMoved?.Invoke();
    }
}
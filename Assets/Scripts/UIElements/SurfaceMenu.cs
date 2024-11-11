using System;
using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines.Editor;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public enum MenuMode
{
    AddPoint,
    AddCurve,
    AddGrid,
    SelectPoint,
    SelectCurve,
    SelectGrid,
}


/// <summary>
/// This is also AbstractSurfaceVisualizer in a sense. This should potentially be split into two classes.
/// </summary>
public class SurfaceMenu: MonoBehaviour 
{
    [SerializeField] private GameObject parametricSurfaceVisualizerUIPrefab;
    [SerializeField] private  GameObject parametricSurfaceVisualizerPrefab;
    [SerializeField] private  GameObject modelSurfaceVisualizerUIPrefab;
    [SerializeField] private GameObject modelSurfaceVisualizerPrefab;
    private RectTransform canvas;

    private readonly Dictionary<string, SurfaceVisualizer> visualizers = new();

    private SurfaceMenu nextMenu, lastMenu;
    private readonly Dictionary<string, Homeomorphism> forwardHomeos = new(), backwardsHomeos = new();
    
    private AbstractSurface surface;
    private string currentSurfaceName; 
    public GeodesicSurface geodesicSurface { get; set; }

    private List<Point> currentWaypoints;
    
    public MenuMode Mode
    {
        get => mainMenu.mode;
        set => mainMenu.mode = value;
    }
    
    private CameraManager cameraManager;
    [SerializeField] private MainMenu mainMenu;
    [SerializeField] private string currentCurveName;


    public void Initialize(AbstractSurface surface, RectTransform canvas, CameraManager cameraManager, MainMenu mainMenu)
    {
        this.canvas = canvas;
        this.surface = surface;
        this.cameraManager = cameraManager;
        this.mainMenu = mainMenu;
        var i = -1;
        foreach (var (drawingSurfaceName, drawingSurface) in surface.drawingSurfaces)
        {
            i++;

            SurfaceVisualizer surfaceVisualizer;
            GameObject surfaceVisualizerGameObject;
            RawImage panel;
            switch (drawingSurface)
            {
                case ParametricSurface parametricSurface:
                {
                    var surfaceVisualizerUI = Instantiate(parametricSurfaceVisualizerUIPrefab, transform);
                    surfaceVisualizerGameObject = Instantiate(parametricSurfaceVisualizerPrefab);
                    panel = surfaceVisualizerUI.GetComponentInChildren<RawImage>();
                    
                    var parametricSurfaceVisualizer = 
                        surfaceVisualizerGameObject.GetComponentInChildren<ParametricSurfaceVisualizer>();
                    parametricSurfaceVisualizer.Initialize(parametricSurface);
                    surfaceVisualizer = parametricSurfaceVisualizer;
                    break;
                }
                case ModelSurface modelSurface:
                {
                    // var surfaceVisualizerUI = Instantiate(modelSurfaceVisualizerUIPrefab, transform);
                    // var modelSurfaceVisualizer = surfaceVisualizerUI.GetComponentInChildren<ModelSurfaceVisualizer>();
                    // modelSurfaceVisualizer.Initialize(modelSurface);
                    // surfaceVisualizer = modelSurfaceVisualizer;
                    //
                    // now same as parametrix
                    var surfaceVisualizerUI = Instantiate(modelSurfaceVisualizerUIPrefab, transform);
                    panel = surfaceVisualizerUI.GetComponentInChildren<RawImage>();
                    surfaceVisualizerGameObject = Instantiate(modelSurfaceVisualizerPrefab); // no parent                   

                    var modelSurfaceVisualizer = 
                        surfaceVisualizerGameObject.GetComponentInChildren<ModelSurfaceVisualizer>();
                    modelSurfaceVisualizer.Initialize(modelSurface, panel);
                    surfaceVisualizer = modelSurfaceVisualizer;
                    
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
            if (this.geodesicSurface is null && drawingSurface is GeodesicSurface geodesicSurface)
                this.geodesicSurface = geodesicSurface;

            surfaceVisualizerGameObject.transform.position = new Vector3(100 * surfaceVisualizer.id, 0, 0);
                    
            var kamera = surfaceVisualizerGameObject.GetComponentInChildren<UICamera>();
            kamera.Initialize(panel, canvas);
            cameraManager.AddKamera(kamera);
            kamera.minimalPosition = drawingSurface.MinimalPosition;
            kamera.maximalPosition = drawingSurface.MaximalPosition;
            mainMenu.UIMoved += () => kamera.UIMoved();
            
            visualizers.Add(drawingSurfaceName, surfaceVisualizer);
            surfaceVisualizer.MouseEvent += (location, button) =>
                MouseEvent(drawingSurface.ClampPoint(location), drawingSurface.Name, button);
        }
        if (this.geodesicSurface is null)
            throw new Exception("None of the surfaces provides geodesics");
    }

    public void Initialize(SurfaceMenu menu, Dictionary<string, Homeomorphism> homeomorphism)
    {
        Initialize(menu.surface, menu.canvas, menu.cameraManager, menu.mainMenu);
        menu.nextMenu = this;
        this.lastMenu = menu;
    }

    public void SetMode(int mode) => SetMode((MenuMode) mode);
    public void SetMode(MenuMode mode)
    {
        switch (Mode)
        {
            case MenuMode.AddCurve:
                currentWaypoints = null;
                currentCurveName = null;
                break;
        }
        Mode = mode;
    }
    
    private  (ITransformable, string)  PreviewDrawable(Point location, string drawingSurfaceName)
    {
        switch (Mode)
        {
            case MenuMode.AddCurve:
                var lastPoint = currentWaypoints?.LastOrDefault();
                if (lastPoint is null || location is null || location == lastPoint)
                    return (location, drawingSurfaceName);
                var homeo = surface.GetHomeomorphism(drawingSurfaceName, geodesicSurface.Name);
                return (geodesicSurface.GetGeodesic(lastPoint, location.ApplyHomeomorphism(homeo), "previewGeodesicSegment"), geodesicSurface.Name);
            default:
                return Drawable(location, drawingSurfaceName);
        }
    }

    private (ITransformable, string) Drawable(Point location, string drawingSurfaceName)
    {
        
        switch (Mode)
        {
            case MenuMode.AddCurve:
            {
                if (string.IsNullOrEmpty(currentCurveName))
                    // todo: ask user
                    currentCurveName = "Curve " + Random.Range(0, 100);
                currentWaypoints ??= new();
                var homeo = surface.GetHomeomorphism(drawingSurfaceName, geodesicSurface.Name);
                var pointInGeodesicSurface = location.ApplyHomeomorphism(homeo);
                currentWaypoints.Add(pointInGeodesicSurface);
                if (currentWaypoints.Count > 1)
                {
                    return (geodesicSurface.GetPathFromWaypoints(currentWaypoints.Append(pointInGeodesicSurface), currentCurveName), geodesicSurface.Name);
                }

                break;
            }
        }
        return (location, drawingSurfaceName);
    }



    public void MouseEvent(Point location, string surfaceName, int button)
    {
        if (button < 0)
        {
            var (drawable, drawingSurfaceName) = PreviewDrawable(location, surfaceName);
            Display(drawable, drawingSurfaceName, true);
        }

        if (button == 0)
        {
            var (drawable, drawingSurfaceName) = Drawable(location, surfaceName);
            Display(drawable, drawingSurfaceName, false);
        }
        // todo: add waypoints for curve
    }

    public void Display([CanBeNull] ITransformable input,
        string drawingSurfaceName,
        bool preview,
        bool propagateBackwards = true,
        bool propagateForwards = true,
        bool propagateToDrawingSurfaces = true,
        IEnumerable<string> skipDrawingSurfaces = null)
    {
        visualizers[drawingSurfaceName].Display(input, preview);

        propagateForwards = propagateForwards && nextMenu != null;
        string preferredForwardSurfaceName = forwardHomeos.Keys.FirstOrDefault();
        propagateBackwards = propagateBackwards && lastMenu != null;
        string preferredBackwardSurfaceName = backwardsHomeos.Keys.FirstOrDefault();
        // todo: different modes in the menu will show different things, e.g. drawing a curve by waypoints,
        // showing a grid, placing points, grids etc.
        if (!propagateToDrawingSurfaces) return;
        var propagateToDrawingSurfacesSet =
            surface.drawingSurfaces.Keys.ToHashSet();
        skipDrawingSurfaces ??= Enumerable.Empty<string>();
        propagateToDrawingSurfacesSet.ExceptWith(
                skipDrawingSurfaces.Append(drawingSurfaceName));  
        foreach (string otherDrawingSurfaceName in propagateToDrawingSurfacesSet)
        {
            var homeomorphism = surface.GetHomeomorphism(drawingSurfaceName, otherDrawingSurfaceName);
            var pt = input?.ApplyHomeomorphism(homeomorphism);  
            visualizers[otherDrawingSurfaceName].Display(pt, preview);
            if (propagateBackwards && backwardsHomeos.TryGetValue(otherDrawingSurfaceName, out var homeo)) 
                lastMenu.Display(pt?.ApplyHomeomorphism(homeo),
                    otherDrawingSurfaceName,
                    preview,
                    otherDrawingSurfaceName == preferredForwardSurfaceName,
                    false, 
                    otherDrawingSurfaceName == preferredBackwardSurfaceName, 
                    backwardsHomeos.Keys
                );
            if (propagateForwards && forwardHomeos.TryGetValue(otherDrawingSurfaceName, out homeo)) 
                nextMenu.Display(pt?.ApplyHomeomorphism(homeo),
                    otherDrawingSurfaceName,
                    preview,
                    false,
                    otherDrawingSurfaceName == preferredForwardSurfaceName, 
                    otherDrawingSurfaceName == preferredForwardSurfaceName,
                    forwardHomeos.Keys
                );
        }

    }

}

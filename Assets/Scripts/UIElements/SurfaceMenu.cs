using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public enum MenuMode
{
    AddPoint,
    AddCurve,
    PreviewGrid,
    SelectPoint,
    SelectCurve,
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
    private Dictionary<string, Homeomorphism> forwardHomeos = new();
    private Dictionary<string, Homeomorphism> backwardsHomeos = new();

    public AbstractSurface surface;
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
    [SerializeField] private Color currentCurveColor;
    private readonly HashSet<(IDrawnsformable, string)> currentStuffShown = new();
    [SerializeField] private Color previewPointColor;
    [SerializeField] private Color previewCurveColor;

    public event Action<IDrawnsformable, string> StuffShown;
    public event Action<IDrawnsformable, string> StuffDeleted;
    


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
            
            kamera.Initialize(panel, canvas, drawingSurface.MinimalPosition, drawingSurface.MaximalPosition);
            cameraManager.AddKamera(kamera);
            
            mainMenu.UIMoved += () => kamera.UIMoved();
            
            visualizers.Add(drawingSurfaceName, surfaceVisualizer);
            surfaceVisualizer.MouseEvent += (location, button) =>
                MouseEvent(drawingSurface.ClampPoint(location), drawingSurface.Name, button);
        }
        if (this.geodesicSurface is null)
            throw new Exception("None of the surfaces provides geodesics");
        
    }

    public void Initialize(SurfaceMenu lastMenu, Dictionary<string, Homeomorphism> automorphisms)
    {
        Initialize(lastMenu.surface, lastMenu.canvas, lastMenu.cameraManager, lastMenu.mainMenu);
        lastMenu.nextMenu = this;
        lastMenu.forwardHomeos = automorphisms;
        this.lastMenu = lastMenu;
        this.backwardsHomeos = automorphisms.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Inverse
        );
        foreach (var (drawnStuff, drawingSurfaceName) in lastMenu.currentStuffShown)
        {
            Display(drawnStuff, drawingSurfaceName, preview: false, propagateBackwards: false, propagateToDrawingSurfaces: false);
            // currentStuffShown should contain drawnStuff once for every DrawingSurface (transformed)! 
        }
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
    
    private  (IDrawnsformable, string) PreviewDrawable(Point location, string drawingSurfaceName)
    {
        switch (Mode)
        {
            case MenuMode.AddCurve:
                var lastPoint = currentWaypoints?.LastOrDefault();
                if (lastPoint is null || location is null || location == lastPoint)
                    return (location, drawingSurfaceName);
                var homeo = surface.GetHomeomorphism(drawingSurfaceName, geodesicSurface.Name);
                var previewSegment = geodesicSurface.GetGeodesic(lastPoint, location.ApplyHomeomorphism(homeo), "previewGeodesicSegment");
                previewSegment.Color = previewCurveColor;
                return (previewSegment, geodesicSurface.Name);
                // todo: ends for curves, ends at points (-> vertices)
            case MenuMode.SelectPoint when location is not null:
            case MenuMode.AddPoint when location is not null:
                location.Color = previewPointColor;
                return (location, drawingSurfaceName);
            case MenuMode.PreviewGrid:
                // todo: implement grid
                return (location, drawingSurfaceName); 
            default:
                return (null, drawingSurfaceName);
        }
    }

    private (IDrawnsformable, string) Drawable(Point location, string drawingSurfaceName)
    {
        if (location is null) return (null, drawingSurfaceName);
        switch (Mode) 
        {
            case MenuMode.AddCurve:
            {
                if (string.IsNullOrEmpty(currentCurveName)){
                    // todo: ask user
                    currentCurveName = (
                        from i in Enumerable.Range(0, 52)
                        select ((char)('a' + i)).ToString()
                    ).FirstOrDefault(
                        c => !surface.drawingSurfaces.ContainsKey(c)
                    );
                    currentCurveColor = new Color(Random.value, Random.value, Random.value);
                }
                currentWaypoints ??= new();
                var homeo = surface.GetHomeomorphism(drawingSurfaceName, geodesicSurface.Name);
                var pointInGeodesicSurface = location?.ApplyHomeomorphism(homeo);
                currentWaypoints.Add(pointInGeodesicSurface);
                if (currentWaypoints.Count > 1)
                {
                    Curve curve = geodesicSurface.GetPathFromWaypoints(currentWaypoints, currentCurveName);
                    curve.Color = currentCurveColor;
                    return (curve, geodesicSurface.Name);
                }

                goto case MenuMode.AddPoint;
            }
            case MenuMode.AddPoint:
                return (location, drawingSurfaceName);
            default:
                return (null, drawingSurfaceName);
        }
    }



    public void MouseEvent(Point location, string surfaceName, int button)
    {
        if (button < 0)
        {
            var (drawable, drawingSurfaceName) = PreviewDrawable(location, surfaceName);
            Display(drawable, drawingSurfaceName, preview: true);
        }

        if (button == 0)
        {
            var (drawable, drawingSurfaceName) = Drawable(location, surfaceName);
            Display(drawable, drawingSurfaceName, preview: false);
        }
        // todo: selection and deletion
    }

    /// <summary>
    /// Display the input on the drawing surface and display the transformed input on the other drawing surfaces.
    /// If not selected otherwise, this is also displayed on all other SurfaceMenus that are connected via homeomorphisms.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="drawingSurfaceName"></param>
    /// <param name="preview">Mark as preview. I.e. it will be overwritten by the next preview of the same type (Point, Curve)</param>
    /// <param name="remove"></param>
    /// <param name="propagateBackwards">View the transformed versions on the previous SurfaceMenus</param>
    /// <param name="propagateForwards">View the transformed versions on the next SurfaceMenus</param>
    /// <param name="propagateToDrawingSurfaces">View the transformed versions on the other drawing surfaces</param>
    /// <param name="skipDrawingSurfaces">Do not propagate to these drawing surfaces</param>
    public void Display([CanBeNull] IDrawnsformable input,
        string drawingSurfaceName = null,
        bool preview = false,
        bool remove = false,
        bool propagateBackwards = true,
        bool propagateForwards = true,
        bool propagateToDrawingSurfaces = true,
        IEnumerable<string> skipDrawingSurfaces = null)
    {
        drawingSurfaceName ??= geodesicSurface.Name;
        propagateForwards = propagateForwards && nextMenu != null;
        string preferredForwardSurfaceName = forwardHomeos.Keys.FirstOrDefault();
        propagateBackwards = propagateBackwards && lastMenu != null;
        string preferredBackwardSurfaceName = backwardsHomeos.Keys.FirstOrDefault();
        var propagateToDrawingSurfacesSet = new HashSet<string>();
        if (propagateToDrawingSurfaces)
            propagateToDrawingSurfacesSet = surface.drawingSurfaces.Keys.ToHashSet();
        skipDrawingSurfaces ??= Enumerable.Empty<string>();
        propagateToDrawingSurfacesSet.ExceptWith(skipDrawingSurfaces);  
        propagateToDrawingSurfacesSet.Add(drawingSurfaceName);
        foreach (string otherDrawingSurfaceName in propagateToDrawingSurfacesSet)
        {
            var homeomorphism = surface.GetHomeomorphism(drawingSurfaceName, otherDrawingSurfaceName);
            var drawable = input?.ApplyHomeomorphism(homeomorphism);
            if (remove)
            {
                visualizers[otherDrawingSurfaceName].Remove(drawable);
                if (!preview && currentStuffShown.Remove((drawable, otherDrawingSurfaceName))) 
                    StuffDeleted?.Invoke(drawable, otherDrawingSurfaceName);
            }
            else
            {
                visualizers[otherDrawingSurfaceName].Display(drawable, preview);
                currentStuffShown.Add((drawable, otherDrawingSurfaceName));
                StuffShown?.Invoke(drawable, otherDrawingSurfaceName);
            }
                
            if (propagateBackwards && backwardsHomeos.TryGetValue(otherDrawingSurfaceName, out var homeo)) 
                lastMenu.Display(drawable?.ApplyHomeomorphism(homeo),
                    otherDrawingSurfaceName,
                    preview: preview,
                    remove: remove,
                    propagateBackwards: otherDrawingSurfaceName == preferredForwardSurfaceName,
                    propagateForwards: false, 
                    propagateToDrawingSurfaces: otherDrawingSurfaceName == preferredBackwardSurfaceName, 
                    skipDrawingSurfaces: backwardsHomeos.Keys
                );
            if (propagateForwards && forwardHomeos.TryGetValue(otherDrawingSurfaceName, out homeo)) 
                nextMenu.Display(drawable?.ApplyHomeomorphism(homeo),
                    otherDrawingSurfaceName,
                    preview: preview,
                    remove: remove,
                    propagateBackwards: false,
                    propagateForwards: otherDrawingSurfaceName == preferredForwardSurfaceName, 
                    propagateToDrawingSurfaces: otherDrawingSurfaceName == preferredForwardSurfaceName,
                    skipDrawingSurfaces: forwardHomeos.Keys
                );
        }

    }

    public (IDrawnsformable, string) GetCurve(string name) => currentStuffShown.FirstOrDefault(
        c => c.Item1 is Curve
    );
}

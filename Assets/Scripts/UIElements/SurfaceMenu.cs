using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;


/// <summary>
/// This is also AbstractSurfaceVisualizer in a sense. This should potentially be split into two classes.
/// </summary>
public class SurfaceMenu: MonoBehaviour 
{
    
    public AbstractSurface surface;
    public GameObject parametricSurfaceVisualizerUIPrefab;
    public GameObject parametricSurfaceVisualizerPrefab;
    public GameObject modelSurfaceVisualizerUIPrefab;
    private RectTransform canvas;

    readonly Dictionary<string, SurfaceVisualizer> visualizers = new();
    private CameraManager cameraManager;
    [SerializeField] private MainMenu mainMenu;

    public void Initialize(AbstractSurface surface, RectTransform canvas, CameraManager cameraManager, MainMenu mainMenu)
    {
        this.canvas = canvas;
        this.surface = surface;
        this.cameraManager = cameraManager;
        this.mainMenu = mainMenu;
        foreach (var (name, drawingSurface) in surface.drawingSurfaces)
        {

            SurfaceVisualizer surfaceVisualizer;
            switch (drawingSurface)
            {
                case ParametricSurface parametricSurface:
                {
                    var surfaceVisualizerUI = Instantiate(parametricSurfaceVisualizerUIPrefab, transform);
                    var panel = surfaceVisualizerUI.GetComponentInChildren<RawImage>();
                    var surfaceVisualizerGameObject = Instantiate(parametricSurfaceVisualizerPrefab);
                    var parametricSurfaceVisualizer = 
                        surfaceVisualizerGameObject.GetComponentInChildren<ParametricSurfaceVisualizer>();
                    parametricSurfaceVisualizer.Initialize(parametricSurface);
                    surfaceVisualizer = parametricSurfaceVisualizer;
                    
                    var kamera = surfaceVisualizerGameObject.GetComponentInChildren<UICamera>();
                    kamera.Initialize(panel, canvas);
                    cameraManager.AddKamera(kamera);
                    mainMenu.UIMoved += () => kamera.UIMoved();
                    break;
                }
                case ModelSurface modelSurface:
                {
                    var surfaceVisualizerUI = Instantiate(modelSurfaceVisualizerUIPrefab, transform);
                    var modelSurfaceVisualizer = surfaceVisualizerUI.GetComponentInChildren<ModelSurfaceVisualizer>();
                    modelSurfaceVisualizer.Initialize(modelSurface);
                    surfaceVisualizer = modelSurfaceVisualizer;
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
            
            visualizers.Add(name, surfaceVisualizer);
            surfaceVisualizer.MouseHover += location => MouseHover(drawingSurface.ClampPoint(location), drawingSurface.Name);
        }
    }

    private void MouseHover([CanBeNull] Point input, string drawingSurfaceName)
    {
        // todo: different modes in the menu will show different things, e.g. drawing a curve by waypoints, showing a grid, placing points, grids etc.
        foreach (var (name, drawingSurface) in surface.drawingSurfaces)
        {
            var homeomorphism = surface.GetHomeomorphism(drawingSurfaceName, name);
            visualizers[name].MovePointTo(input?.ApplyHomeomorphism(homeomorphism));
        }
    }
}

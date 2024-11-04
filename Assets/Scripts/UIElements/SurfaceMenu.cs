using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[Serializable]
public struct SurfaceParameter
{
    // todo: this should be split into two tpyes:
    // One being basically the input for the constructor of Modelsurface
    // One describing embedded surfaces, e.g. where the genera are. This affects only the embeddings (homeomorphisms)
    public int genus, punctures;
    public bool modelSurface;
    // todo

    public static SurfaceParameter FromString(string s) =>
        s switch
        {
            "Torus2D" => new() { genus = 1, punctures = 0, modelSurface = true },
            "Torus3D" => new() { genus = 1, punctures = 0, modelSurface = false },
            _ => throw new NotImplementedException()
        };
}

public class SurfaceMenu: MonoBehaviour 
{
    public AbstractSurface surface;
    public GameObject surfaceVisualizerUIPrefab;
    public GameObject parametricSurfaceVisualizerPrefab;
    public GameObject modelSurfaceVisualizerPrefab;
    private RectTransform canvas;

    public void Initialize(string parameterString, RectTransform canvas) // for UI calls
        => Initialize(from s in parameterString.Split(";") select SurfaceParameter.FromString(s), canvas);

    public void Initialize(IEnumerable<SurfaceParameter> parameters, RectTransform canvas)
    {
        this.canvas = canvas;
        surface = SurfaceGenerator.CreateSurface(this, parameters);
        foreach (var (name, drawingSurface) in surface.drawingSurfaces)
        {
            var surfaceVisualizerUI = Instantiate(surfaceVisualizerUIPrefab, transform);
            
            
            GameObject surfaceVisualizerGameObject = null;
            if (drawingSurface is ParametricSurface parametricSurface)
            {
                surfaceVisualizerGameObject = Instantiate(parametricSurfaceVisualizerPrefab);
                var surfaceVisualizer =
                    surfaceVisualizerGameObject.GetComponentInChildren<ParametricSurfaceVisualizer>();
                surfaceVisualizer.Initialize(parametricSurface);
            }
            else if (drawingSurface is ModelSurface modelSurface)
            {
                surfaceVisualizerGameObject = Instantiate(modelSurfaceVisualizerPrefab);
            }
            else
                throw new NotImplementedException();
            
            var camera = surfaceVisualizerGameObject.GetComponentInChildren<UICamera>();
            var panel = surfaceVisualizerUI.GetComponentInChildren<RawImage>();
            camera.Initialize(panel, canvas);


        }
    }
}

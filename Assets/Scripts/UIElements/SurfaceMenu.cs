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

    public static SurfaceParameter FromString(string s)
    {
        if (s.StartsWith("g="))
            return new() { genus = int.Parse(s.Substring(2)), punctures = 0, modelSurface = false }; // todo
        if (s == "Torus2D")
            return new() { genus = 1, punctures = 0, modelSurface = true };
        if (s == "Torus3D")
            return new() { genus = 1, punctures = 0, modelSurface = false };
        throw new NotImplementedException();
    }
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
            var panel = surfaceVisualizerUI.GetComponentInChildren<RawImage>();

            
            switch (drawingSurface)
            {
                case ParametricSurface parametricSurface:
                {
                    var surfaceVisualizerGameObject = Instantiate(parametricSurfaceVisualizerPrefab);
                    var surfaceVisualizer =
                        surfaceVisualizerGameObject.GetComponentInChildren<ParametricSurfaceVisualizer>();
                    surfaceVisualizer.Initialize(parametricSurface);
                    var kamera = surfaceVisualizerGameObject.GetComponentInChildren<UICamera>();
                    kamera.Initialize(panel, canvas);
                    break;
                }
                case ModelSurface modelSurface:
                {
                    var surfaceVisualizer = panel.gameObject.AddComponent<ModelSurfaceVisualizer>();
                    surfaceVisualizer.Initialize(modelSurface, panel.rectTransform);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
            


        }
    }
}

using UnityEngine;
using UnityEngine.UI;

public class ModelSurfaceVisualizer : SurfaceVisualizer
{
    private ModelSurface surface;
    [SerializeField] private RawImage drawingArea;

    // these features are basically already redundant. We don't use UI for the visualization of the Model Surfaces, 
    // because it is harder and this way is basically the same as for the 3D surfaces


    public void Initialize(ModelSurface surface, RawImage drawingArea, float scale = 1f, Vector2 offset = default)
    {
        this.surface = surface;
        var tooltipTarget = GetComponentInChildren<TooltipTarget>(); // tooltipTarget is on a quad that is a child. It is scaled and offset, thus its transform is bad for transforming hit to local coords (we only want to subtract the (id*100, 0, 0))
        tooltipTarget.Initialize(this, transformForHitCoordsToLocal: transform); // drawingArea.rectTransform);
        this.scale = scale;
        imageOffset = offset; 
        // ?? (surface.MaximalPosition - surface.MinimalPosition) / 2;
        // Mathf.Min(
        //     drawingArea.rectTransform.rect.width / surface.width,
        //     drawingArea.rectTransform.rect.height / surface.height
        // );
    }
}
using UnityEngine;
using UnityEngine.UI;

public class ModelSurfaceVisualizer : SurfaceVisualizer
{
    private Surface surface;

    // these features are basically already redundant. We don't use UI for the visualization of the Model Surfaces, 
    // because it is harder and this way is basically the same as for the 3D surfaces


    public void Initialize(Surface surface, Camera camera = null, float scale = 1f, Vector2 imageOffset = default)
    {
        base.Initialize(camera);
        this.surface = surface;
        this.scale = scale;
        this.imageOffset = imageOffset; 
        var tooltipTarget = GetComponentInChildren<TooltipTarget>(); // tooltipTarget is on a quad that is a child. It is scaled and offset, thus its transform is bad for transforming hit to local coords (we only want to subtract the (id*100, 0, 0))
        tooltipTarget.Initialize(this, transformForHitCoordsToLocal: transform); 
        var background = transform.GetChild(0);
        var size = (surface.MaximalPosition - surface.MinimalPosition) * 1.5f;
        var center = (surface.MaximalPosition + surface.MinimalPosition) / 2;
        background.localScale = new Vector3(size.x, size.y, 1);
        background.localPosition = new Vector3(center.x, center.y, 0);
        
        gameObject.name = surface.Name;
    }
}
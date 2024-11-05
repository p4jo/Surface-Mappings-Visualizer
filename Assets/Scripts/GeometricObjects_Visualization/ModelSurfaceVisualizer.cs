using UnityEngine;
using UnityEngine.UI;

public class ModelSurfaceVisualizer : SurfaceVisualizer
{
    private ModelSurface surface;
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RawImage drawingArea;
    [SerializeField] private float scale = 1f;
    
    public override void MovePointTo(Point point)
    {
        if (point == null)
        {
            pointer.gameObject.SetActive(false);
            return;
        }
        pointer.gameObject.SetActive(true);
        pointer.localPosition = point.Position * scale;
        // todo: several positions => several pointers
    } // todo: several points as well

    public void Initialize(ModelSurface surface)
    {
        this.surface = surface;
        var tooltipTarget = GetComponent<TooltipTarget>();
        tooltipTarget.Initialize(this, drawingArea.rectTransform);
        scale = Mathf.Min(
            drawingArea.rectTransform.rect.width / surface.width,
            drawingArea.rectTransform.rect.height / surface.height);
    }
}
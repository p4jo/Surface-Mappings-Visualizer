using UnityEngine;
using UnityEngine.UI;

public class ModelSurfaceVisualizer : SurfaceVisualizer
{
    private ModelSurface surface;
    [SerializeField] private Transform pointer;
    [SerializeField] private RawImage drawingArea;
    [SerializeField] private float scale = 1f;
    [SerializeField] private Vector2 imageOffset;

    public Vector2 displayPosition(Vector2 surfacePosition) => surfacePosition * scale + imageOffset;
    // these features are basically already redundant. We don't use UI for the visualization of the Model Surfaces, 
    // because it is harder and this way is basically the same as for the 3D surfaces
    public Vector2 surfacePosition(Vector2 displayPosition) => (displayPosition - imageOffset) / scale;
    
    public override void MovePointTo(Point point)
    {
        if (point == null)
        {
            pointer.gameObject.SetActive(false);
            return;
        }
        pointer.gameObject.SetActive(true);
        pointer.localPosition = displayPosition(point.Position);
        // todo: several positions => several pointers
    // todo: several points as well
    }
    
    
    protected override void AddPoint(Point point)
    {
        var newPointer = Instantiate(pointer.gameObject, pointer.transform.parent);
        newPointer.transform.localPosition = displayPosition(point.Position);
    }


    public override void AddCurve(Curve curve)
    {
        AddCurveVisualizer(curve.Name).Initialize(curve, 0.2f, scale, imageOffset);
    }

    public override void OnHover(Kamera activeKamera, Vector3 position) => 
        base.OnHover(activeKamera, surfacePosition(position));

    public override void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => 
        base.OnClick(activeKamera, surfacePosition(position), mouseButton);

    public void Initialize(ModelSurface surface, RawImage drawingArea, Vector2 offset = new())
    {
        this.surface = surface;
        var tooltipTarget = GetComponentInChildren<TooltipTarget>();
        tooltipTarget.Initialize(this, null); // drawingArea.rectTransform);
        scale = 1f; 
        imageOffset = offset;
        // Mathf.Min(
        //     drawingArea.rectTransform.rect.width / surface.width,
        //     drawingArea.rectTransform.rect.height / surface.height
        // );
    }
}
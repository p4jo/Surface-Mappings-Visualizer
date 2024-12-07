using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UICamera : Kamera {
    [SerializeField] RectTransform renderRectTransform;
    Rect renderRect => renderRectTransform.rect;
    [SerializeField] RawImage renderTarget;
    [SerializeField] RenderTexture renderTexture;
    [SerializeField] RectTransform canvas;
    [SerializeField] Vector2 screenOffset;
    [SerializeField] Vector2 canvasOffset;
    [SerializeField] Vector2 canvasToScreenScale = new(1,1);
    [SerializeField] Vector2 screenToCameraOutputScale = new(1,1);
    float stopCheckingTime;

    void Start() => Activate(); // Deactivate();

    private new void Awake() {
        base.Awake();
        // if (renderRectTransform == null) renderRectTransform = renderTarget.gameObject.GetComponent<RectTransform>();
    }

    public void Initialize(RawImage renderTarget, RectTransform canvas, Vector3 minimalPosition, Vector3 maximalPosition)
    {
        base.Initialize(minimalPosition, maximalPosition);
        this.canvas = canvas;
        this.renderTarget = renderTarget;
        renderRectTransform = renderTarget.GetComponent<RectTransform>();
        UIMoved();
    }
    
    public void UIMoved(bool offscreen = false) {
        if (offscreen) Deactivate();
        else Activate();
        ContinueCheckingPosition();

    }

    void ContinueCheckingPosition() {
        stopCheckingTime = Time.time + 0.5f; // coroutine just needed bc the side menu is still slightly shifted when the event is called
        StartCoroutine(checkPosition());
        IEnumerator checkPosition() {
            while (Time.time <= stopCheckingTime) {
                yield return null; 
                FixScale();
            }
        }
    }


    void Activate() {
        //cam.targetTexture.Release();
        //cam.targetTexture = null;
        FixScale();
        Cam.enabled = true;
        //cam.rect = normalizeRectToCanvasViewport(renderRectTransform);
    }

    //Rect normalizeRectToCanvasViewport(RectTransform rect) { // ZUM VERZWEIFELN
    //    var screenRect = canvas.GetComponent<RectTransform>().rect;
    //    return new Rect(
    //        rect.position.x / screenRect.width,   
    //        1 - rect.position.y / screenRect.height,
    //        rect.rect.width / screenRect.width,
    //        rect.rect.height / screenRect.height
    //    );
    //}



    void Deactivate() {
        FixScale();
        Cam.targetTexture.Create();
        Cam.enabled = false;
    }

    public override bool IsMouseInViewport(Vector3 mousePosition, bool ignoreSubCameras = false) {
        return renderRect.Contains(renderRectTransform.InverseTransformPoint(mousePosition)) || 
               (!ignoreSubCameras && childKamera != null && childKamera.IsMouseInViewport(mousePosition, true));
    }

    
    public override Ray ScreenPointToRay(Vector3 mousePosition) {
        if (Input.GetKeyUp(KeyCode.Alpha1)) FixScale();
        // transform.position is in pixels on the current screen, from the bottom
        // canvasOffset is in pixels on the canvas (1920�1080)
        // screenOffset is in pixels on the current screen
        // mousePosition is in pixels on the current screen, from the bottom
        // pos is in pixels on the canvas, from the bottom
        Ray res = new();
        Vector3 pos = new(
            (mousePosition.x - screenOffset.x) * screenToCameraOutputScale.x - canvasOffset.x,
            (mousePosition.y - screenOffset.y) * screenToCameraOutputScale.y - canvasOffset.y);
        
        if (float.IsFinite(pos.x) && float.IsFinite(pos.y))
            res = Cam.ScreenPointToRay(pos);

        if (Input.GetKeyUp(KeyCode.Space))
            Debug.Log("mouse pos:" + mousePosition +
                      ", scaled offset pos: " + pos +
                      ", rect:" + renderRectTransform.rect +
                      ", at" + renderRectTransform.position + 
                      ", scaled Rect:" + new Vector2(renderRect.width * canvasToScreenScale.x, renderRect.height * canvasToScreenScale.y) +
                      ", ray:" + res);

        return res;
    }

    void FixScale()
    {
        if (canvas == null) return;
        if (renderRectTransform == null) return;
        if (renderTarget == null) return;
        canvasToScreenScale = new(
           Screen.width / canvas.rect.width,   
           Screen.height / canvas.rect.height 
        );
        screenOffset = new(
            renderRectTransform.position.x - renderRect.width * canvasToScreenScale.x / 2,
            renderRectTransform.position.y - renderRect.height * canvasToScreenScale.y / 2
            // !!! THEY SEEM TO HAVE CHANGED WHAT RectTransform.position MEANS : It's now the center, not the bottom left
            // old is still good with Unity 2023.6, but not with 6000.0
            // alternative: https://docs.unity3d.com/ScriptReference/RectTransform.GetWorldCorners.html
        );
        if (FixRenderTexture())
            screenToCameraOutputScale = new(
                renderRect.width * canvasToScreenScale.x / renderTexture.width,
                renderRect.height * canvasToScreenScale.y / renderTexture.height
            );
    }


    bool FixRenderTexture() {
        var width = Mathf.RoundToInt(Mathf.Max(100, renderRect.width * canvasToScreenScale.x));
        var height = Mathf.RoundToInt(Mathf.Max(100, renderRect.height * canvasToScreenScale.y));
        if (renderTexture != null && !renderTexture.ToString().Equals("null") && renderTexture.height == height && renderTexture.width == width)
            return false;
        
        
        if (renderTexture != null) Destroy(renderTexture);
        renderTexture = new(width, height, 24) {
            // antiAliasing = 4
        };
        renderTarget.texture = Cam.targetTexture = renderTexture;
        return true;
    }
}

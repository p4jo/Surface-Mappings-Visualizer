using UnityEngine;
using TMPro;
using UnityEngine.UI;

/**
    * This class is responsible for managing the tooltip that appears when hovering over a group in the group gallery.
    * Inspired by https://www.youtube.com/watch?v=y2N_J391ptg
    */
public class TooltipManager : MonoBehaviour {

    public static TooltipManager Instance { get; private set; }

    [SerializeField] private RectTransform tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private CameraManager cameraManager;
    // [SerializeField] float hoverTime = 1f;

    private TooltipContent content;
    private float timer;
    private bool uiActivated;
    private bool objectActivated;
    private bool Activated => uiActivated || objectActivated;
    private bool isActive;
    private int layerMask;
    private Transform lastHoverObject;
    private ITooltipOnHover lastTooltipThing;
    private Vector3 lastHoverPosition;

    private void Awake() {
        // bug: Unity Objects might not be "alive" anymore, even though their C# object is not null. 
        // You then get Errors when calling things like GetComponent on them.
        // Go to definition of == below: this function is stupid...
        // if both objects aren't null (is not null) independent of if they are alive (!= null), it returns equality of m_InstanceID
        // but here, there is a previous value for Instance (why? I don't know), and this is not null but is not alive...
        if (Instance == null) {
            Instance = this;
            return;
        }
        Destroy(gameObject);
    }

    private void Start() {
        Cursor.visible = true;
        tooltipPanel.gameObject.SetActive(false);
    }

    private void Update()
    {

        var mousePosition = Input.mousePosition;
        if (!uiActivated && cameraManager.TryGetKamera(mousePosition, out var kamera)) { // UI elements have priority

            Ray ray = kamera.ScreenPointToRay(mousePosition);

            layerMask = kamera.cullingMask; // LayerMask.GetMask("TooltipObjects");

            // Debug.DrawRay(ray.origin, ray.direction.normalized * 2000, Color.yellow,0.1f);

            if (Physics.Raycast(ray, out var hit, maxDistance: 2000, layerMask)) { 
                var tooltipObject = hit.transform;
                lastHoverPosition = hit.point;
                Debug.DrawLine(ray.origin, hit.point, Color.red, 0.1f);
                if (tooltipObject != lastHoverObject) {
                    OnHoverEnd(lastTooltipThing);
                    lastHoverObject = tooltipObject;
                }
                // moved out of the if; now OnHover is called every frame!!
                if (tooltipObject.TryGetComponent<ITooltipOnHover>(out var tooltipThing))
                    OnHover(tooltipThing, lastHoverPosition, kamera);
            }
            else {
                OnHoverEnd(lastTooltipThing);
                lastHoverObject = null;
            }
        }

        if (!isActive && Activated && Time.time >= timer + lastTooltipThing.hoverTime) {
            ShowTooltip();
            isActive = true;
        }

        // if (!isActive) return;
        // now you can also click if the tooltip is not yet shown

        int mouseButtonPressed = -1;
        if (Input.GetMouseButtonUp(0)) mouseButtonPressed = 0;
        if (Input.GetMouseButtonUp(1)) mouseButtonPressed = 1;
        if (Input.GetMouseButtonUp(2)) mouseButtonPressed = 2;
        
        if (mouseButtonPressed != -1) {
            if (objectActivated) {
                if (cameraManager.TryGetKamera(mousePosition, out kamera)) 
                    lastTooltipThing?.OnClick(kamera, lastHoverPosition, mouseButtonPressed);
            } else if (uiActivated)
            {
                lastTooltipThing?.OnClick(null, lastHoverPosition, mouseButtonPressed);
            }
        }


        tooltipPanel.position = new(mousePosition.x + 10, mousePosition.y - 10);
    }

    public void OnHover(ITooltipOnHover tooltipThing, Vector3 position, Kamera kamera = null) {
        if (tooltipThing != lastTooltipThing)
            timer = Time.time;
        lastTooltipThing = tooltipThing;
        content = tooltipThing.GetTooltip();
        uiActivated = !kamera;
        objectActivated = !uiActivated;
        lastHoverPosition = position;
        tooltipThing.OnHover(kamera, position);
    }

    public void ShowTooltip(string text = null, float timeout = float.PositiveInfinity)
    {
        text ??= content.text ?? "";
        if (!string.IsNullOrWhiteSpace(content.url)) 
            text += "\n<b>Right click for more info.</b>";

        if (string.IsNullOrWhiteSpace(text))
            return;
        tooltipPanel.gameObject.SetActive(true);
        tooltipText.text = text;

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
        tooltipPanel.SetAsLastSibling();
        
        if (timeout < float.PositiveInfinity && timeout > 0) {
            Invoke(nameof(HideTooltip), timeout);
        }
    }

    void HideTooltip() => tooltipPanel.gameObject.SetActive(false);

    public void OnHoverEnd(ITooltipOnHover tooltipThing) {
        tooltipThing?.OnHoverEnd();
        if (lastTooltipThing != tooltipThing)
            return;
        uiActivated = false;
        objectActivated = false;
        isActive = false;
        HideTooltip();
        lastTooltipThing = null;
        lastHoverObject = null;
    }
}
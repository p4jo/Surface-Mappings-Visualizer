using UnityEngine;
using TMPro;
using UnityEngine.UI;

/**
    * This class is responsible for managing the tooltip that appears when hovering over a group in the group gallery.
    * Inspired by https://www.youtube.com/watch?v=y2N_J391ptg
    */
public class TooltipManager : MonoBehaviour {

    public static TooltipManager Instance { get; private set; }

    [SerializeField] RectTransform tooltipPanel;
    [SerializeField] TMP_Text tooltipText;
    [SerializeField] CameraManager cameraManager;
    // [SerializeField] float hoverTime = 1f;
    
    TooltipContent content;
    float timer;
    bool uiActivated;
    bool objectActivated;
    bool Activated => uiActivated || objectActivated;
    bool isActive;
    int layerMask;
    Transform lastHoverObject;
    ITooltipOnHover lastTooltipThing;
    private Vector3 lastHoverPosition;

    void Awake() {
        if (Instance == this) 
            return;
        if (Instance == null) {
            Instance = this;
            return;
        }
        Destroy(gameObject);
    }

    void Start() {
        Cursor.visible = true;
        tooltipPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.K))
        {
            int a;
            a = 1;
        }

        var mousePosition = Input.mousePosition;
        if (!uiActivated && cameraManager.TryGetKamera(mousePosition, out var kamera)) { // UI elements have priority

            Ray ray = kamera.ScreenPointToRay(mousePosition);

            layerMask = kamera.cullingMask; // LayerMask.GetMask("TooltipObjects");

            Debug.DrawRay(ray.origin, ray.direction.normalized * 2000, Color.yellow,0.1f);

            if (Physics.Raycast(ray, out var hit, maxDistance: 2000, layerMask)) { 
                var tooltipObject = hit.transform;
                lastHoverPosition = hit.point;
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

    void ShowTooltip()
    {
        var text = content.text ?? "";
        if (!string.IsNullOrWhiteSpace(content.url)) 
            text += "\n<b>Right click for more info.</b>";

        if (string.IsNullOrWhiteSpace(text))
            return;
        tooltipPanel.gameObject.SetActive(true);
        tooltipText.text = text;

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
        tooltipPanel.SetAsLastSibling();
    }



    public void OnHoverEnd(ITooltipOnHover tooltipThing) {
        tooltipThing?.OnHoverEnd();
        if (lastTooltipThing != tooltipThing)
            return;
        uiActivated = false;
        objectActivated = false;
        isActive = false;
        tooltipPanel.gameObject.SetActive(false);
        lastTooltipThing = null;
        lastHoverObject = null;
    }
}
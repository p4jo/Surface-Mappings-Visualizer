using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;


public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ITooltipOnHover
{
    public ITooltipOnHover tooltipThing;
    public bool continuous, hovering;
    /// <summary>
    /// This is for a tooltip that is on a UI object
    /// Leave as null if this is on a GameObject in the 3D scene.
    /// This rescales the mouse position
    /// </summary>
    [FormerlySerializedAs("rectTransform")] public RectTransform rectTransformForScreenCoordsToLocal;

    public Transform transformForHitCoordsToLocal;

    void Update()
    {
        if (tooltipThing == null) 
            Debug.LogError("TooltipTarget must be initialized");
    }
    
    public void Initialize(ITooltipOnHover tooltipThing, RectTransform rectTransform = null, Transform transformForHitCoordsToLocal = null)
    {
        this.tooltipThing = tooltipThing;
        this.rectTransformForScreenCoordsToLocal = rectTransform;
        this.transformForHitCoordsToLocal = transformForHitCoordsToLocal;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovering(eventData.position);
        if (!continuous) return;
        hovering = true;
        StartCoroutine(KeepHovering());
        return;

        IEnumerator KeepHovering()
        {
            while (hovering)
            {
                Hovering(Input.mousePosition);
                yield return null;
            }
        }
    }

    private void Hovering(Vector2 position)
    {
        if (rectTransformForScreenCoordsToLocal != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransformForScreenCoordsToLocal,
                position,
                null,
                out position);
        TooltipManager.Instance?.OnHover(tooltipThing, position);
    }

    public void OnPointerExit(PointerEventData eventData) {
        hovering = false;
        TooltipManager.Instance?.OnHoverEnd(tooltipThing);
    }
    
    public TooltipContent GetTooltip() => tooltipThing.GetTooltip();

    public void OnHover(Kamera activeKamera, Vector3 position) => tooltipThing.OnHover(activeKamera, 
        transformForHitCoordsToLocal != null ?
            transformForHitCoordsToLocal.InverseTransformPoint(position) :
            position
    );

    public void OnHoverEnd() => tooltipThing.OnHoverEnd();

    public void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => tooltipThing.OnClick(activeKamera, 
        transformForHitCoordsToLocal != null ?
            transformForHitCoordsToLocal.InverseTransformPoint(position) :
            position, mouseButton);

    public float hoverTime => tooltipThing.hoverTime;
}
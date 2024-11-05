using UnityEngine;
using UnityEngine.EventSystems;


public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ITooltipOnHover
{
    public ITooltipOnHover tooltipThing;
    /// <summary>
    /// This is for a tooltip that is on a UI object
    /// Leave as null if this is on a GameObject in the 3D scene
    /// </summary>
    public RectTransform rectTransform;
    
    public void Initialize(ITooltipOnHover tooltipThing, RectTransform rectTransform = null)
    {
        this.tooltipThing = tooltipThing;
        this.rectTransform = rectTransform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var position = eventData.position;
        if (rectTransform != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                null,
                out position);
        TooltipManager.Instance?.OnHover(tooltipThing, position);
    }

    public void OnPointerExit(PointerEventData eventData) {
        TooltipManager.Instance?.OnHoverEnd(tooltipThing);
    }
    
    public TooltipContent GetTooltip() => tooltipThing.GetTooltip();

    public void OnHover(Kamera activeKamera, Vector3 position) => tooltipThing.OnHover(activeKamera, position);

    public void OnHoverEnd() => tooltipThing.OnHoverEnd();

    public void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => tooltipThing.OnClick(activeKamera, position, mouseButton);

    public float hoverTime => tooltipThing.hoverTime;
}
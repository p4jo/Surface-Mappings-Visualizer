using UnityEngine;
using UnityEngine.EventSystems;


public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ITooltipOnHover {
    public TooltipContent content = new();
    /// <summary>
    /// This is for a tooltip that is on a UI object
    /// Leave as null if this is on a GameObject in the 3D scene
    /// </summary>
    public RectTransform rectTransform;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        var position = eventData.position;
        if (rectTransform != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                null,
                out position);
        TooltipManager.Instance?.OnHover(this, position);
    }

    public void OnPointerExit(PointerEventData eventData) {
        TooltipManager.Instance?.OnHoverEnd(this);
    }

    public TooltipContent GetTooltip() => content;
    public virtual void OnHover(Kamera activeKamera, Vector3 position) { }

    public virtual void OnHoverEnd() { }
    public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton)
    { }

    public virtual float hoverTime => 1;
}
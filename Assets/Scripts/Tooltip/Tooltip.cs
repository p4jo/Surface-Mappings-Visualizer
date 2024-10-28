using UnityEngine;
using UnityEngine.EventSystems;


public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ITooltipOnHover {
    public TooltipContent content = new();

    public void OnPointerEnter(PointerEventData eventData) {
        TooltipManager.Instance?.OnHover(this);
    }

    public void OnPointerExit(PointerEventData eventData) {
        TooltipManager.Instance?.OnHoverEnd();
    }

    public TooltipContent GetTooltip() => content;
    public virtual void OnHover(Kamera activeKamera, Vector3 position) { }

    public virtual void OnHoverEnd() { }
    public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton)
    { }

    public virtual float hoverTime => 1;
}
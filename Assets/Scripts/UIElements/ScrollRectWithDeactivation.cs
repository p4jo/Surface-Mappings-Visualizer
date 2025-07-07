using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ScrollRectWithDeactivation : ScrollRect
{
    public bool interactionAllowed = true;
    
    public void SetInteractionAllowed(bool allowed)
    {
        interactionAllowed = allowed;
    }
    
    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (interactionAllowed) 
            base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        // if (interactionAllowed) 
            base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (interactionAllowed)
            base.OnEndDrag(eventData);
    }
}
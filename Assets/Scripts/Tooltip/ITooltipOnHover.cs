using System;
using UnityEngine;

public interface ITooltipOnHover {
    TooltipContent GetTooltip();
    void OnHover(Kamera activeKamera, Vector3 position);
    void OnHoverEnd();
    void OnClick(Kamera activeKamera, Vector3 position, int mouseButton);
    float hoverTime { get; }
}

[Serializable]
public struct TooltipContent {
    public string text;
    public string url;
}

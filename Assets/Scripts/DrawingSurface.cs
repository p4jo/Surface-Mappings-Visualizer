using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class DrawingSurface : MonoBehaviour, ITooltipOnHover
{
    public string Name { get; protected set; }
    public int Genus { get; protected set; }
    public readonly List<Vector3> punctures = new();
    public readonly bool is2D;

    protected DrawingSurface(string name, int genus, bool is2D)
    {
        this.Name = name;
        this.Genus = genus;
        this.is2D = is2D;
    }

    public event Action<Vector3?> MouseHover;
    
    public virtual TooltipContent GetTooltip() => new(); // don't actually show a tooltip
    public virtual void OnHover(Kamera activeKamera, Vector3 position) => MouseHover?.Invoke(position);
    public virtual void OnHoverEnd() => MouseHover?.Invoke(null);
    public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => MouseHover?.Invoke(position);
    public virtual float hoverTime => 0;
}


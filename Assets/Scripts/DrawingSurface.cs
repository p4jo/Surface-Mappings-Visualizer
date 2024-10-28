using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class DrawingSurface : MonoBehaviour, ITooltipOnHover
{
    public new readonly string name;
    public readonly int genus;
    public readonly List<Vector3> punctures = new();

    protected DrawingSurface(string name, int genus)
    {
        this.name = name;
        this.genus = genus;
    }

    public event Action<Vector3?> MouseHover;
    
    public virtual TooltipContent GetTooltip() => new(); // don't actually show a tooltip
    public virtual void OnHover(Kamera activeKamera, Vector3 position) => MouseHover?.Invoke(position);
    public virtual void OnHoverEnd() => MouseHover?.Invoke(null);
    public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => MouseHover?.Invoke(position);
    public virtual float hoverTime => 0;
}


public readonly struct Homeomorphism
{
    public readonly DrawingSurface source, target; // relatively unnecessary
    private readonly Func<Vector3, Vector3> f;
    public Vector3? F(Vector3? pos) => pos == null ? null : f((Vector3) pos);

    public Homeomorphism(DrawingSurface source, DrawingSurface target, Func<Vector3, Vector3> f)
    {
        this.source = source;
        this.target = target;
        this.f = f;
    }
    
    public static Homeomorphism operator *(Homeomorphism f, Homeomorphism g)
    {
        return new Homeomorphism(g.source, f.target, x => f.f(g.f(x)));
    }
}
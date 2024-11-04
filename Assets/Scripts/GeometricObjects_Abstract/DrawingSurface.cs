using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class DrawingSurface : ITooltipOnHover
{
    public string Name { get; protected set; }
    public int Genus { get; protected set; }
    public readonly List<IPoint> punctures = new();
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

    /// <summary>
    /// Bring the point into the boundary / significant point if it is close. Return null if too far from the surface.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public abstract IPoint ClampPoint(Vector3? point);
    
    /// <summary>
    /// A visual point should be shown here.
    /// </summary>
    /// <param name="point"></param>
    public abstract void UpdatePoint(IPoint point);
}

[Serializable]
public class ParametricSurface: DrawingSurface
{
    public ParametricSurfaceVisualizer visualizer;
    public Homeomorphism? parametrization; 
    // this has to be assigned after creation as the homeomorphism has this as one of its fields
    public ParametricSurface(string name, int genus, IEnumerable<IPoint> punctures = null) : base(name, genus, false)
    {
        if (punctures != null)
            this.punctures.AddRange(punctures);
    }

    public override IPoint ClampPoint(Vector3? point)
    { 
        // this should only be called on hits of ray with collider, so these shouldn't be too far.
        // Still we probably need to optimize over both variables
        throw new NotImplementedException();
    }

    public override void UpdatePoint(IPoint point)
    {
        if (point is not BasicPoint)
            Debug.LogWarning("Weird Point: " + point.GetType());
        visualizer.MovePointTo(point.Position);
    }

    public ParametricSurface WithAddedGenus(int addedGenus, IEnumerable<IPoint> addedPunctures) =>
        new ParametricSurface($"{Name}#T", Genus + addedGenus, punctures.Concat(addedPunctures));
}
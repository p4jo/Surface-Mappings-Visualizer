using System;
using System.Collections.Generic;
using UnityEngine;

public class AbstractSurface
{
    public readonly int genus;
    public readonly SurfaceMenu surfaceMenu;

    /// <summary>
    /// These represent the drawn representations of this abstract surface
    /// </summary>
    public readonly Dictionary<string, DrawingSurface> drawingSurfaces = new();
    public readonly Dictionary<string, IPoint> currentPoints = new();
    
    /// <summary>
    /// These represent the edges between the drawn representations of the same surface
    /// </summary>
    private readonly Dictionary<(string, string), Homeomorphism> homeomorphisms = new();

    

    public AbstractSurface(int genus, SurfaceMenu surfaceMenu = null) // todo? remove surfaceMenu
    {
        this.genus = genus;
        this.surfaceMenu = surfaceMenu;
    }

    public void UpdatePoint(Vector3? point, DrawingSurface source)
    {
        // todo: overthink this for multiple points, and curve drawing
        var sourcePoint = source.ClampPoint(point);
        if (sourcePoint == null)
        {
            foreach (var (key, drawingSurface) in drawingSurfaces)
                drawingSurface.UpdatePoint(null);
            return;
        }
        foreach (var (key, drawingSurface) in drawingSurfaces)
        {
            drawingSurface.UpdatePoint(sourcePoint.ApplyHomeomorphism( homeomorphisms[(source.Name, key)] ));
            // TODO: Also show grid; transport the grid through the homeomorphisms as well
        }
    }
    
    public void AddDrawingSurface(DrawingSurface drawingSurface)
    {
        if (drawingSurfaces.TryAdd(drawingSurface.Name, drawingSurface))
            drawingSurface.MouseHover += point => UpdatePoint(point, drawingSurface);
    }
    
    public void AddHomeomorphism(Homeomorphism homeomorphism)
    {
        AddDrawingSurface(homeomorphism.source);
        AddDrawingSurface(homeomorphism.target);
        homeomorphisms.Add((homeomorphism.source.Name, homeomorphism.target.Name), homeomorphism);
    }
    
    public static AbstractSurface FromHomeomorphism(Homeomorphism homeomorphism)
    {
        var res = new AbstractSurface(homeomorphism.source.Genus);
        res.AddHomeomorphism(homeomorphism);
        return res;
    }
}

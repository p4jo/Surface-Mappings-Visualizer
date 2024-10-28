using System;
using System.Collections.Generic;
using UnityEngine;

public class Surface
{
    public readonly int genus;
    public readonly SurfaceMenu surfaceMenu;

    /// <summary>
    /// These represent the drawn representations of this abstract surface
    /// </summary>
    private readonly Dictionary<string, DrawingSurface> drawingSurfaces;
    
    /// <summary>
    /// These represent the edges between the drawn representations of the same surface
    /// </summary>
    private readonly Dictionary<(string, string), Homeomorphism> homeomorphisms;

    

    public Surface(int genus, SurfaceMenu surfaceMenu)
    {
        this.genus = genus;
        this.surfaceMenu = surfaceMenu;
    }

    public void UpdatePoint(Vector3? point, DrawingSurface source)
    {
        
    }
    
    public void AddDrawingSurface(DrawingSurface drawingSurface)
    {
        drawingSurfaces.Add(drawingSurface.name, drawingSurface);
        drawingSurface.MouseHover += point => UpdatePoint(point, drawingSurface);
        
    }
}



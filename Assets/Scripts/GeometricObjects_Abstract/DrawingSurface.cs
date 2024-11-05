using System.Collections.Generic;
using UnityEngine;

public abstract class DrawingSurface 
{
    public string Name { get; protected set; }
    public int Genus { get; protected set; }
    public readonly List<Point> punctures = new();
    public readonly bool is2D;

    protected DrawingSurface(string name, int genus, bool is2D)
    {
        this.Name = name;
        this.Genus = genus;
        this.is2D = is2D;
    }


    /// <summary>
    /// Bring the point into the boundary / significant point if it is close. Return null if too far from the surface.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public abstract Point ClampPoint(Vector3? point);
}
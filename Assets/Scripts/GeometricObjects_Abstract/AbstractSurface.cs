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

    public readonly Dictionary<string, Point> currentPoints = new();

    /// <summary>
    /// These represent the edges between the drawn representations of the same surface
    /// </summary>
    private readonly Dictionary<(string, string), Homeomorphism> homeomorphisms = new();

    public AbstractSurface(int genus, SurfaceMenu surfaceMenu = null) // todo? remove surfaceMenu
    {
        this.genus = genus;
        this.surfaceMenu = surfaceMenu;
    }

    public void AddDrawingSurface(DrawingSurface drawingSurface)
    {
        drawingSurfaces.TryAdd(drawingSurface.Name, drawingSurface);
        homeomorphisms.TryAdd((drawingSurface.Name, drawingSurface.Name), Homeomorphism.Identity(drawingSurface));
    }

    public void AddHomeomorphism(Homeomorphism homeomorphism)
    {
        AddDrawingSurface(homeomorphism.source);
        AddDrawingSurface(homeomorphism.target);
        homeomorphisms[(homeomorphism.source.Name, homeomorphism.target.Name)] = homeomorphism;
        homeomorphisms[(homeomorphism.target.Name, homeomorphism.source.Name)] = homeomorphism.Inverse();
    }
    
    public Homeomorphism GetHomeomorphism(string source, string target)
    {
        if (homeomorphisms.ContainsKey((source, target))) 
            return homeomorphisms[(source, target)];
        // todo: generalize, i.e. find shortest path in the graph of homeomorphisms
        foreach (var (s, t) in homeomorphisms.Keys)
        {
            if (s != source) continue;
            foreach (var (s2, t2) in homeomorphisms.Keys)   
            {
                if (s2 != t) continue;
                if (t2 != target) continue;
                homeomorphisms[(source, target)] = homeomorphisms[(t, target)] * homeomorphisms[(source, t)];
                return homeomorphisms[(source, target)];
            }
        }
        throw new Exception($"No homeomorphism given from {source} to {target}, not even with one step in between");
    }

    public static AbstractSurface FromHomeomorphism(Homeomorphism homeomorphism)
    {
        var res = new AbstractSurface(homeomorphism.source.Genus);
        res.AddHomeomorphism(homeomorphism);
        return res;
    }
}

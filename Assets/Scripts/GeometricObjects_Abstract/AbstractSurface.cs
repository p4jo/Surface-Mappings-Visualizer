using System;
using System.Collections.Generic;

public class AbstractSurface
{
    
    /// <summary>
    /// These represent the drawn representations of this abstract surface
    /// </summary>
    public readonly Dictionary<string, Surface> drawingSurfaces = new();

    /// <summary>
    /// These represent the edges between the drawn representations of the same surface
    /// </summary>
    private readonly Dictionary<(string, string), Homeomorphism> homeomorphisms = new();
    
    public readonly Dictionary<string, string> windowAssignment = new();


    public AbstractSurface()
    { }
    public AbstractSurface(Surface surface)
    {
        AddDrawingSurface(surface);
    }
    public AbstractSurface (Homeomorphism homeomorphism)
    {
        AddHomeomorphism(homeomorphism);
    }

    public void AddDrawingSurface(Surface surface)
    {
        drawingSurfaces.TryAdd(surface.Name, surface);
        homeomorphisms.TryAdd((surface.Name, surface.Name), Homeomorphism.Identity(surface));
        windowAssignment.TryAdd(surface.Name, surface.Name);
    }

    public void AddHomeomorphism(Homeomorphism homeomorphism, bool drawTargetInSameWindowAsSource = false)
    {
        AddDrawingSurface(homeomorphism.source);
        AddDrawingSurface(homeomorphism.target);
        homeomorphisms[(homeomorphism.source.Name, homeomorphism.target.Name)] = homeomorphism;
        homeomorphisms[(homeomorphism.target.Name, homeomorphism.source.Name)] = homeomorphism.Inverse;
        if (drawTargetInSameWindowAsSource)
            windowAssignment[homeomorphism.target.Name] = homeomorphism.source.Name;
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

}

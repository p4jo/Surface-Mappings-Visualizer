using System;
using UnityEngine;

public readonly struct Homeomorphism
{
    public readonly DrawingSurface source, target; // relatively unnecessary
    public readonly Func<Vector3, Vector3> f;
    public Vector3? F(Vector3? pos) => pos == null ? null : f((Vector3)pos);
    

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
using System;
using UnityEngine;

public class Homeomorphism
{
    public readonly DrawingSurface source; 
    public DrawingSurface target;
    public readonly Func<Vector3, Vector3> f, fInv;
    public Vector3? F(Vector3? pos) => pos == null ? null : f((Vector3)pos);
    
    

    public Homeomorphism(DrawingSurface source, DrawingSurface target, Func<Vector3, Vector3> f, Func<Vector3, Vector3> fInv)
    {
        this.source = source;
        this.target = target;
        this.f = f;
        this.fInv = fInv;
    }

    public static Homeomorphism operator *(Homeomorphism f, Homeomorphism g) => 
        new(g.source, f.target, x => f.f(g.f(x)), x => g.fInv(f.fInv(x)));
    
    public static Homeomorphism Identity(DrawingSurface drawingSurface) => 
        new(drawingSurface, drawingSurface, x => x, x => x);

    public Homeomorphism Inverse() => 
        new(target, source, fInv, f);
}
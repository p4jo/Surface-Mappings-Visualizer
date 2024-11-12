using System;
using UnityEngine;

public class Homeomorphism
{
    public readonly Surface source; 
    public Surface target;
    
    public readonly Func<Vector3, Vector3> f, fInv;
    public readonly Func<Vector3, Matrix3x3> df, dfInv;
    public readonly bool isIdentity;
    
    public Homeomorphism(Surface source,
        Surface target,
        Func<Vector3, Vector3> f,
        Func<Vector3, Vector3> fInv,
        Func<Vector3, Matrix3x3> df,
        Func<Vector3, Matrix3x3> dfInv,
        bool isIdentity = false)
    {
        this.source = source;
        this.target = target;
        this.f = f;
        this.fInv = fInv;
        this.df = df;
        this.isIdentity = isIdentity;

        dfInv ??= x => df(fInv(x)).Inverse();
        this.dfInv = dfInv;
    }

    public Vector3? F(Vector3? pos) => pos == null ? null : f((Vector3)pos);
    public static Homeomorphism operator *(Homeomorphism f, Homeomorphism g) => 
        new(g.source, f.target, 
            x => f.f(g.f(x)),
            z => g.fInv(f.fInv(z)),
            x => f.df(g.f(x)) * g.df(x),
            z=> g.dfInv(f.fInv(z)) * f.dfInv(z)
        );

    public static Homeomorphism Identity(Surface surface) =>
        new(surface, surface,
            x => x,
            x => x,
            x => Matrix3x3.Identity,
            y => Matrix3x3.Identity,
            isIdentity: true
        );

    private Homeomorphism inverse;

    public Homeomorphism Inverse => inverse ??= new(target, source, fInv, f, dfInv, df);
}
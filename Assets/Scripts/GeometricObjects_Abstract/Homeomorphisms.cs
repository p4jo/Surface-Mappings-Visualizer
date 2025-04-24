using System;
using UnityEngine;

public class Homeomorphism
{
    public readonly Surface source;

    /// <summary>
    /// Remark for why non-readonly: The target might be assigned after the constructor has ended, since the homeomorphism is used as a variable in the constructor of the target.
    /// </summary>
    public Surface target;

    /// <summary>
    /// Why non-readonly? It makes sense to give own names for secondary homeos such as ....Inverse, ... * ...
    /// </summary>
    public string name;
    
    public readonly Func<Vector3, Vector3> f, fInv;
    public readonly Func<Vector3, Matrix3x3> df, dfInv;
    public readonly bool isIdentity;
    
    public Homeomorphism(Surface source,
        Surface target,
        Func<Vector3, Vector3> f,
        Func<Vector3, Vector3> fInv,
        Func<Vector3, Matrix3x3> df,
        Func<Vector3, Matrix3x3> dfInv,
        string name, bool isIdentity = false)
    {
        this.source = source;
        this.target = target;
        this.f = f;
        this.fInv = fInv;
        this.df = df;
        this.isIdentity = isIdentity;

        dfInv ??= y => df(fInv(y)).Inverse();
        this.dfInv = dfInv;
        this.name = name;
    }

    public Vector3? F(Vector3? pos) => pos == null ? null : f((Vector3)pos);
    public static Homeomorphism operator *(Homeomorphism f, Homeomorphism g) => 
        new(g.source, f.target, 
            x => f.f(g.f(x)),
            z => g.fInv(f.fInv(z)),
            x => f.df(g.f(x)) * g.df(x),
            z=> g.dfInv(f.fInv(z)) * f.dfInv(z),
            f.name + " * " + g.name
        );

    public static Homeomorphism Identity(Surface surface) =>
        new(surface, surface,
            x => x,
            x => x,
            x => Matrix3x3.Identity,
            y => Matrix3x3.Identity,
            isIdentity: true,
            name: "id_" + surface.Name
        );

    private Homeomorphism inverse;

    public Homeomorphism Inverse => inverse ??= new(target, source, fInv, f, dfInv, df, $"({name})^-1");

    public static Homeomorphism ContinueAutomorphismOnSubsurface(Homeomorphism automorphismOnSubsurface, Surface surface)
    {
        return new(surface, surface, forward, backward, forwardDerivative, backwardDerivative, automorphismOnSubsurface.name + " on " + surface.Name);

        Vector3 forward(Vector3 x)
        {
            var y = automorphismOnSubsurface.source.ClampPoint(x, 0.01f);
            if (y == null) return x;
            return automorphismOnSubsurface.f(y);
        }

        Vector3 backward(Vector3 x)
        {
            var y = automorphismOnSubsurface.source.ClampPoint(x, 0.01f);
            if (y == null) return x;
            return automorphismOnSubsurface.fInv(y);
        }

        Matrix3x3 forwardDerivative(Vector3 x)
        {
            var y = automorphismOnSubsurface.source.ClampPoint(x, 0.01f);
            if (y == null) return Matrix3x3.Identity;
            return automorphismOnSubsurface.df(y);
        }

        Matrix3x3 backwardDerivative(Vector3 x)
        {
            var y = automorphismOnSubsurface.source.ClampPoint(x, 0.01f);
            if (y == null) return Matrix3x3.Identity;
            return automorphismOnSubsurface.dfInv(y);
        }
    }
}
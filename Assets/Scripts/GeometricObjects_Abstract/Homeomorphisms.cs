using System;
using UnityEngine;

public struct Matrix3x3
{
    public readonly Vector3 a, b, c;

    public Matrix3x3(float α, float β, float γ = 1f):
        this(new Vector3(α, 0, 0), new Vector3(0, β, 0), new Vector3(0, 0, γ)){}
    public Matrix3x3(Vector2 a, Vector2 b):
        this(a, b, Vector3.forward){}
    public Matrix3x3(Vector3 a, Vector3 b, Vector3 c)
    {
        this.a = a;
        this.b = b;
        this.c = c;
    }

    public static Vector3 operator *(Matrix3x3 A, Vector3 v)
        => A.a * v.x + A.b * v.y + A.c * v.z;

    public static Matrix3x3 operator *(Matrix3x3 A, Matrix3x3 B) => 
        new(A * B.a, A * B.b, A * B.c);

    public static Matrix3x3 Identity = new(Vector3.right, Vector3.up, Vector3.forward);

    // public static Matrix3x3 FromTangentSpace() 
    public Matrix3x3 Inverse()
    {
        float det = Vector3.Dot(Vector3.Cross(a, b), c);

        var invDet = 1.0f / det;
        var adjA = Vector3.Cross(b, c) * invDet;
        var adjB = Vector3.Cross(c, a) * invDet;
        var adjC = Vector3.Cross(a, b) * invDet;
        return new Matrix3x3(adjA, adjB, adjC).Transpose();
    }

    public Matrix3x3 Transpose() =>
        new(
            new Vector3(a.x, b.x, c.x),
            new Vector3(a.y, b.y, c.y),
            new Vector3(a.z, b.z, c.z)
        );
}

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

    public Homeomorphism Inverse() => 
        new(target, source, fInv, f, dfInv, df);
}
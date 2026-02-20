using UnityEngine;

public class Matrix3x3
{
    public readonly Vector3 a, b, c;

    public Matrix3x3(float α, float β, float γ = 1f):
        this(new Vector3(α, 0, 0), new Vector3(0, β, 0), new Vector3(0, 0, γ)){}
    
    public Matrix3x3(float a, float b, float c, float d): this(new Vector2(a, c), new Vector2(b, d)){}
    
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
    
    public static Vector3 operator *(Matrix3x3 A, Vector2 v)
        => A.a * v.x + A.b * v.y;

    public static Matrix3x3 operator *(float scalar, Matrix3x3 A) => 
        new(scalar * A.a, scalar * A.b, scalar * A.c);
    
    public static Matrix3x3 operator *(Matrix3x3 A, Matrix3x3 B) => 
        new(A * B.a, A * B.b, A * B.c);

    public static readonly Matrix3x3 Identity = new(Vector3.right, Vector3.up, Vector3.forward);
    public static readonly Matrix3x3 InvertZ = new(Vector3.right, Vector3.up, Vector3.back);
    
    public Matrix3x3 Inverse()
    {
        float det = Determinant();

        var invDet = 1.0f / det;
        var adjA = Vector3.Cross(b, c) * invDet;
        var adjB = Vector3.Cross(c, a) * invDet;
        var adjC = Vector3.Cross(a, b) * invDet;
        return new Matrix3x3(adjA, adjB, adjC).Transpose();
    }

    public float Determinant() => Vector3.Dot(Vector3.Cross(a, b), c);

    public Matrix3x3 Transpose() =>
        new(
            new Vector3(a.x, b.x, c.x),
            new Vector3(a.y, b.y, c.y),
            new Vector3(a.z, b.z, c.z)
        );
}
using UnityEngine;

public class Matrix3x3
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

public class TangentSpace : ITransformable<TangentSpace>
{
    public readonly Point point;
    public readonly Matrix3x3 basis;

    public TangentSpace(Point point, Matrix3x3 basis)
    {
        this.point = point;
        this.basis = basis;
    }

    public TangentSpace ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new(
            point.ApplyHomeomorphism(homeomorphism),
            homeomorphism.df(point.Position) * basis
        );

    public void Deconstruct(out Point point, out Matrix3x3 basis)  
    {
        point = this.point;
        basis = this.basis;
    }
}

public class TangentVector: ITransformable<TangentVector>
{
    public readonly Vector3 vector;
    public readonly Point point;

    private TangentVector(Point point, Vector3 vector)
    {
        this.vector = vector;
        this.point = point;
    }

    public TangentVector ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new(
            point.ApplyHomeomorphism(homeomorphism),
            homeomorphism.df(point.Position) * vector
        );
    
    public void Deconstruct(out Point point, out Vector3 vector)
    {
        point = this.point;
        vector = this.vector;
    }
    
    public static TangentVector operator +(TangentVector self, TangentVector other)
    {
        if (self.point != other.point)
            throw new System.Exception("Tangent vectors must be on the same point to be added");
        return new TangentVector(self.point, self.vector + other.vector);
    }
    
    public static TangentVector operator *(float scalar, TangentVector tangentVector) =>
        new(tangentVector.point, scalar * tangentVector.vector);
    
    public static TangentVector operator -(TangentVector self) => -1 * self;
    
    public static TangentVector operator -(TangentVector self, TangentVector other) => self + -other;
}
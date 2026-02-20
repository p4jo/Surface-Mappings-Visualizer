using UnityEngine;

public class TangentVector: ITransformable<TangentVector>
{
    /// <summary>
    /// This is the vector at the primary position of the point.
    /// </summary>
    public readonly Vector3 vector;
    public readonly Point point;

    public TangentVector(Point point, Vector3 vector)
    {
        this.point = point;
        this.vector = vector;
    }
    
    public TangentVector(Point point, Vector3 vector, int primaryPositionIndex)
    {
        this.point = point;
        this.vector = point.PassThrough(primaryPositionIndex, 0, vector);
    }

    public TangentVector Normalized => new (point, vector.normalized);

    public Vector3 VectorAtPositionIndex(int index) => point.PassThrough(0, index, vector);

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

    public Vector2 Coordinates(TangentSpace tangentSpace) => tangentSpace.basis.Inverse() * vector;
    public Vector2 Coordinates(Surface surface) => Coordinates(surface.BasisAt(point));

    public override string ToString() => $"(vector {vector} at {point.Position})";

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
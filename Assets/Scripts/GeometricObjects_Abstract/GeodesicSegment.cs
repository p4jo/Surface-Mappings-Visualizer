using UnityEngine;

// TODO: also implement with starting vector or some combination, optimizing
public abstract class GeodesicSegment: Curve
{
    public override string Name { get; set; }
    public override Point StartPosition { get; }
    public override Point EndPosition { get; }
    public override TangentVector StartVelocity { get; }
    public override Surface Surface { get; }

    public abstract override TangentVector DerivativeAt(float t);
    
    public override TangentVector EndVelocity { get; }
    public override float Length { get; }

    protected GeodesicSegment(Point startPosition, Point endPosition, Vector3 startVelocity, Vector3 endVelocity, float length, Surface surface, string name)
    {
        EndPosition = endPosition;
        StartPosition = startPosition;
        StartVelocity = new TangentVector(startPosition, startVelocity);
        EndVelocity = new TangentVector(endPosition, endVelocity);
        Length = length;
        Surface = surface;
        Name = name;
    }
}

public class FlatGeodesicSegment : GeodesicSegment
{
    public FlatGeodesicSegment(Vector3 start, Vector3 end, Surface surface, string name)
        : base(start,end, end - start, end - start, 1, surface, name)
    {  }

    public override Point ValueAt(float t) => StartPosition.Position + StartVelocity.vector * t;
    public override TangentVector DerivativeAt(float t) => new(ValueAt(t), StartVelocity.vector);
}

public class HyperbolicGeodesicSegment : GeodesicSegment
{
    public HyperbolicGeodesicSegment(Vector3 start, Vector3 end, Surface surface, string name)
        : base(start,  end, end - start, end - start, 1, surface, name)
    {  throw new System.NotImplementedException(); }

    public override Point ValueAt(float t) => throw new System.NotImplementedException();
    public override TangentVector DerivativeAt(float t) => throw new System.NotImplementedException();
}

public class SphericalGeodesicSegment : GeodesicSegment
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end, Surface surface, string name)
        : base( start,  end, end - start, end - start, 1, surface, name)
    {  throw new System.NotImplementedException(); }

    public override Point ValueAt(float t) => throw new System.NotImplementedException();

    public override TangentVector DerivativeAt(float t) => throw new System.NotImplementedException();
}
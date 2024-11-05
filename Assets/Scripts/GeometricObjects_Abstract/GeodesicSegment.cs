using UnityEngine;

// TODO: also implement with starting vector or some combination, optimizing
public abstract class GeodesicSegment: Curve
{
    public override Point StartPosition { get; }
    public override Point EndPosition { get; }
    public override Vector3 StartVelocity { get; }
    public override DrawingSurface Surface { get; }
    public abstract override Vector3 ValueAt(float t);
    public abstract override Vector3 DerivativeAt(float t);
    public override Curve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        throw new System.NotImplementedException();
    }

    public override Vector3 EndVelocity { get; }
    public override float Length { get; }

    protected GeodesicSegment(Point startPosition, Point endPosition, Vector3 startVelocity, Vector3 endVelocity,
        float length, DrawingSurface surface)
    {
        EndPosition = endPosition;
        StartPosition = startPosition;
        StartVelocity = startVelocity;
        Length = length;
        Surface = surface;
        EndVelocity = endVelocity;
    }
}

public class FlatGeodesicSegment : GeodesicSegment
{
    public FlatGeodesicSegment(Vector3 start, Vector3 end, DrawingSurface surface)
        : base((BasicPoint) start,(BasicPoint)  end, end - start, end - start, 1, surface)
    {  }

    public override Vector3 ValueAt(float t) => StartPosition.Position + StartVelocity * t;
    public override Vector3 DerivativeAt(float t) => StartVelocity;
}

public class HyperbolicGeodesicSegment : GeodesicSegment
{
    public HyperbolicGeodesicSegment(Vector3 start, Vector3 end, DrawingSurface surface)
        : base((BasicPoint) start, (BasicPoint)  end, end - start, end - start, 1, surface)
    {  throw new System.NotImplementedException(); }

    public override Vector3 ValueAt(float t) => throw new System.NotImplementedException();
    public override Vector3 DerivativeAt(float t) => throw new System.NotImplementedException();
}

public class SphericalGeodesicSegment : GeodesicSegment
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end, DrawingSurface surface)
        : base((BasicPoint) start, (BasicPoint)  end, end - start, end - start, 1, surface)
    {  throw new System.NotImplementedException(); }
    
    public override Vector3 ValueAt(float t) => throw new System.NotImplementedException();
    public override Vector3 DerivativeAt(float t) => throw new System.NotImplementedException();
}
using UnityEngine;

// TODO: also implement with starting vector or some combination, optimizing
public abstract class GeodesicSegment: ICurve
{
    public IPoint StartPosition { get; protected set; }
    public IPoint EndPosition { get; protected set; }
    public Vector3 StartVelocity { get; protected set; }
    public DrawingSurface Surface { get; protected set; }
    public abstract Vector3 ValueAt(float t);
    public abstract Vector3 DerivativeAt(float t);
    public ICurve ApplyHomeomorphism(Homeomorphism homeomorphism)
    {
        throw new System.NotImplementedException();
    }

    public Vector3 EndVelocity { get; protected set; }
    public float Length { get; protected set; }

    protected GeodesicSegment(IPoint startPosition, IPoint endPosition, Vector3 startVelocity, Vector3 endVelocity,
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
        : base((BasicPoint) start,(BasicPoint)  end, start - end, start - end, 1, surface)
    {  }

    public override Vector3 ValueAt(float t) => StartPosition.Position + StartVelocity * t;
    public override Vector3 DerivativeAt(float t) => StartVelocity;
}

public class HyperbolicGeodesicSegment : GeodesicSegment
{
    public HyperbolicGeodesicSegment(Vector3 start, Vector3 end, DrawingSurface surface)
        : base((BasicPoint) start, (BasicPoint)  end, start - end, start - end, 1, surface)
    {  throw new System.NotImplementedException(); }

    public override Vector3 ValueAt(float t) => throw new System.NotImplementedException();
    public override Vector3 DerivativeAt(float t) => throw new System.NotImplementedException();
}

public class SphericalGeodesicSegment : GeodesicSegment
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end, DrawingSurface surface)
        : base((BasicPoint) start, (BasicPoint)  end, start - end, start - end, 1, surface)
    {  throw new System.NotImplementedException(); }
    
    public override Vector3 ValueAt(float t) => throw new System.NotImplementedException();
    public override Vector3 DerivativeAt(float t) => throw new System.NotImplementedException();
}
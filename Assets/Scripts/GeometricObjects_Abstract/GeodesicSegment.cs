using UnityEngine;

// TODO: also implement with starting vector or some combination, optimizing
public abstract class GeodesicSegment: ICurve
{
    public Vector3 StartPosition { get; protected set; }
    public Vector3 EndPosition { get; protected set; }
    public Vector3 StartVelocity { get; protected set; }
    public Vector3 EndVelocity { get; protected set; }
    public float Length { get; protected set; }

    public abstract Vector3 this[float value] { get; }


    protected GeodesicSegment(Vector3 startPosition, Vector3 endPosition, Vector3 startVelocity, Vector3 endVelocity,
        float length)
    {
        EndPosition = endPosition;
        StartPosition = startPosition;
        StartVelocity = startVelocity;
        Length = length;
        EndVelocity = endVelocity;
    }
}

public class FlatGeodesicSegment : GeodesicSegment
{
    public FlatGeodesicSegment(Vector3 start, Vector3 end)
        : base(start, end, start - end, start - end, 1)
    {  }

    public override Vector3 this[float value] => StartPosition + StartVelocity * value;
}

public class HyperbolicGeodesicSegment : GeodesicSegment
{
    public HyperbolicGeodesicSegment(Vector3 start, Vector3 end)
        : base(start, end, start - end, start - end, 1)
    {  throw new System.NotImplementedException(); }

    public override Vector3 this[float value] => throw new System.NotImplementedException();
}

public class SphericalGeodesicSegment : GeodesicSegment
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end)
        : base(start, end, start - end, start - end, 1)
    {  throw new System.NotImplementedException(); }
    
    public override Vector3 this[float value] => throw new System.NotImplementedException();

}
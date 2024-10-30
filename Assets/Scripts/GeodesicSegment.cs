
using System.Collections.Generic;
using kmty.NURBS;
using UnityEngine;

// TODO: also implement with starting vector or some combination, optimizing
public abstract class GeodesicSegment: CurveSegment
{
    public readonly Vector3 start, end;

    protected GeodesicSegment(Vector3 end, Vector3 start)
    {
        this.end = end;
        this.start = start;
    }
}

public class FlatGeodesicSegment: GeodesicSegment
{
    public FlatGeodesicSegment(Vector3 start, Vector3 end)
        : base(start, end)
    {    }

    protected override IEnumerable<CP> GetSplinePoints(float resolution) =>
        new CP[] { new(start, 1), new(end, 1) };
}

public class HyperbolicGeodesicSegment : GeodesicSegment
{
    public HyperbolicGeodesicSegment(Vector3 start, Vector3 end)
        : base(start, end)
    {    }

    // todo: implement
    protected override IEnumerable<CP> GetSplinePoints(float resolution) =>
        new CP[] { new(start, 1), new(end, 1) };
}

public class SphericalGeodesicSegment : GeodesicSegment
{
    public SphericalGeodesicSegment(Vector3 start, Vector3 end)
        : base(start, end)
    {    }

    protected override IEnumerable<CP> GetSplinePoints(float resolution)
    {
        throw new System.NotImplementedException();
    }
}
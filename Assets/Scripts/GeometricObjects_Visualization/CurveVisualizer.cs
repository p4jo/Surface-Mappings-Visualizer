using System.Linq;
using Dreamteck.Splines;
using UnityEngine;

public class CurveVisualizer : MonoBehaviour
{
    public SplineComputer splineComputer;

    public void Initialize(Curve curve, float resolution, float scale = 1, Vector3 offset = new Vector3())
    {
        var points = 
            from t in (
                from i in Enumerable.Range(0, Mathf.RoundToInt(curve.Length / resolution))
                select i * resolution
            ).Append(curve.Length)
            let tangentSpace = curve.BasisAt(t)
            let basis = tangentSpace.basis
            let position = tangentSpace.point.Position * scale + offset
            let tangentVector = basis.a * scale * resolution / 3 // /2 would be the guess for the position, /
            let normalVector = basis.c.normalized * scale
            let positionOutside = position + normalVector * 0.1f
            select new SplinePoint(positionOutside,
                positionOutside - tangentVector,
                normalVector,
                1f, 
                curve.Color);
        splineComputer.SetPoints(points.ToArray(), SplineComputer.Space.Local);
        
        splineComputer.Rebuild();
    }
}

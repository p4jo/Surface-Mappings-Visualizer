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
            let tangentVector = curve.DerivativeAt(t) * scale * resolution / 3 // /2 would be the guess for the position, /
            let normalVector = curve.Surface.BasisAt(curve.ValueAt(t).Position).c.normalized * scale
            let position = curve.ValueAt(t).Position * scale + offset
            let positionOutside = position + normalVector * 0.1f
            select new SplinePoint(positionOutside,
                positionOutside - tangentVector,
                normalVector,
                // inefficient: calculates the derivative twice
                1f, 
                curve.Color);
        splineComputer.SetPoints(points.ToArray(), SplineComputer.Space.Local);
        
        splineComputer.Rebuild();
    }
}

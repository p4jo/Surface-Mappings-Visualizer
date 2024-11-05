using System.Linq;
using Dreamteck.Splines;
using UnityEngine;

public class CurveVisualizer : MonoBehaviour
{
    public SplineComputer splineComputer;

    public void Initialize(Curve curve, float resolution)
    {
        var points = 
            from i in Enumerable.Range(0, Mathf.RoundToInt(curve.Length / resolution))
            let t = i * resolution
            select new SplinePoint(curve.ValueAt(t), curve.DerivativeAt(t));
        splineComputer.SetPoints(points.ToArray(), SplineComputer.Space.Local);
        splineComputer.Rebuild();
    }
}

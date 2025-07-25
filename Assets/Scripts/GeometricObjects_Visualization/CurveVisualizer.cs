using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using UnityEngine;

public class CurveVisualizer : MonoBehaviour
{
    public GameObject splinePrefab;
    public List<SplineComputer> activeSplines = new(), inactiveSplines = new();
    [SerializeField] private float outwardOffset = 0.05f;

    /// <summary>
    /// We assume this curve to not have any (visual) jump points.
    /// </summary>
    /// <param name="curve"></param>
    /// <param name="resolution"></param>
    /// <param name="camera"></param>
    /// <param name="scale"></param>
    /// <param name="offset"></param>
    public void Initialize(Curve curve, float resolution, Camera camera, float scale = 1,
        Vector3 offset = new Vector3())
    {

        var boundaryTimes = curve.VisualJumpTimes.Prepend(0f).Append(curve.Length).ToArray();
        int activeSplinesCount = activeSplines.Count;
        for (int i = boundaryTimes.Length - 1; i < activeSplinesCount; i++)
        {
            var splineComputerTooMuch = activeSplines.Pop();
            splineComputerTooMuch.gameObject.SetActive(false);
            inactiveSplines.Add(splineComputerTooMuch);
        }
        for (int i = 0; i < boundaryTimes.Length - 1; i++)
        {
            float resolutionLocal = resolution;
            float length = boundaryTimes[i+1] - boundaryTimes[i];
            if (length < resolutionLocal) resolutionLocal = length; // continue;
            float ε = 1e-2f * resolutionLocal;
            length -= 2 * ε;
            if (length <= 0)
                continue;
            
            SplineComputer splineComputer;
            if (activeSplines.Count <= i)
            {
                if (inactiveSplines.Count == 0)
                {
                    var splineGameObject = Instantiate(splinePrefab, transform);
                    splineComputer = splineGameObject.GetComponent<SplineComputer>();
                    splineGameObject.GetComponent<ScaleWithCameraSpline>().camera = camera;
                    activeSplines.Add(splineComputer);
                }
                else
                {
                    splineComputer = inactiveSplines.Pop();
                    activeSplines.Add(splineComputer);
                }

                splineComputer.gameObject.SetActive(true);
            }
            else
                splineComputer = activeSplines[i];


            float pts = length / resolutionLocal;
            int pointsCount = Mathf.RoundToInt(pts);
            float newResolution = length / pointsCount;
            float start = boundaryTimes[i] + ε;
            
            var points =
                from index in Enumerable.Range(0, pointsCount + 1)
                let t = start + index * newResolution
                let tangentSpace = curve.BasisAt(t)
                let basis = tangentSpace.basis
                let position = tangentSpace.point.Position * scale + offset
                let tangentVector = basis.a * scale * newResolution / 3 // /2 would be the guess for the position, /
                let normalVector = basis.c.normalized * scale
                let positionOutside = position + normalVector * outwardOffset
                select new SplinePoint(positionOutside,
                    positionOutside - tangentVector,
                    normalVector,
                    1f,
                    curve.Color);
            splineComputer.SetPoints(points.ToArray(), SplineComputer.Space.Local);

            splineComputer.GetComponent<Renderer>().material.color = curve.Color;
            var sizeModifier = splineComputer.GetComponent<MeshGenerator>().sizeModifier;
            foreach (var sizeKey in sizeModifier.keys)
            {
                sizeKey.start = 1d - (1d - sizeKey.start) / curve.Length;
                sizeKey.end = 1d - (1d - sizeKey.end) / curve.Length;
            }
            
            splineComputer.Rebuild();
        }
    }
}

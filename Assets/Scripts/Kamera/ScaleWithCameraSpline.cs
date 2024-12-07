using Dreamteck.Splines;
using UnityEngine;
using UnityEngine.Serialization;

public class ScaleWithCameraSpline : MonoBehaviour
{
    [SerializeField] public new Camera camera;
    [SerializeField] private float baseScale;
    SplineRenderer splineRenderer;

    void Update()
    {
        splineRenderer.size = camera.orthographicSize * baseScale;    
    }
    
    void Start()
    {
        camera ??= GetComponentInParent<Camera>();
        splineRenderer = GetComponent<SplineRenderer>();
        if (baseScale == 0)
            baseScale = splineRenderer.size / camera.orthographicSize;
    }
}

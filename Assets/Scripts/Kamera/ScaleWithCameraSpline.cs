using Dreamteck.Splines;
using UnityEngine;
using UnityEngine.Serialization;

public class ScaleWithCameraSpline : MonoBehaviour
{
    [SerializeField] public new Camera camera;
    [SerializeField] private float baseScale;
    MeshGenerator splineViewer;

    void Update()
    {
        splineViewer.size = camera.orthographicSize * baseScale;    
    }
    
    void Start()
    {
        camera ??= GetComponentInParent<Camera>();
        splineViewer = GetComponent<MeshGenerator>();
        if (baseScale == 0)
            baseScale = splineViewer.size / camera.orthographicSize;
    }
}

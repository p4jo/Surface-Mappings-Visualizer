using Dreamteck.Splines;
using UnityEngine;
using UnityEngine.Serialization;

public class ScaleWithCamera : MonoBehaviour
{
    [SerializeField] private Camera camera;
    [SerializeField] private Vector3 baseScale;

    void Update()
    {
        transform.localScale = camera.orthographicSize * baseScale;    
    }
    
    void Start()
    {
        camera ??= GetComponentInParent<Camera>();
        baseScale = transform.localScale / camera.orthographicSize;
    }
}

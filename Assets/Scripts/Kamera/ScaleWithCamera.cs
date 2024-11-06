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
        baseScale = transform.localScale / camera.orthographicSize;
    }
}

using UnityEngine;

public class ScaleWithCamera : MonoBehaviour
{
    [SerializeField] public new Camera camera;
    [SerializeField] private Vector3 baseScale;

    void Update()
    {
        transform.localScale = camera.orthographicSize * baseScale;    
    }
    
    void Start()
    {
        camera ??= GetComponentInParent<Camera>();
        if (baseScale.sqrMagnitude == 0)
            baseScale = transform.localScale / camera.orthographicSize;
        Update();
    }
}

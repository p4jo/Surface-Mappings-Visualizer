using System;
using UnityEngine;

public class CenterPointerToTransformComponent : MonoBehaviour
{
    public CenterPointerToTransform centerPointer;
}


[Serializable]
public class CenterPointer {
    public virtual Vector3? position => null;
    public event Action<Kamera> OnCenter;
    public void Center() => OnCenter?.Invoke(null);
}

[Serializable]
public class CenterPointerToTransform : CenterPointer {
    public override Vector3? position => transform == null ? null : transform.position;
    public Transform transform;
}

[Serializable]
public class CenterPointerToPosition : CenterPointer {
    public override Vector3? position => center;
    public Vector3 center;
}

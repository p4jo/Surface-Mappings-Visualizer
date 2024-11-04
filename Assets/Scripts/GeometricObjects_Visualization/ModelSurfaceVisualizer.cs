using System;
using System.Collections.Generic;
using UnityEngine;
using MathMesh;
using UnityEngine.Serialization;

public class ModelSurfaceVisualizer : MonoBehaviour
{
    [SerializeField] private ModelSurface surface;
    [SerializeField] private RectTransform pointer;
    
    
    public void MovePointTo(Vector3? point)
    {
        if (!point.HasValue)
        {
            pointer.gameObject.SetActive(false);
            return;
        }
        pointer.localPosition = point.Value;
    }

    public void Initialize(ModelSurface surface)
    {
        this.surface = surface;
        throw  new NotImplementedException();
    }
}
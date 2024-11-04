using System;
using System.Collections.Generic;
using UnityEngine;
using MathMesh;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

public class ParametricSurfaceVisualizer : MonoBehaviour
{
    [SerializeField] private ParametricSurface parametricSurface;
    [SerializeField] private Transform pointer;
    [SerializeField] private List<MeshGenerator> meshGenerator;
    [SerializeField] private GameObject meshPrefab;


    public void MovePointTo(Vector3? point)
    {
        if (!point.HasValue)
        {
            pointer.gameObject.SetActive(false);
            return;
        }
        pointer.position = point.Value;
    }

    public void Initialize(ParametricSurface parametricSurface)
    {
        this.parametricSurface = parametricSurface;
        var gameObject = Instantiate(meshPrefab, transform);
        var generator = gameObject.GetComponent<MeshGenerator>();
        generator.CurrentSurface = new SurfaceData(parametricSurface.Name, 0, Array.Empty<float>(),
            func: floats => parametricSurface.parametrization?.f(new Vector3(floats[0], floats[1])) ?? Vector3.zero);
        generator.u = new(0, tau);
        generator.v = new(0, tau);
        generator.doubleSided = false;
        generator.uSlices = 200;
        generator.vSlices = 200; // todo: increase?
        generator.GenerateMesh();
    }

    private const float tau = Mathf.PI * 2;
}
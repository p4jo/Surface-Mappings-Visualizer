using System;
using System.Collections.Generic;
using UnityEngine;
using MathMesh;
using UnityEngine.Serialization;

public class ParametricSurfaceVisualizer : SurfaceVisualizer
{
    [SerializeField] private ParametricSurface parametricSurface;
    [FormerlySerializedAs("meshGenerator")] [SerializeField] private List<MeshGenerator> meshGenerators;
    [SerializeField] private GameObject meshPrefab;


    public void Initialize(ParametricSurface parametricSurface)
    {
        this.parametricSurface = parametricSurface;
        foreach (var rect in parametricSurface.chartRects)
        {
            var gameObject = Instantiate(meshPrefab, transform);
            var generator = gameObject.GetComponent<MeshGenerator>();
            generator.u = new(rect.xMin, rect.xMax);
            generator.v = new(rect.yMin, rect.yMax);
            generator.CurrentSurface = new SurfaceData(
                parametricSurface.Name, 
                0, 
                new []{generator.u.x, generator.u.y, generator.v.x, generator.v.y},
                func: floats => parametricSurface.embedding.f(new Vector3(floats[0], floats[1]))
            );
            // generator.doubleSided = false;
            generator.uSlices = 300;
            generator.vSlices = 300; 
            generator.GenerateMesh();
            meshGenerators.Add(generator);

            gameObject.GetComponent<MeshCollider>().sharedMesh = generator.mesh;
            gameObject.GetComponent<TooltipTarget>().Initialize(this);
        }
    }

    private const float tau = MathF.PI * 2;
    
    
}
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class PointPushSlider : MonoBehaviour
{
    [SerializeField] private float v0;
    [SerializeField] private float v1;
    [SerializeField] private float v2;
    [SerializeField] private float v3;
    [SerializeField] private float v4;
    [SerializeField] private float v5;
    [SerializeField] private float v6;
    [SerializeField] private float v7;
    private PushingPath pushingPath;
    [SerializeField] private FibredSurfaceMenu fibredSurfaceMenu;
    [SerializeField] private bool update;

    private void Update()
    {
        if (!update) return;
        update = false;
        if (v0 > 0) pushingPath.variables.ElementAtOrDefault(0)?.SetValue(v0); 
        else pushingPath.variables.ElementAtOrDefault(0)?.FreeVariable();
        if (v1 > 0) pushingPath.variables.ElementAtOrDefault(1)?.SetValue(v1); 
        else pushingPath.variables.ElementAtOrDefault(1)?.FreeVariable();
        if (v2 > 0) pushingPath.variables.ElementAtOrDefault(2)?.SetValue(v2); 
        else pushingPath.variables.ElementAtOrDefault(2)?.FreeVariable();
        if (v3 > 0) pushingPath.variables.ElementAtOrDefault(3)?.SetValue(v3); 
        else pushingPath.variables.ElementAtOrDefault(3)?.FreeVariable();
        if (v4 > 0) pushingPath.variables.ElementAtOrDefault(4)?.SetValue(v4); 
        else pushingPath.variables.ElementAtOrDefault(4)?.FreeVariable();
        if (v5 > 0) pushingPath.variables.ElementAtOrDefault(5)?.SetValue(v5);
        else pushingPath.variables.ElementAtOrDefault(5)?.FreeVariable();
        if (v6 > 0) pushingPath.variables.ElementAtOrDefault(6)?.SetValue(v6);
        else pushingPath.variables.ElementAtOrDefault(6)?.FreeVariable();
        if (v7 > 0) pushingPath.variables.ElementAtOrDefault(7)?.SetValue(v7);
        else pushingPath.variables.ElementAtOrDefault(7)?.FreeVariable();
        if (pushingPath.Concrete) pushingPath.CalculateSelfIntersections();
        var newGraphMap = fibredSurfaceMenu.FibredSurface.Strips.ToDictionary(s => (Strip) s, s => pushingPath.Image(s));
        fibredSurfaceMenu.UpdateGraphMap(newGraphMap);
        Debug.Log( "Pushing path: " + pushingPath, this);
        Debug.Log("Variables: " + pushingPath.variables.ToCommaSeparatedString(), this);
        Debug.Log( "Map: " + newGraphMap.ToCommaSeparatedString(kv => $"{kv.Key.ColorfulName} -> {kv.Value.ToColorfulString(150, 10)}"), this);
    }

    public void Initialize(FibredSurfaceMenu fibredSurfaceMenu, PushingPath pushingPath)
    {
        this.fibredSurfaceMenu = fibredSurfaceMenu;
        this.pushingPath = pushingPath;
    }
}

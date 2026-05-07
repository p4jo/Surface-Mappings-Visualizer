using System;
using System.Linq;
using UnityEngine;

public class PointPushSlider : MonoBehaviour
{
    [SerializeField] private float t1;
    [SerializeField] private float t2;
    [SerializeField] private float t3;
    [SerializeField] private float t4;
    [SerializeField] private float t5;
    [SerializeField] private float t6;
    [SerializeField] private float t7;
    [SerializeField] private float t8;
    private PushingPath pushingPath;
    [SerializeField] private FibredSurfaceMenu fibredSurfaceMenu;
    [SerializeField] private string description;
    [SerializeField] private string description2;
    [SerializeField] private bool update;

    private void Update()
    {
        if (!update) return;
        update = false;
        if (t1 > 0) pushingPath.variables.ElementAtOrDefault(0)?.SetValue(t1); 
        else pushingPath.variables.ElementAtOrDefault(0)?.FreeVariable();
        if (t2 > 0) pushingPath.variables.ElementAtOrDefault(1)?.SetValue(t2); 
        else pushingPath.variables.ElementAtOrDefault(1)?.FreeVariable();
        if (t3 > 0) pushingPath.variables.ElementAtOrDefault(2)?.SetValue(t3); 
        else pushingPath.variables.ElementAtOrDefault(2)?.FreeVariable();
        if (t4 > 0) pushingPath.variables.ElementAtOrDefault(3)?.SetValue(t4); 
        else pushingPath.variables.ElementAtOrDefault(3)?.FreeVariable();
        if (t5 > 0) pushingPath.variables.ElementAtOrDefault(4)?.SetValue(t5); 
        else pushingPath.variables.ElementAtOrDefault(4)?.FreeVariable();
        if (t6 > 0) pushingPath.variables.ElementAtOrDefault(5)?.SetValue(t6);
        else pushingPath.variables.ElementAtOrDefault(5)?.FreeVariable();
        if (t7 > 0) pushingPath.variables.ElementAtOrDefault(6)?.SetValue(t7);
        else pushingPath.variables.ElementAtOrDefault(6)?.FreeVariable();
        if (t8 > 0) pushingPath.variables.ElementAtOrDefault(7)?.SetValue(t8);
        else pushingPath.variables.ElementAtOrDefault(7)?.FreeVariable();
        if (pushingPath.Concrete) pushingPath.CalculateSelfIntersections();
        var newGraphMap = fibredSurfaceMenu.FibredSurface.Strips.ToDictionary(s => (Strip) s, s => pushingPath.Image(s));
        fibredSurfaceMenu.UpdateGraphMap(newGraphMap);
        description = $@"Variables: {pushingPath.variables.ToCommaSeparatedString()} \n Map: {newGraphMap.ToCommaSeparatedString(kv => $"{kv.Key.ColorfulName} -> {kv.Value.ToColorfulString(150, 10)}")}";
        description2 = pushingPath.ToString();
    }

    public void Initialize(FibredSurfaceMenu fibredSurfaceMenu, PushingPath pushingPath)
    {
        this.fibredSurfaceMenu = fibredSurfaceMenu;
        this.pushingPath = pushingPath;
    }
}

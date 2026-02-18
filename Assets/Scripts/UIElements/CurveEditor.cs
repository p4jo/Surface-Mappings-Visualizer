using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CurveEditor : MonoBehaviour
{
    [SerializeField] private TMP_Text headingText;
    private Curve curve;
    private Curve originalCurve;
    [SerializeField] private GameObject sectionEditorPrefab;
    private FibredSurface fibredSurface;
    [SerializeField] private TMP_Dropdown curveDropdown;
    public event Action<Curve> CurveUpdated;
    
    private List<Curve> subcurves = new();
    private string colorfulName;
    private UnorientedStrip strip;

    public void Initialize()
    {
        var curveName = curveDropdown.options[curveDropdown.value].text;
        strip = fibredSurface.graph.Edges.FirstOrDefault(e => e.ColorfulName == curveName);
        if (strip != null) 
            Initialize(strip.Curve, strip.ToColorfulString());
    }

    public void Initialize(Curve curve, string colorfulName)
    {
        
        this.curve = curve;
        this.colorfulName = colorfulName;
        originalCurve = curve;
        UpdateSectionEditors();
    }

    private void UpdateSectionEditors()
    {
        float lastTime = 0f;
        Point lastPoint = curve.StartPosition;
        Curve subcurve;
        var sectionEditors = GetComponentsInChildren<CurveSectionEditor>();
        subcurves.Clear();
        int i = 0;
        CurveSectionEditor NewSectionEditor()
        {
            if (i < sectionEditors.Length)
                return sectionEditors[i];
            return Instantiate(sectionEditorPrefab, transform).GetComponent<CurveSectionEditor>();
        }

        foreach (var (time, boundaryPoint) in curve.VisualJumpPoints)
        {
            subcurve = curve.Restrict(lastTime, time);
            subcurves.Add(subcurve);
            NewSectionEditor().Initialize(this, subcurve, lastTime, time, lastPoint, boundaryPoint, i);
            i++;
            lastTime = time;
            lastPoint = boundaryPoint.SwitchSide();
        }
        
        subcurve = curve.Restrict(lastTime, curve.Length);
        subcurves.Add(subcurve);
        NewSectionEditor().Initialize(this, subcurve, lastTime, curve.Length, lastPoint, curve.EndPosition, i);
        i++;
        
        headingText.text = $"Edit {colorfulName}";
        for (int j = i; j < sectionEditors.Length; j++)
        {
            sectionEditors[j].gameObject.SetActive(false);
        }
        gameObject.SetActive(true);
    }

    public void Revert()
    {
        curve = originalCurve;
        UpdateSectionEditors();
        CurveUpdated?.Invoke(curve);
    }

    public void UpdateStrip()
    {
        strip.Curve = curve;   
    }
    
    public void Apply()
    {
        curve = new ConcatenatedCurve(subcurves, curve.Name) { Color = curve.Color};
        UpdateSectionEditors();
        CurveUpdated?.Invoke(curve);
    }

    public void Close()
    {
        gameObject.SetActive(false);   
    }

    public void Smooth()
    {
        curve = new ConcatenatedCurve(subcurves, curve.Name, smoothed: true) { Color = curve.Color };
        UpdateSectionEditors();
        CurveUpdated?.Invoke(curve);
    }
    
    public void StraightenSegment(int index, Point startPoint, Point endPoint)
    {
        if (subcurves[index].Surface is ModelSurface modelSurface)
            subcurves[index] =
                modelSurface.GetBasicGeodesic(startPoint, endPoint, "Straightened Segment");
        else if (subcurves[index].Surface is GeodesicSurface geodesicSurface)
            subcurves[index] =
                geodesicSurface.GetGeodesic(startPoint, endPoint, "Straightened Segment");
        Apply();
    }
    
    public void UpdateDropdown(FibredSurface newFibredSurface)
    {
        fibredSurface = newFibredSurface;
        curveDropdown.options = newFibredSurface.graph.Edges.Select(e => new TMP_Dropdown.OptionData(e.ColorfulName, null, e.Color)).ToList();
        Close();
    }
}

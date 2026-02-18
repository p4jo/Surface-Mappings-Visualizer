using TMPro;
using UnityEngine;

public class CurveSectionEditor : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    private int index;
    private CurveEditor curveEditor;
    private Point endPoint;
    private Point startPoint;

    public void Initialize(CurveEditor curveEditor, Curve subcurve, float startTime, float endTime, Point startPoint, Point endPoint, int index)
    {
        this.curveEditor = curveEditor;
        this.index = index;
        this.startPoint = startPoint;
        this.endPoint = endPoint;
        gameObject.SetActive(true); 
        label.text = $"Segment [{startTime:0.00}, {endTime:0.00}] ({subcurve.GetType()}) from {startPoint} to {endPoint}";
    }

    public void StraightenSegment() => curveEditor.StraightenSegment(index, startPoint, endPoint);
}

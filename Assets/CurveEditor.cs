using TMPro;
using UnityEngine;

public class CurveEditor : MonoBehaviour
{
    [SerializeField] private TMP_Text headingText;
    private Curve curve;
    [SerializeField] private GameObject sectionEditorPrefab;
    
    public void Initialize(Curve curve)
    {
        this.curve = curve;
        headingText.text = $"Edit {((IDrawable)curve).ColorfulName}";
    }
}

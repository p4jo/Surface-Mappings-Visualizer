
using UnityEngine;

public class MainMenu: MonoBehaviour
{
    
    [SerializeField] private GameObject surfaceMenuPrefab;
    private SurfaceMenu surfaceMenu;
    [SerializeField] private RectTransform canvas;
    [SerializeField] private string surfaceParameters;
    private void Start()
    {
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        surfaceMenu = gameObject.GetComponent<SurfaceMenu>();
        surfaceMenu.Initialize(surfaceParameters, canvas); 
        // todo
    }    
}
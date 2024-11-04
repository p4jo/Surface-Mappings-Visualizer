
using UnityEngine;

public class MainMenu: MonoBehaviour
{
    
    [SerializeField] private GameObject surfaceMenuPrefab;
    private SurfaceMenu surfaceMenu;
    [SerializeField] private RectTransform canvas;

    private void Start()
    {
        var gameObject = Instantiate(surfaceMenuPrefab, transform);
        surfaceMenu = gameObject.GetComponent<SurfaceMenu>();
        surfaceMenu.Initialize("Torus2D;Torus3D", canvas); 
        // todo
    }    
}
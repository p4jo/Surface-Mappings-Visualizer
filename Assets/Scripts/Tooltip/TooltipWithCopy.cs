using UnityEngine;

public class TooltipWithCopy : Tooltip {
    [SerializeField] string copyableString;
    void Start() {
        content = new() { text = "Click to copy!" };
    }
    public override void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) {
        GUIUtility.systemCopyBuffer = copyableString;
        // TODO: fix this for mobile or WebGL
    }
}
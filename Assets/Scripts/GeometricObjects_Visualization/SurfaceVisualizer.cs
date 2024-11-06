
    using System;
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using UnityEngine;

    public abstract class SurfaceVisualizer: MonoBehaviour, ITooltipOnHover
    {
        [SerializeField] private GameObject curveVisualizerPrefab;
        [SerializeField] private Dictionary<string,CurveVisualizer> curves = new();
        
        public abstract void MovePointTo([CanBeNull] Point point);
        
        public virtual void AddCurve(Curve curve)
        {
            var gameObject = Instantiate(curveVisualizerPrefab, transform);
            var curveVisualizer = gameObject.GetComponent<CurveVisualizer>();
            curveVisualizer.Initialize(curve, 0.05f);
            curves[curve.Name] = curveVisualizer;
        }

        public void RemoveCurve(string name)
        {
            if (!curves.ContainsKey(name)) return;
            Destroy(curves[name].gameObject);
            curves.Remove(name);
        }
        
        
        #region ITooltipOnHover
        public event Action<Vector3?> MouseHover;
    
        public virtual TooltipContent GetTooltip() => new(); // don't actually show a tooltip
        public virtual void OnHover(Kamera activeKamera, Vector3 position) => MouseHover?.Invoke(position);
        public virtual void OnHoverEnd() => MouseHover?.Invoke(null);
        public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => MouseHover?.Invoke(position);
        public virtual float hoverTime => 0;
        #endregion
    }
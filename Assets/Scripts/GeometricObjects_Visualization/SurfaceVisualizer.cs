
    using System;
    using JetBrains.Annotations;
    using UnityEngine;

    public abstract class SurfaceVisualizer: MonoBehaviour, ITooltipOnHover
    {
        
        public abstract void MovePointTo([CanBeNull] Point point);
        
        #region ITooltipOnHover
        public event Action<Vector3?> MouseHover;
    
        public virtual TooltipContent GetTooltip() => new(); // don't actually show a tooltip
        public virtual void OnHover(Kamera activeKamera, Vector3 position) => MouseHover?.Invoke(position);
        public virtual void OnHoverEnd() => MouseHover?.Invoke(null);
        public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) => MouseHover?.Invoke(position);
        public virtual float hoverTime => 0;
        #endregion
    }
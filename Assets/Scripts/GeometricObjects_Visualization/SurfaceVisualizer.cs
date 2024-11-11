
    using System;
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using UnityEngine;

    public abstract class SurfaceVisualizer: MonoBehaviour, ITooltipOnHover
    {
        [SerializeField] private GameObject curveVisualizerPrefab;
        [SerializeField] protected Dictionary<string, CurveVisualizer> curveVisualizers = new();
        
        public readonly int id;
        private static int lastID;

        protected SurfaceVisualizer()
        {
            id = lastID++;
        }

        
        public abstract void MovePointTo([CanBeNull] Point point);
        
        public virtual CurveVisualizer AddCurveVisualizer(string name)
        {
            if (curveVisualizers.TryGetValue(name, out var curveVisualizer)) return curveVisualizer;
            var gameObject = Instantiate(curveVisualizerPrefab, transform);
            curveVisualizer = gameObject.GetComponent<CurveVisualizer>();
            curveVisualizers[name] = curveVisualizer;
            gameObject.GetComponent<ScaleWithCameraSpline>().camera = GetComponentInChildren<Camera>();
            return curveVisualizer;
        }

        public virtual void AddCurve(Curve curve)
        {
            // if a curve of this name is already shown, re-initialize it
            AddCurveVisualizer(curve.Name).Initialize(curve, 0.2f);
        }

        public void RemoveCurve(string name)
        {
            if (!curveVisualizers.ContainsKey(name)) return;
            Destroy(curveVisualizers[name].gameObject);
            curveVisualizers.Remove(name);
        }
        
        
        #region ITooltipOnHover
        public event Action<Vector3?, int> MouseEvent;
    
        public virtual TooltipContent GetTooltip() => new(); // don't actually show a tooltip
        public virtual void OnHover(Kamera activeKamera, Vector3 position) => MouseEvent?.Invoke(position, -1);
        public virtual void OnHoverEnd() => MouseEvent?.Invoke(null, -1);

        public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) =>
            MouseEvent?.Invoke(position, mouseButton);
        public virtual float hoverTime => 0;
        #endregion

        /// <summary>
        /// Display a point, curve, grid etc.
        /// If preview is on, this (re)moves the previous preview object.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="preview"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Display(ITransformable input, bool preview = false)
        {
            if (input == null)
            {
                if (preview)
                    MovePointTo(null); 
                return;
            }
            switch (input)
            {
                case Point point when preview:
                    MovePointTo(point);
                    break;
                case Point point when !preview:
                    AddPoint(point);
                    break;
                case Curve curve:
                    AddCurve(curve);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        protected abstract void AddPoint(Point point);
    }
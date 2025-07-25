
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public abstract class SurfaceVisualizer: MonoBehaviour, ITooltipOnHover
    {
        [SerializeField] private GameObject curveVisualizerPrefab;
        [SerializeField] protected Dictionary<string, CurveVisualizer> curveVisualizers = new();
        
        List<GameObject> inactivePointers = new();
        Dictionary<Vector3, GameObject> activePointers = new();
        List<GameObject> activePreviewPointers = new();
        public readonly int id;
        private static int lastID;
        private IDrawable lastPreviewObject;
        [SerializeField] protected GameObject pointerPrefab;
        [SerializeField] protected float scale = 1f;
        [SerializeField] protected Vector3 imageOffset;
        [SerializeField] public new Camera camera;
        [SerializeField] protected float resolution = 0.2f;

        protected SurfaceVisualizer()
        {
            id = lastID++;
        }

        public virtual void Initialize(Camera camera = null)
        {
            if (camera != null)
                this.camera = camera;
        }


        public virtual CurveVisualizer AddCurveVisualizer(string name)
        {
            if (curveVisualizers.TryGetValue(name, out var curveVisualizer) && curveVisualizer != null) return curveVisualizer;
            var gameObject = Instantiate(curveVisualizerPrefab, transform);
            gameObject.name = name;
            gameObject.transform.localPosition = Vector3.zero;
            curveVisualizer = gameObject.GetComponent<CurveVisualizer>();
            curveVisualizers[name] = curveVisualizer;
            return curveVisualizer;
        }

        
        #region ITooltipOnHover
        public event Action<Vector3?, int> MouseEvent;
    
        public virtual TooltipContent GetTooltip() => new(); // don't actually show a tooltip
        public virtual void OnHover(Kamera activeKamera, Vector3 position) => MouseEvent?.Invoke(surfacePosition(position), -1);
        public virtual void OnHoverEnd() => MouseEvent?.Invoke(null, -1);

        public virtual void OnClick(Kamera activeKamera, Vector3 position, int mouseButton) =>
            MouseEvent?.Invoke(surfacePosition(position), mouseButton);
        public virtual float hoverTime => 0;
        #endregion

        protected virtual void AddCurve(Curve curve)
        {
            if (curve is ModelSurfaceSide side) 
                AddCurveVisualizer(curve.Name + "*").Initialize(side.other, resolution, camera, scale, imageOffset);
            AddCurveVisualizer(curve.Name).Initialize(curve, resolution, camera, scale, imageOffset);
        }

        public void RemoveCurve(string name)
        {
            if (!curveVisualizers.ContainsKey(name)) return;
            Destroy(curveVisualizers[name].gameObject);
            curveVisualizers.Remove(name);
        }
        
        /// <summary>
        /// Display a point, curve, grid etc.
        /// If preview is on, this (re)moves the previous preview object.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="preview"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Display(IDrawnsformable input, bool preview = false)
        {
            
            switch (input)
            {
                case null when preview && lastPreviewObject is Point:
                    foreach (var pointer in activePreviewPointers)
                    {
                        pointer.SetActive(false);
                        inactivePointers.Add(pointer);
                    }
                    activePreviewPointers.Clear();
                    break;
                case Point point when preview:
                    PreviewPoint(point);
                    lastPreviewObject = point;
                    break;
                case Point point when !preview:
                    AddPoint(point);
                    break;
                case Curve curve:
                    AddCurve(curve);
                    if (preview) 
                        lastPreviewObject = curve;
                    break;
                case null when preview && lastPreviewObject != null:
                    Remove(lastPreviewObject);
                    break;
                case null:
                    break;
                case IPatchedDrawnsformable patchedTransformable:
                    foreach (var patch in patchedTransformable.Patches) 
                        Display(patch, preview);
                    if (preview)
                        lastPreviewObject = patchedTransformable; 
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void Remove(IDrawable input)
        {
            switch (input)
            {
                case null:
                    break;
                case Point point:
                    RemovePoint(point);
                    break;
                case Curve curve:
                    RemoveCurve(curve.Name);
                    break;
                case IPatchedDrawnsformable patchedTransformable:
                    foreach (var patch in patchedTransformable.Patches) 
                        Remove(patch);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        
        public Vector3 displayPosition(Vector3 surfacePosition) => surfacePosition * scale + imageOffset;
        public Vector3 surfacePosition(Vector3 displayPosition) => (displayPosition - imageOffset) / scale;

        protected virtual void AddPoint(Point point)
        {
            foreach (var position in point.Positions)   
            {
                if (activePointers.ContainsKey(position)) 
                    continue;
                var pointer = inactivePointers.Pop() ?? NewPointer();
                pointer.SetActive(true);
                pointer.transform.localPosition = displayPosition(position);
                pointer.GetComponent<Renderer>().material.color = point.Color;
                activePointers.Add(position, pointer);
            }
        }
        
        protected virtual void RemovePoint(Point point)
        {
            foreach (var position in point.Positions)
            {
                if (!activePointers.TryGetValue(position, out var pointer)) 
                    continue;
                pointer.SetActive(false);
                activePointers.Remove(position);
                inactivePointers.Add(pointer);
            }
        }
        
        protected virtual void PreviewPoint(Point point)
        {
            var pointPositions = point.Positions.ToArray();
            List<GameObject> newActivePointers = new();
                
            foreach (var position in pointPositions)
            {
                var pointer = activePreviewPointers.Pop() ?? inactivePointers.Pop() ?? NewPointer();
                pointer.SetActive(true);
                pointer.transform.localPosition = displayPosition(position);
                pointer.GetComponent<Renderer>().material.color = point.Color;
                newActivePointers.Add(pointer);
            }

            foreach (var notNeededPreviewPointer in activePreviewPointers)
            {
                notNeededPreviewPointer.SetActive(false);
                inactivePointers.Add(notNeededPreviewPointer);
            }
            activePreviewPointers = newActivePointers;
        }

        private GameObject NewPointer()
        {
            var newPointer = Instantiate(pointerPrefab, transform);
            newPointer.GetComponent<ScaleWithCamera>().camera = camera;
            return newPointer;
        }
    }
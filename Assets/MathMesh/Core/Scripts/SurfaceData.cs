using UnityEngine;
using System;

namespace MathMesh
{
    [Serializable]
    public class SurfaceData
    {
        Func<float[], Vector3> function;
        public string name { get; private set; }
        public int parameters { get; private set; }
        public float[] defaults { get; private set; }
        public Constraints constraints { get; private set; }
        public int sealType { get; private set; } //0 == none, 1 == u, 2 == v 3 == u & v
        public int sealReverse { get; private set; }//0 == none, 1 == u, 2 == v, 3 == u&v
        public SurfaceData(string name, int param, float[] def, Constraints cons = null, int seal = 0, int sr = 0, Func<float[], Vector3> func = null)
        {
            if (func != null)
                function = func;
            else
            {
                var info = typeof(MathMeshUtility).GetMethod("Get" + name);
                if (info == null)
                    throw new Exception("No function found for " + name);
                function = args => (Vector3) info.Invoke(null, new object[] { args });
            }

            this.name = name;
            parameters = param;
            defaults = def;
            constraints = cons;
            sealType = seal;
            sealReverse = sr;
        }
        public SurfaceData(string name, int param, float[] def, int seal, int sr, Constraints cons = null) : this(name, param, def, cons, seal, sr)
        { }
        public SurfaceData(string name, int param, float[] def, int seal, Constraints cons = null) : this(name, param, def, cons, seal)
        { }

        public Vector3 GetPoint(params float[] args) => function(args);
    }
}
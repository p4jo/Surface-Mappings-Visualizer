using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;

namespace MathMesh
{
    [Serializable]
    public class SurfaceData
    {
        MethodInfo info;
        public string name { get; private set; }
        public int parameters { get; private set; }
        public float[] defaults { get; private set; }
        public Constraints constraints { get; private set; }
        public int sealType { get; private set; } //0 == none, 1 == u, 2 == v 3 == u & v
        public int sealReverse { get; private set; }//0 == none, 1 == u, 2 == v, 3 == u&v
        public SurfaceData(string name, int param, float[] def, Constraints cons = null, int seal = 0, int sr = 0)
        {
            Type type = typeof(MathMeshUtility);
            info = type.GetMethod("Get" + name);

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

        public Vector3 GetPoint(params float[] args)
        {
            return (Vector3)info.Invoke(null, new object[] { args });
        }
    }
}
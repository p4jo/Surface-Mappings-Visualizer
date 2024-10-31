using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System;

namespace MathMesh
{
    public static partial class MathMeshUtility {
        public static UnityEngine.Vector3 GetBoy_BK(float[] args)
        {
            //http://www.science.smith.edu/~patela/boysurface/AmyJenny/other_boys.html
            //x = , y = , z = 
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            Complex z = new Complex(u*Math.Cos(v), u*Math.Sin(v));
            Complex denom = MathHelper.Pow(z, 6) + Math.Sqrt(5) * MathHelper.Pow(z, 3) - 1;
            /* Solve Coords */
            Vector3 point = new Vector3();
            float g1 = -1.5f * (float)(z * (1 - MathHelper.Pow(z, 4))/denom).Imaginary;
            float g2 = -1.5f * (float)(z * (1 + MathHelper.Pow(z, 4)) / denom).Real;
            float g3 = -0.5f + (float)((1+MathHelper.Pow(z,6))/denom).Imaginary;
            float temp = g1 * g1 + g2 * g2 + g3 * g3;
            point.X = g1/temp;
            point.Y = g2/temp;
            point.Z = g3/temp;
            return new UnityEngine.Vector3(point.X,point.Y,point.Z);
        }
    }
}

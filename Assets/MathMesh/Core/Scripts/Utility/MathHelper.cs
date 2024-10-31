using System;
using System.Numerics;

namespace MathMesh
{
    public static class MathHelper
    {
        public static float Cosh(float val)
        {
            return (float)Math.Cosh(val);
        }
        public static float Sinh(float val)
        {
            return (float)Math.Sinh(val);
        }
        public static float Sech(float val)
        {
            return 1f / Cosh(val);
        }
        public static float Tanh(float val)
        {
            return (float)Math.Tanh(val);
        }
        public static float Csch(float val)
        {
            return 1f / Sinh(val);
        }
        public static float Coth(float val)
        {
            return Cosh(val) / Sinh(val);
        }
        public static Complex Pow(Complex com, int power)
        {
            Complex ret = com;
            for(int i = 1; i < power; i++)
            {
                ret *= com;
            }
            return ret;
        }
    }
}

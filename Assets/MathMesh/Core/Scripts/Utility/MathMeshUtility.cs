using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace MathMesh {
    public static partial class MathMeshUtility
    {
        #region Unpack Funcs
        private static float UnpackArguments(float arg1)
        {
            return arg1;
        }
        private static (float, float) UnpackArguments(float arg1, float arg2)
        {
            return (arg1, arg2);
        }
        private static (float, float, float) UnpackArguments(float arg1, float arg2, float arg3)
        {
            return (arg1, arg2, arg3);
        }
        private static (float, float, float, float) UnpackArguments(float arg1, float arg2, float arg3, float arg4)
        {
            return (arg1, arg2, arg3, arg4);
        }
        private static (float, float, float, float, float) UnpackArguments(float arg1, float arg2, float arg3, float arg4, float arg5)
        {
            return (arg1, arg2, arg3, arg4, arg5);
        }
        private static (float, float, float, float, float, float) UnpackArguments(float arg1, float arg2, float arg3, float arg4, float arg5, float arg6)
        {
            return (arg1, arg2, arg3, arg4, arg5, arg6);
        }
        private static (float, float, float, float, float, float, float) UnpackArguments(float arg1, float arg2, float arg3, float arg4, float arg5, float arg6, float arg7)
        {
            return (arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        #endregion

        public static Vector3 GetTemplate(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = 0;
            point.y = 0;
            point.z = 0;
            return point;
        }
        
        
        public static Vector3 GetAstroidal_Ellipsoid(float[] args)
        {
            // x = a*cos^3(u)*cos^3(v), y = b*sin^3(u)*cos^3(v), z = c*sin^3(v)
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            float CosV3 = Mathf.Pow(Mathf.Cos(v), 3);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a * Mathf.Pow(Mathf.Cos(u),3) * CosV3;
            point.y = b * Mathf.Pow(Mathf.Sin(u), 3) * CosV3;
            point.z = c * Mathf.Pow(Mathf.Sin(v), 3);
            return point;
        }
        public static Vector3 GetBentHorns(float[] args)
        {
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = (2 + Mathf.Cos(u))*(v/a-Mathf.Sin(v));
            point.y = (2 + Mathf.Cos(u+PI2/a))*(Mathf.Cos(v)-1);
            point.z = (2 + Mathf.Cos(u-PI2/a))*(Mathf.Cos(v)-1);
            return point*0.5f; //Too large
        }
        public static Vector3 GetBonBon(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = v;
            point.y = a*Mathf.Cos(v)*Mathf.Sin(u);
            point.z = a*Mathf.Cos(v)*Mathf.Cos(u);
            return point;
        }
        public static Vector3 GetBoy_Apery(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            float SQRT2 = Mathf.Sqrt(2);
            float denom = SQRT2 - Mathf.Sin(2 * v) * Mathf.Sin(3 * u);
            if(denom == 0) { denom = 0.01f; }
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = (Mathf.Pow(Mathf.Cos(v), 2) * Mathf.Cos(2 * u) + Mathf.Sin(2 * v) * Mathf.Cos(u) / SQRT2) / denom;
            point.y = (Mathf.Pow(Mathf.Cos(v), 2) * Mathf.Sin(2 * u) - Mathf.Sin(2 * v) * Mathf.Sin(u) / SQRT2) / denom;
            point.z = SQRT2 * Mathf.Pow(Mathf.Cos(v), 2) / denom;
            return point;
        }
        public static Vector3 GetBreather(float[] args)
        {
            //x = -u + (2 * wsqr * cosh(aa * u) * sinh(aa * u) / denom), y = 2 * w * cosh(aa * u) * (-(w * cos(v) * cos(w * v)) - (sin(v) * sin(w * v))) / denom, z = 2 * w * cosh(aa * u) * (-(w * sin(v) * cos(w * v)) + (cos(v) * sin(w * v))) / denom
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            //Set temps
            float w = Mathf.Sqrt(1 - a * a);
            float denom = a * (Mathf.Pow(w * MathHelper.Cosh(a * u), 2) + Mathf.Pow(a * Mathf.Sin(w * v), 2));
            float COSA2U = MathHelper.Cosh(a * u);
            float COSWV = Mathf.Cos(w * v);
            float SINWV = Mathf.Sin(w * v);

            if (denom == 0) { denom = 0.01f; }
            //Solve Coords
            Vector3 point = new Vector3();
            point.x = -u + (2 * (1 - a * a) * COSA2U * MathHelper.Sinh(a * u) / denom);
            point.y = 2 * w * COSA2U * (-w * Mathf.Cos(v) * COSWV - Mathf.Sin(v) * SINWV) / denom;
            point.z = 2 * w * COSA2U * (-w * Mathf.Sin(v) * COSWV + Mathf.Cos(v) * SINWV) / denom;
            return point;
        }
        public static Vector3 GetCatalan(float[] args)
        {
            //Doubly periodic for now
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            float COSHV = MathHelper.Cosh(v);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a * (u - Mathf.Sin(u) * COSHV);
            point.y = a * (1 - Mathf.Cos(u) * COSHV);
            point.z = -4 * a * Mathf.Sin(u / 2) * MathHelper.Sinh(v / 2);
            return point;
        }
        public static Vector3 GetCatenoid(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a, float b) = UnpackArguments(args[0], args[1], args[2], args[3]);
            /* Set temps */
            float AVA = a * MathHelper.Cosh(v/a);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = b*AVA*Mathf.Cos(u);
            point.y = b*AVA*Mathf.Sin(u);
            point.z = b*v;
            return point;
        }
        public static Vector3 GetClifford_Torus(float[] args)
        {
            //x = cos(u+v)/(sqrt(2)+cos(v-u)), y = sin(v-u)/(sqrt(2)+cos(v-u)), z = sin(u+v)/(sqrt(2)+cos(v-u))
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            float denom = 1 - a * Mathf.Sin(u - v);
            if(denom == 0) denom = 0.01f;
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a * Mathf.Cos(u + v) / denom;
            point.y = a * Mathf.Sin(u + v) / denom;
            point.z = a * Mathf.Cos(u - v) / denom;
            return point;
        }
        public static Vector3 GetCosinus(float[] args)
        {
            //x = u, y = sin(pi*((u)**2+(v)**2))/2, z = v
            /* Unpack */
            (float u, float v, float a, float b) = UnpackArguments(args[0], args[1], args[2], args[3]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a*u;
            point.y = b*Mathf.Sin(Mathf.PI*(u*u+v*v))/2f;
            point.z = a*v;
            return point;
        }

        public static Vector3 GetCrescent(float[] args)
        {
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            float temp = a+Mathf.Sin(b*PI*u)*Mathf.Sin(b*PI*v);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = temp*Mathf.Sin(c*PI*v);
            point.y = temp*Mathf.Cos(c*PI*v);
            point.z = Mathf.Cos(b*PI*u)*Mathf.Sin(b*PI*v)+4*v-2;
            return point;
        }

        public static Vector3 GetCrossCap(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = (Mathf.Sin(u)*Mathf.Sin(2*v)/2f);
            point.y = (Mathf.Sin(2*u)*Mathf.Cos(v)*Mathf.Cos(v));
            point.z = (Mathf.Cos(2*u)*Mathf.Cos(v)*Mathf.Cos(v));
            return point;
        }
        public static Vector3 GetCyclide(float[] args)
        {
            //x = (d*(c-acos(u)cos(v))+b*b*cos(u))/denom, y = b*sin(u)*(a-d*cos(v))/denom, z = b*sin(v)*(c*cos(u)-d)/denom
            /* Unpack */
            (float u, float v, float a, float b, float c, float d) = UnpackArguments(args[0], args[1], args[2], args[3], args[4], args[5]);
            /* Set temps */
            float denom = a - c * Mathf.Cos(u) * Mathf.Cos(v);
            if(denom == 0) { denom = 0.01f; }
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = (d*(c - a * Mathf.Cos(u) * Mathf.Cos(v)) + b*b*Mathf.Cos(u))/denom;
            point.y = b*Mathf.Sin(u)*(a-d*Mathf.Cos(v))/denom;
            point.z = b*Mathf.Sin(v)*(c*Mathf.Cos(u)-d)/denom;
            return point;
        }
        public static Vector3 GetDing_Dong(float[] args)
        {
            //x = a*v*sqrt(1-v) *cos(u), y = a*v*sqrt(1-v)*sin(u), z = a*v
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            float temp = v * Mathf.Sqrt(1 - v);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = temp * Mathf.Cos(u);
            point.y = temp * Mathf.Sin(u);
            point.z = v;
            return point;
            
        }
        public static Vector3 GetDini(float[] args)
        {
            //x = a*cos(u)*sin(v), y = a*sin(u)*sin(v), z = a*(cos(v) + ln(tan(v / 2))) + b*u
            /* Unpack */
            (float u, float v, float a, float b) = UnpackArguments(args[0], args[1], args[2], args[3]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a*Mathf.Cos(u)*Mathf.Sin(v);
            point.y = a*Mathf.Sin(u)*Mathf.Sin(v);
            point.z = a*(Mathf.Cos(v) + Mathf.Log(Mathf.Tan(v/2f))) + b*u;
            return point;
        }
        public static Vector3 GetEnneper(float[] args)
        {
            //x = u-(u^3)/3 + u*v*v, y = -v-u*u*v + (v^3)/3, z = u*u-v*v
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            float U2 = u * u;
            float V2 = v * v;
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = u - U2*u/3f + u*V2;
            point.y = v - V2*v/3f + v * U2;
            point.z = U2-V2;
            return point;
        }
        public static Vector3 GetHyperbolic_Helicoid(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            float denom = 1 + MathHelper.Cosh(u) * MathHelper.Cosh(v);
            if (denom == 0) { denom = 0.01f; }
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = MathHelper.Sinh(v) * Mathf.Cos(a * u) / denom;
            point.y = MathHelper.Sinh(v) * Mathf.Sin(a * u) / denom;
            point.z = MathHelper.Cosh(v) * MathHelper.Sinh(u) / denom;
            return point;
        }
        public static Vector3 GetIsolator(float[] args)
        {
            //x = cos(u)*cos(v)+sin((sin(u)+1)*2*pi), y = 4*sin(u), z = cos(u)*sin(v)+cos((sin(u)+1)*2*pi)
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            float temp = (a + b*Mathf.Sin(c*u*PI2));
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = u;
            point.y = temp*Mathf.Sin(v);
            point.z = temp*Mathf.Cos(v);
            return point;
        }
        public static Vector3 GetKlein_Bottle(float[] args)
        {
            //x = cos(u)*(cos(u/2)*(sqrt(2)+cos(v))+sin(u/2)*sin(v)*cos(v)), y = sin(u)*(cos(u/2)*(sqrt(2)+cos(v))+sin(u/2)*sin(v)*cos(v)), z = -sin(u/2)*(sqrt(2)+cos(v))+cos(u/2)*sin(v)*cos(v)
            //https://mathcurve.com/surfaces.gb/klein/klein.shtml
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            float r = c * (1 - Mathf.Cos(u) / 2);
            /* Solve Coords */
            Vector3 point = new Vector3();
            if (u < Mathf.PI)
            {
                point.x = (a * (1 + Mathf.Sin(u)) + r * Mathf.Cos(v)) * Mathf.Cos(u);
                point.y = (b + r * Mathf.Cos(v)) * Mathf.Sin(u);
                point.z = r * Mathf.Sin(v);

            }
            else
            {
                point.x = a * (1 + Mathf.Sin(u)) * Mathf.Cos(u) - r * Mathf.Cos(v);
                point.y = b * Mathf.Sin(u);
                point.z = r * Mathf.Sin(v);
            }
            return point;
        }
        public static Vector3 GetKuen(float[] args)
        {
            //x = 2*cosh(v)*(cos(u)+u*sin(u))/denom, y = 2*cosh(v)*(-u*cos(u)+sin(u))/denom, z = v-(2*sinh(v)*cosh(v))/denom
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            float denom = 1 + Mathf.Pow(Mathf.Sin(v) * u, 2);
            if (denom == 0) { denom = 0.01f; }
            float SINV2 = 2 * Mathf.Sin(v);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = SINV2 * (Mathf.Cos(u) + u * Mathf.Sin(u)) / denom;
            point.y = SINV2 * (-u * Mathf.Cos(u) + Mathf.Sin(u)) / denom;
            point.z = Mathf.Log(Mathf.Tan(v / 2)) + 2 * Mathf.Cos(v) / denom;
            return point;
        }
        public static Vector3 GetMobius_Strip(float[] args)
        {
            // x = (1+u*cos(v/2))*cos(v), y = (1+u*cos(v/2))*sin(v), z = u*sin(v/2)
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            Vector3 point = new Vector3();
            point.x = (a + u * Mathf.Cos(v / 2f)) * Mathf.Cos(v);
            point.y = (a + u * Mathf.Cos(v / 2f)) * Mathf.Sin(v);
            point.z = u * Mathf.Sin(v / 2f);
            return point;
        }
        public static Vector3 GetPseudocatenoid(float[] args)
        {
            //x = 2.2*(2*cosh(v/2)*cos(u), y = 1.51166 * (2*cosh(v/2)*sin(u) * sin((2.2*(2*cosh(v/2)*cos(u)) - -11.0404)*2*pi*1/22.0513) + 1.8*(v) * cos((2.2*(2*cosh(v/2)*cos(u)) - -11.0404)*2*pi*1/22.0513)),
            //z = 1.51166 * (2*cosh(v/2)*sin(u) * cos((2.2*(2*cosh(v/2)*cos(u)) - -11.0404)*2*pi*1/22.0513) - 1.8*(v) * sin((2.2*(2*cosh(v/2)*cos(u)) - -11.0404)*2*pi*1/22.0513))
            /* Unpack */
            (float u, float v, float a, float b, float c, float d) = UnpackArguments(args[0], args[1], args[2], args[3], args[4], args[5]);
            /* Set temps */
            d *= 2;
            b /= 20;
            a /= 10;
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a * (2 * MathHelper.Cosh(v / 2) * Mathf.Cos(u));
            point.y = b * (2 * MathHelper.Cosh(v / 2) * Mathf.Sin(u) * Mathf.Sin((a * (2 * MathHelper.Cosh(v / 2) * Mathf.Cos(u)) + 11.0404f) * 2 * Mathf.PI * 1 / d) + c * v * Mathf.Cos((a * (2 * MathHelper.Cosh(v / 2) * Mathf.Cos(u)) + 11.0404f) * 2 * Mathf.PI * 1 / d));
            point.z = b * (2 * MathHelper.Cosh(v / 2) * Mathf.Sin(u) * Mathf.Cos((a * (2 * MathHelper.Cosh(v / 2) * Mathf.Cos(u)) + 11.0404f) * 2 * Mathf.PI * 1 / d) - c * v * Mathf.Sin((a * (2 * MathHelper.Cosh(v / 2) * Mathf.Cos(u)) + 11.0404f) * 2 * Mathf.PI * 1 / d));
            return point;
        }
        public static Vector3 GetPseudosphere(float[] args)
        {
            //x = cos(u)*cos(v)+sin((sin(u)+1)*2*pi), y = 4*sin(u), z = cos(u)*sin(v)+cos((sin(u)+1)*2*pi)
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = c * Mathf.Cos(u) * Mathf.Cos(v) + Mathf.Sin((Mathf.Sin(u) + 1) * 2 * b * Mathf.PI);
            point.y = a * 4 * Mathf.Sin(u);
            point.z = c * Mathf.Cos(u) * Mathf.Sin(v) + Mathf.Cos((Mathf.Sin(u) + 1) * 2 * b * Mathf.PI);
            return point;
        }
        public static Vector3 GetRoman(float[] args)
        {
            //x = 2u*cos(v), y = , z = 
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            float temp = Mathf.Sqrt(1 - u * u);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = 2 * u * Mathf.Cos(v) * temp;
            point.y = 2 * u * Mathf.Sin(v) * temp;
            point.z = 2 * u * u * Mathf.Cos(v) * Mathf.Sin(v);
            return point;
        }
        public static Vector3 GetScherk(float[] args)
        {
            //Doubly periodic for now
            //x = u/a, y = v/a, z = 1/a*ln(cos(v)/cos(u))
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = u/a;
            point.y = v/a;
            point.z = 1/a*Mathf.Log(Mathf.Cos(v)/Mathf.Cos(u));
            return point;
        }
        public static Vector3 GetSnail_Shell(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a, float b, float c, float d) = UnpackArguments(args[0], args[1], args[2], args[3], args[4], args[5]);
            /* Set temps */
            c = c / 10;
            float vv = v + Mathf.Pow(v-2, 2)/16;
            float s = Mathf.Exp(-c*vv);
            float r = s * a + s * b * Mathf.Cos(u);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = r * Mathf.Cos(vv);
            point.y = d*(1-s) + s*b*Mathf.Sin(u);
            point.z = r*Mathf.Sin(vv);
            return point;
        }
        public static Vector3 GetSteinbach_Screw(float[] args)
        {
            //x = , y = , z = 
            /* Unpack */
            (float u, float v, float a, float b) = UnpackArguments(args[0], args[1], args[2], args[3]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a * u * Mathf.Cos(v);
            point.y = a * u * Mathf.Sin(v);
            point.z = b * v * Mathf.Cos(u);
            return point;
        }
        public static Vector3 GetSteroSphere(float[] args)
        {
            //x = 2*u/(u*u+v*v+1), y = (u*u+v*v-1)/(u*u+v*v+1), z = 2*v/(u*u+v*v+1)
            /* Unpack */
            (float u, float v, float a) = UnpackArguments(args[0], args[1], args[2]);
            /* Set temps */
            float denom = (u*u+v*v+1);
            if(denom == 0) { denom = 0.01f; }
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a*u / denom;
            point.y = (denom-2)/denom;
            point.z = a*v/denom;
            return point;
        }
        public static Vector3 GetTorus(float[] args)
        {
            //x = a*(cos(v)+u*cos(halftwists*v/2)*cos(v)), y = a*(sin(v)+u*cos(halftwists*v/2)*sin(v)), z = a*u*sin(v/2)
            /* Unpack */
            (float u, float v, float a, float b) = UnpackArguments(args[0], args[1], args[2], args[3]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = (a+b*Mathf.Cos(v))*Mathf.Cos(u);
            point.y = (a + b * Mathf.Cos(v)) * Mathf.Sin(u);
            point.z = b*Mathf.Sin(v);
            return point;
        }
        public static Vector3 GetTriaxialTeardrop(float[] args)
        {
            //
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = (1-Mathf.Cos(u))*Mathf.Cos(u+PI2/3)*Mathf.Cos(v+PI2/3)/2;
            point.y = (1-Mathf.Cos(u))*Mathf.Cos(u+PI2/3)*Mathf.Cos(v-PI2/3)/2;;
            point.z = Mathf.Cos(u-PI2/3);
            return point*2;
        }
        public static Vector3 GetTwisted_Klein_Bottle(float[] args)
        {
            // x = (aa + cos(v / 2) * sin(u) - sin(v / 2) * sin(2 * u)) * cos(v), y = (aa + cos(v / 2) * sin(u) - sin(v / 2) * sin(2 * u)) * sin(v), z = sin(v / 2) * sin(u) + cos(v / 2) * sin(2 * u)
            (float u, float v, float a, float b) = UnpackArguments(args[0], args[1], args[2], args[3]);
            float SinV2 = Mathf.Sin(v / 2f);
            float Sin2U = Mathf.Sin(b * 2f * u);
            float CosV2 = Mathf.Cos(v / 2f);
            float temp1 = (float)a + CosV2 * Mathf.Sin(u) - SinV2 * Sin2U;
            Vector3 point = new Vector3();
            point.x = temp1 * Mathf.Cos(v);
            point.y = temp1 * Mathf.Sin(v);
            point.z = SinV2 * Mathf.Sin(u) + CosV2 * Sin2U;
            return point;
        }
        public static Vector3 GetTwisted_Torus(float[] args)
        {
            //x = cos(u)*(6-(5./4. + sin(3*v))*sin(v-3*u)), y = (6-(5./4. + sin(3*v))*sin(v-3*u))*sin(u), z = -cos(v-3*u)*(5./4.+sin(3*v))
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            b = (int)b / 3f;
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = Mathf.Cos(u) * (a - (c + c*Mathf.Sin(3 * v)) * Mathf.Sin(v - b * u));
            point.y = (a - (c + c*Mathf.Sin(3 * v)) * Mathf.Sin(v - b * u)) * Mathf.Sin(u);
            point.z = -Mathf.Cos(v - b * u) * (c + c*Mathf.Sin(3 * v));
            return point;
        }

        public static Vector3 GetWellenkugel(float[] args)
        {
            //
            /* Unpack */
            (float u, float v) = UnpackArguments(args[0], args[1]);
            /* Set temps */
            float COSU = Mathf.Cos(u);
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = u*Mathf.Cos(COSU)*Mathf.Cos(v);
            point.y = u*Mathf.Cos(COSU)*Mathf.Sin(v);
            point.z = u*Mathf.Sin(COSU);
            return point*0.25f;
        }


        //TBC
        //Meh
        public static Vector3 GetTrinoid(float[] args)
        {
            //
            /* Unpack */
            (float u, float v, float a, float b, float c) = UnpackArguments(args[0], args[1], args[2], args[3], args[4]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = 0;
            point.y = 0;
            point.z = 0;
            return point;
        }
        public static Vector3 GetBraidedTorus(float[] args)
        {
            //
            /* Unpack */
            (float u, float v, float a, float b, float c, float d, float e) = UnpackArguments(args[0], args[1], args[2], args[3], args[4], args[5], args[6]);
            /* Set temps */
            /* Solve Coords */
            Vector3 point = new Vector3();
            point.x = a*Mathf.Cos(v)*Mathf.Cos(u) + b*Mathf.Cos(u)*(1+c*Mathf.Cos(d*u));
            point.y = e*(a*Mathf.Sin(v) + c*Mathf.Sin(d*u));
            point.z = a*Mathf.Cos(v)*Mathf.Sin(u) + b*Mathf.Sin(u)*(1+c*Mathf.Cos(d*u));
            return point;
        }
        
        //Gyroid?
    }
}

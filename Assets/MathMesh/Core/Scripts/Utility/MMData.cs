using System.Collections.Generic;
using UnityEngine;

namespace MathMesh
{
    public static partial class MathMeshUtility
    {
        public readonly static float GoldenRatio = (1 + Mathf.Sqrt(5f)) / 2f;
        public readonly static float PI = Mathf.PI;
        public readonly static float PI2 = 2f * Mathf.PI;
        public readonly static float PI4 = 4f * Mathf.PI;

        public delegate TOut ParamsFunc<TIn, TOut>(params TIn[] args);

        public readonly static Dictionary<MeshType, SurfaceData> surfaceDict = new Dictionary<MeshType, SurfaceData>()
        {
            { MeshType.Astroidal_Ellipsoid,
                new SurfaceData("Astroidal_Ellipsoid", 3, new float[]{ 0,PI,0,PI2,2,2,2 }) },
            { MeshType.BentHorns,
                new SurfaceData("BentHorns", 1, new float[]{ -PI,PI,-PI2,PI2, 3}, new Constraints(new string[] {"MinU", "MinV", "MaxU", "MaxV", "MinA"}, new float[] { -PI, -PI2, PI, PI2, 1f})) },
            { MeshType.BonBon,
                new SurfaceData("BonBon", 1, new float[]{ 0,PI2,-PI,PI, 1}) },
            { MeshType.Boy_Apery,
                new SurfaceData("Boy_Apery", 0, new float[]{ 0,PI,0,PI}, new Constraints(new string[] {"MinU", "MinV", "MaxU", "MaxV"}, new float[] { 0, 0, PI, PI})) },
            { MeshType.Boy_BK,
                new SurfaceData("Boy_BK", 0, new float[]{ 0,1,0,PI2}, new Constraints(new string[] {"MinU", "MaxU", "MinV", "MaxV"}, new float[] {0f, 1f, 0f, PI2})) },
            { MeshType.Breather,
                new SurfaceData("Breather", 1, new float[]{ -14,14,-37.4f,37.4f, 0.4f}, new Constraints(new string[] {"MinA", "MaxA"}, new float[] {0.01f, 0.99f})) },
             { MeshType.Braided_Torus,
                new SurfaceData("BraidedTorus", 5, new float[]{ 0, 8*PI, 0, PI2, 0.1f, 1, 0.5f, 1.75f, 1f}) },
            { MeshType.Catenoid,
                new SurfaceData("Catenoid", 2, new float[]{ 0,PI2,-PI2,PI2, 6, 0.1f }, new Constraints(new string[] {"MinA"}, new float[] {0.75f}) )},
            { MeshType.Catalan,
                new SurfaceData("Catalan", 1, new float[]{ 0,PI4+PI2, 0,PI, 0.1f }, new Constraints(new string[] {"MinV"}, new float[] {0.01f})) },
            { MeshType.Clifford_Torus,
                new SurfaceData("Clifford_Torus", 1, new float[]{ 0,PI,0,PI2, 0.6f}, new Constraints(new string[] {"MaxA"}, new float[] {1f})) },
            { MeshType.Cosinus,
                new SurfaceData("Cosinus", 2, new float[]{ -1,1,-1,1, 1, 1}) },
             { MeshType.Crescent,
                new SurfaceData("Crescent", 3, new float[]{ 0,1,0, 1, 2, 2, 3}, new Constraints(new string[] {"MinU", "MinV", "MaxU", "MaxV"}, new float[] { 0, 0, 1, 1})) },
            { MeshType.CrossCap,
                new SurfaceData("CrossCap", 0, new float[]{ 0,PI,0,PI}) },
            { MeshType.Cyclide,
                new SurfaceData("Cyclide", 4, new float[]{ 0, PI2, 0, PI2, 1, 0.98f, 0.3f, 0.3f }) },
            { MeshType.Ding_Dong,
                new SurfaceData("Ding_Dong", 0, new float[]{ 0,PI2,-1,1}, new Constraints(new string[] {"MaxV"}, new float[] {1})) },
            { MeshType.Dini,
                new SurfaceData("Dini", 2, new float[] {-PI2, PI4, 0.01f, PI, 0.7f, 0.3f}, new Constraints(new string[] {"MinV", "MaxV"}, new float[] {0.01f, PI-0.0001f})) },
            { MeshType.Enneper,
                new SurfaceData("Enneper", 0, new float[]{ -PI/4, PI/4, -PI/4, PI/4}) },
            { MeshType.HyperBolic_Helicoid,
                new SurfaceData("Hyperbolic_Helicoid", 1, new float[]{ -4, 4, -4, 4, 4}) },
            { MeshType.Isolator,
                new SurfaceData("Isolator", 3, new float[]{ -3.5F, 3f, 0, PI2, 0.8f, 0.6f, 1}) },
            { MeshType.Klein_Bottle,
                new SurfaceData("Klein_Bottle", 3, new float[]{ 0, PI2, 0, PI2, 1, 2, 0.5f }) },
            { MeshType.Kuen,
                new SurfaceData("Kuen", 0, new float[]{ 0, PI2, 0, PI}, new Constraints(new string[] {"MinV", "MinU", "MaxV"}, new float[] { 0.01f, 0, PI-0.01f }) ) },
            { MeshType.Mobius_Strip,
                new SurfaceData("Mobius_Strip", 1, new float[]{ -0.5f, 0.5f, 0, PI2, 1 }) },
            { MeshType.Pseudocatenoid,
                new SurfaceData("Pseudocatenoid", 4, new float[]{ 0, PI2, 0, PI2, 1.5f, 1.5f, 1, 1.8f }) },
            { MeshType.Pseudosphere,
                new SurfaceData("Pseudosphere", 3, new float[]{ 0, PI2, 0, PI, 1, 1, 1 }) },
            { MeshType.Roman,
                new SurfaceData("Roman", 0, new float[]{ 0, 1, 0, PI2 }, new Constraints(new string[] {"MinU", "MaxU"}, new float[] { 0, 1 } ) ) },
            { MeshType.Scherk,
                new SurfaceData("Scherk", 1, new float[] {-PI/2f, PI/2f, -PI/2f, PI/2f, 2 }, new Constraints(new string[] {"MinV", "MinU", "MaxV", "MaxU", "MinA"}, new float[] { -PI / 2 + 0.01f, -PI / 2 + 0.01f, PI/2-0.01f, PI/2-0.01f, 0.05f}) )},
            { MeshType.Snail_Shell,
                new SurfaceData("Snail_Shell", 4, new float[]{ 0, PI2, 0, PI4*2, 1.5f,1.5f,1,4.5f }) },
            { MeshType.Steinbach_Screw,
                new SurfaceData("Steinbach_Screw", 2, new float[]{ -4, 4, 0, 7, 0.2f, 0.3f } ) },
            { MeshType.SteroSphere,
                new SurfaceData("SteroSphere", 1, new float[]{ -PI, PI, -PI, PI, 2f } ) },
            { MeshType.Torus,
                new SurfaceData("Torus", 2, new float[]{ 0, PI2, 0, PI2, 0.6f, 0.2f }) },
            { MeshType.TriaxialTeardrop,
                new SurfaceData("TriaxialTeardrop", 0, new float[]{ 0, PI, 0, PI2 }, new Constraints(new string[] {"MinU", "MinV", "MaxU", "MaxV"}, new float[] { 0, 0, PI, PI2}) )},
            { MeshType.Twisted_Klein_Bottle,
                new SurfaceData("Twisted_Klein_Bottle", 2, new float[]{ 0, PI2, 0, PI2, 2, 1 }) },
            { MeshType.Twisted_Torus,
                new SurfaceData("Twisted_Torus", 3, new float[]{ 0, PI2, 0, PI2, 1, 7, 0.2f }) },
            { MeshType.Wellenkugel,
                new SurfaceData("Wellenkugel", 0, new float[]{ 0, 14.5f, 0, PI2 }) },
        };
    }
}
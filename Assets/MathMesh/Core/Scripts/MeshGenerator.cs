using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
// using UnityEditor.Formats.Fbx.Exporter;
#endif

namespace MathMesh
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    public class MeshGenerator : MonoBehaviour
    {

        //Mesh vars
        public Mesh mesh;
        // for selection of mesh type in editor
        public MeshType meshType = MeshType.Astroidal_Ellipsoid;
        public MathMeshTopology topologyType = MathMeshTopology.Triangles;
        [HideInInspector]
        public Vector3[] vertices;
        private Vector3[] normals;
        private Vector2[] uvs;
        private int[] triangles;
        private int[] indices;
        private Color[] colors;


        private SurfaceData _surface;
        public SurfaceData currentSurface
        {
            get => _surface ?? MathMeshUtility.surfaceDict.GetValueOrDefault(meshType);
            set {
                if (value != null)
                {
                    meshType = MeshType.Custom;
                }
                _surface = value;
            }
        }

        //Settings
        [Min(1)]
        public int uSlices = 1;
        [Min(1)]
        public int vSlices = 1;
        public bool doubleSided = true;
        public bool autoUpdate = false;
        public bool displayVertices = false;
        public bool displayNormals = false;
        public Gradient gradient;
        //x = min, y = max
        //U is the width
        public Vector2 u;
        //V is the length
        public Vector2 v;
        //Additional Vars
        public float[] vars = new float[5] {0,0,0,0,0};
        //Size
        public float size = 1f;

        //Constraint reflection
        MethodInfo info;
        Type type = typeof(Constraints);

        void Start()
        {
            GenerateMesh();
        }

        public void GenerateMesh()
        {
            uSlices = Mathf.Min(200, uSlices);
            vSlices = Mathf.Min(200, vSlices);
            //Ensure meshtype has been selected properly
            if (currentSurface == null)
            {
                return;
            }
            //Create mesh
            mesh = new Mesh();
            mesh.subMeshCount = 1;

            ApplyConstraints();

            //Separation between width nodes and length nodes
            float u_diff = uSlices == 1 ? (u.y - u.x) / 2f : (u.y - u.x) / (uSlices - 1);
            float v_diff = vSlices == 1 ? (v.y - v.x) / 2f : (v.y - v.x) / (vSlices-1);

            int vertCount = doubleSided ? uSlices * vSlices * 2 : uSlices * vSlices;
            CreateVertices(u_diff, v_diff, vertCount);
            //Only create triangles if meshrenderer is activated
            if (GetComponent<MeshRenderer>().enabled)
            {
                if (topologyType == MathMeshTopology.Triangles)
                {
                    // list of index locations for the vertices making up each triangle
                    CreateTriangles();
                    //Apply mesh data
                    mesh.RecalculateNormals();
                    if(displayNormals) normals = mesh.normals;
                    mesh.RecalculateTangents();
                }
                else if (topologyType == MathMeshTopology.Lines)
                {
                    CreateLineIndices();
                }
                else if (topologyType == MathMeshTopology.Points)
                {
                    CreatePointIndices();
                }
            }

            CalculateUVs(vertCount);
            //Get the mesh type's name
            mesh.name = currentSurface.name;

            if(TryGetComponent(out MeshFilter m)){
                m.mesh = mesh;
            }
            if(TryGetComponent<MeshVFXManager>(out MeshVFXManager vfx)){
                    vfx.ApplyVFX(mesh);
            }
        }

        private void CreateVertices(float u_diff, float v_diff, int vertCount)
        {
            vertices = new Vector3[vertCount];
            //All vertices. filled with u values first, then iterate to the next v
            for (int i = 0; i < vSlices; i++)
            {
                for (int j = 0; j < uSlices; j++)
                {
                    vertices[i * uSlices + j] = size * currentSurface.GetPoint(u.x + u_diff * j, v.x + v_diff * i, vars[0], vars[1], vars[2], vars[3], vars[4]);
                }
            }

            //Before creating back vertices, replace vertices based on seal status
            //SealGeometry();
            if (doubleSided)
            {
                CreateBackVertices();
            }
            //Set
            mesh.vertices = vertices;
        }

        /// <summary>
        /// Duplicates all of the vertices for back-sided geometry
        /// </summary>
        private void CreateBackVertices() //Necessary to avoid lighting issues
        {
            //Start from halfway
            int length = vertices.Length / 2;
            for (int i = 0; i < length; i++)
            {
                vertices[length + i] = vertices[i];
            }
        }
        private void CreateTriangles()
        {
            int vertCount = uSlices * vSlices;
            int triCount = (uSlices-1) * (vSlices-1) * 6 ;
            triCount = doubleSided ? triCount * 2 : triCount;
            triangles = new int[triCount];

            for (int n = 0, l = 0, ti = 0; n < vSlices-1; n++, l++)
            {
                for (int k = 0; k < uSlices - 1; k++, ti += 6, l++)
                {
                    //Front
                    triangles[ti] = l;
                    triangles[ti + 1] = triangles[ti + 4] = (l + uSlices);
                    triangles[ti + 2] = triangles[ti + 3] = l + 1;
                    triangles[ti + 5] = (l + uSlices + 1);
                }
            }
            //last rung TODO:
            if (doubleSided)
            {
                CreateBackTriangles();
            }
            //Set
            mesh.triangles = triangles;

        }
        private void CreateBackTriangles()
        {
            //Start from halfway
            int length = triangles.Length / 2;
            int vertLength = uSlices * vSlices;
            for (int i = 0; i < length; i += 6)
            {
                triangles[length + i] = triangles[i] + vertLength;
                triangles[length + i + 1] = triangles[length + i + 4] = triangles[i + 2] + vertLength;
                triangles[length + i + 2] = triangles[length + i + 3] = triangles[i + 1] + vertLength;
                triangles[length + i + 5] = triangles[i + 5] + vertLength;
            }
        }
        private void CreateLineIndices()
        {
            int lineCount = uSlices * (vSlices - 1) * 4;
            indices = new int[lineCount];
            //Draw all lines except end of row
            for (int n = 0, l = 0, li = 0; n < vSlices - 1; n++, l++)
            {
                for (int k = 0; k < uSlices - 1; k++, li += 4, l++)
                {
                    //Front
                    indices[li] = indices[li+2] = l;
                    indices[li + 1] = l + uSlices;
                    indices[li + 3] = l+1;
                }
                indices[li] = l;
                indices[li+1] = l+uSlices;
                li += 2;
            }
            //Set
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }
        
        private void CreatePointIndices()
        {
            int pointCount = vertices.Length;
            indices = new int[pointCount];
            //Draw all lines except end of row
            for (int n = 0; n < pointCount; n++)
            {
                indices[n] = n;
            }
            //Set
            mesh.SetIndices(indices, MeshTopology.Points, 0);
        }

        private void CreateColors()
        {
            float sliceCount = doubleSided ? vSlices*2 : vSlices;
            float diff = 0.5f/vSlices;
            colors = new Color[vertices.Length];
            for (int i = 0; i < sliceCount; i++)
            {
                for (int j = 0; j < uSlices; j++)
                {
                    colors[i * uSlices + j] = gradient.Evaluate(diff*i);
                }
            }
            //Set
            mesh.colors = colors;
        }

        private void SealGeometry()
        {
            int sealVal = currentSurface.sealType;
            int sealReverse = currentSurface.sealReverse;
            int startVertex = doubleSided ? vertices.Length/2 : vertices.Length;
            startVertex = startVertex - uSlices;
            //if seal is 'u' only, or uv, replace last row of u with first row
            if(sealVal == 0)
            {
                return;
            }
            //Seal U
            else if(sealVal == 1 || sealVal == 3)
            {
                for(int i = 0; i < vSlices; i++)
                {
                    //Reverse order
                    vertices[uSlices*i + uSlices-1] = vertices[uSlices * i];
                }
            }
            //Seal V
            if(sealVal == 2 || sealVal == 3)
            {
                for (int i = 0; i < uSlices; i++)
                {
                    //Reverse
                    vertices[startVertex + i] = sealReverse >= 2 ? vertices[uSlices-i-1] : vertices[i];
                }
            }
            //First though, we check to make sure we're at the seal value (2pi)
        }

        private void ApplyConstraints()
        {
            Constraints tempCons = currentSurface.constraints;
            if(tempCons == null)
            {
                return;
            }
            foreach (KeyValuePair<string, float> entry in tempCons.constraints)
            {
                info = type.GetMethod("Get" + entry.Key);
                if (entry.Key.EndsWith("U")){
                    u.x = Helper(entry.Key, u.x, entry.Value);
                    u.y = Helper(entry.Key, u.y, entry.Value);
                }
                else if (entry.Key.EndsWith("V"))
                {
                    v.x = Helper(entry.Key, v.x, entry.Value);
                    v.y = Helper(entry.Key, v.y, entry.Value);
                }
                else if (entry.Key.EndsWith("A"))
                {
                    vars[0] = Helper(entry.Key, vars[0], entry.Value);
                }
                else if (entry.Key.EndsWith("B"))
                {
                    vars[1] = Helper(entry.Key, vars[1], entry.Value);
                }
                else if (entry.Key.EndsWith("C"))
                {
                    vars[2] = Helper(entry.Key, vars[2], entry.Value);
                }
                else if (entry.Key.EndsWith("D"))
                {
                    vars[3] = Helper(entry.Key, vars[3], entry.Value);
                }
            }

            float Helper(string entry, float param, float limit){
                if (entry.StartsWith("Max"))
                {
                    return Mathf.Min(param, limit);
                }
                else if (entry.StartsWith("Min"))
                {
                    return Mathf.Max(param, limit);
                }
                return param;
            }
        }


        /*
         xDistances array = 
         0 * * * * * * *
         0 * * * * * * *
         0 * * * * * * *
         0 * * * * * * *
         0 * * * * * * *
         0 * * * * * * *
         yDistances array =
         * * * * * * * *
         * * * * * * * *
         * * * * * * * *
         * * * * * * * *
         * * * * * * * *
         0 0 0 0 0 0 0 0
         
         
         */
        //calculate uvs. could be done in create vertices to save computation time *shrug*
        //time complexity is uSlices*vSlices = O(n*m)
        private void CalculateUVs(int vertCount)
        {
            //Create arrays
            uvs = new Vector2[vertCount];
            int distArrSize = doubleSided ? vertCount / 2 : vertCount;
            float[] xDistances = new float[distArrSize];
            float[] yDistances = new float[distArrSize];
            float maxX = 0;
            float maxY = 0;

            //Calculate distances for hypothetical plane
            //distances are required to determine fractional uv mappings. imagine mapping the mesh onto a 2d plane
            //largest summed j distances = width of hypothetical plane
            //largest summed i distances = height of hypothetical plane
            //Not sure if this is the correct approach, but its what I came up with
            for (int i = 0; i < vSlices; i++)
            {
                float prevVal = 0;
                for (int j = 1; j < uSlices; j++)
                {
                    xDistances[i * uSlices + j] = prevVal + Vector3.Distance(vertices[i * uSlices + j], vertices[i * uSlices + (j - 1)]);
                    prevVal = xDistances[i * uSlices + j];
                }
                if (prevVal > maxX) maxX = prevVal;
            }

            //yDistances
            for (int j = 0; j < uSlices; j++)
            {
                float prevVal = 0;
                for (int i = 1; i < vSlices; i++)
                {
                    yDistances[i * uSlices + j] = prevVal + Vector3.Distance(vertices[i * uSlices + j], vertices[(i - 1) * uSlices + j]);
                    prevVal = yDistances[i * uSlices + j];
                }
                if (prevVal > maxY) maxY = prevVal;
            }

            //Now that we have the max X and Y, we can normalize each vertexes position and set all UVs from 0 to 1
            for (int i = 0; i < vSlices; i++)
            {
                for (int j = 0; j < uSlices; j++)
                {
                    uvs[i * uSlices + j] = new Vector2(xDistances[i * uSlices + j] / maxX, yDistances[i * uSlices + j] / maxY);
                }
            }

            if (doubleSided)
            {
                CalculateBackUVs();
            }

            //Finally set the uvs
            mesh.uv = uvs;
        }

        private void CalculateBackUVs()
        {
            //Start from halfway
            int length = uvs.Length / 2;
            for (int i = 0; i < length; i++)
            {
                uvs[length + i] = uvs[i];
            }
        }

#if UNITY_EDITOR
        public bool ExportMesh()
        {
            try
            {
                var filePath = EditorUtility.SaveFilePanel("Title", Application.dataPath, mesh.name, "fbx");
                // ModelExporter.ExportObject(filePath, Selection.activeObject);
                Debug.LogError("Not implemented anymore... Unity namespace was missing.");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ResetToDefaults()
        {
            float[] vals = currentSurface.defaults;
            u.x = vals[0];
            u.y = vals[1];
            v.x = vals[2];
            v.y = vals[3];

            //Quicker way of assigning variables without if statements
            for(int i = 4; i < vals.Length; i++)
            {
                vars[i-4] = vals[i];
            }
            GenerateMesh();
        }

        void OnDrawGizmos()
        {
            //Called without mesh, skip
            if (vertices == null) { return; }

            if(displayVertices || displayNormals){
                Gizmos.matrix = transform.localToWorldMatrix;
                int len = vertices.Length;
                if(doubleSided && displayVertices && !displayNormals) len /= 2;
                for (int i = 0; i < len; i++)
                {
                    Gizmos.color = Color.red;
                    if((i+1) % uSlices == 0) { Gizmos.color = Color.blue; }
                    else if (i % uSlices == 0) { Gizmos.color = Color.green; }
                    Vector3 pos = vertices[i];
                    if(displayVertices){
                        Gizmos.DrawSphere(pos, 0.01f);
                    }
                    if(displayNormals && normals != null){
                        if(i < normals.Length){
                            Gizmos.DrawLine(pos, pos+normals[i]);
                        }
                    }
                }
            }
        }

        void _OnValidate() {
            if(this == null) {
                return;
            }
            GenerateMesh();
        }

        private void OnValidate()
        {
            if (autoUpdate)
            {
                EditorApplication.delayCall += _OnValidate;
            }
        }
        #endif
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MathMesh
{
    [CustomEditor(typeof(MeshGenerator))]
    public class MeshGenEditor : Editor
    {
        //Foldouts
        bool meshProperties = true;
        bool editorProperties = true;

        string pi = "\u03C0";
        string[] varNames = new string[5] {"A", "B", "C", "D", "E"};
        MeshGenerator mg;
        //Bools and enums
        SerializedProperty autoUpdateProp;
        SerializedProperty displayVerticesProp;
        SerializedProperty displayNormalsProp;
        SerializedProperty doubleSidedProp;
        SerializedProperty meshTypeProp;
        SerializedProperty topologyProp;
        SerializedProperty twoColoredProp;
        //Vars
        SerializedProperty uSlicesProp;
        SerializedProperty vSlicesProp;
        SerializedProperty vProp;
        SerializedProperty uProp;
        SerializedProperty varsProp;
        SerializedProperty sizeProp;
        //Colors
        SerializedProperty gradientProp;

        private void OnEnable()
        {
            mg = serializedObject.targetObject as MeshGenerator;
            //Bools and Enums
            meshTypeProp = serializedObject.FindProperty("meshType");
            topologyProp = serializedObject.FindProperty("topologyType");
            autoUpdateProp = serializedObject.FindProperty("autoUpdate");
            displayVerticesProp = serializedObject.FindProperty("displayVertices");
            displayNormalsProp = serializedObject.FindProperty("displayNormals");
            doubleSidedProp = serializedObject.FindProperty("doubleSided");
            twoColoredProp = serializedObject.FindProperty("twoColored");
            //Vars
            uSlicesProp = serializedObject.FindProperty("uSlices");
            vSlicesProp = serializedObject.FindProperty("vSlices");
            vProp = serializedObject.FindProperty("v");
            uProp = serializedObject.FindProperty("u");
            varsProp = serializedObject.FindProperty("vars");
            sizeProp = serializedObject.FindProperty("size");
            //Colors
            gradientProp = serializedObject.FindProperty("gradient");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            //base.OnInspectorGUI();

            EditorGUILayout.PropertyField(meshTypeProp, new GUIContent("Mesh Type", "The Parametric surface being constructed."));
            EditorGUILayout.PropertyField(topologyProp, new GUIContent("Mesh Topology", "For FBX exporting, mesh must be in standard mode. Additionally, line and point mode may not display properly (dark) from certain angles in scene view."));
            DisplayEditorProperties();
            DisplayMeshProperties();
            
            if (GUILayout.Button("Export Mesh"))
            {
                mg.ExportMesh();
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void DisplayEditorProperties()
        {
            editorProperties = EditorGUILayout.BeginFoldoutHeaderGroup(editorProperties, new GUIContent("Editor Properties", "Editor Property Settings"));
            EditorGUI.indentLevel++;
            if (editorProperties)
            {
                EditorGUILayout.PropertyField(autoUpdateProp, new GUIContent("Auto Update", "Update the mesh immediately after making changes in the inspector. Performance heavy, but good for quick debugging."));
                EditorGUILayout.PropertyField(doubleSidedProp, new GUIContent("Double Sided", "Display front and back faces of the mesh. Duplicate geometry created when enabled."));
                EditorGUILayout.PropertyField(displayVerticesProp, new GUIContent("Display Vertices", "Debug only feature. Display vertices in scene as gizmo spheres."));
                EditorGUILayout.PropertyField(displayNormalsProp, new GUIContent("Display Normals", "Debug only feature. Display normals in scene as gizmo lines."));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        private void DisplayMeshProperties()
        {
            meshProperties = EditorGUILayout.BeginFoldoutHeaderGroup(meshProperties, new GUIContent("Mesh Properties", "Mesh Property Settings"));
            EditorGUI.indentLevel++;
            if (meshProperties)
            {
                //USlices
                EditorGUILayout.PropertyField(uSlicesProp, new GUIContent("U-Slices", "U-Slices is equivalent to something like the rows in a table. Increasing this increases the number of vertices, as vertices = U-Slices * V-Slices"));
                //VSlices
                EditorGUILayout.PropertyField(vSlicesProp, new GUIContent("V-Slices", "V-Slices is equivalent to something like the columns in a table. Increasing this increases the number of vertices, as vertices = U-Slices * V-Slices"));
                //Display size
                EditorGUILayout.PropertyField(sizeProp, new GUIContent("Size", "Size/Scale of the model, without modifying the transform scale."));
                //Display u
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(uProp, new GUIContent("U"));
                DisplayPiButtons(uProp);
                EditorGUILayout.EndHorizontal();
                //Display v
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(vProp, new GUIContent("V"));
                DisplayPiButtons(vProp);
                EditorGUILayout.EndHorizontal();

                if (meshTypeProp.enumValueIndex != -1)
                {
                    //Display additional vars
                    for (int i = 0; i < mg.CurrentSurface.parameters; i++)
                    {
                        EditorGUILayout.PropertyField(varsProp.GetArrayElementAtIndex(i), new GUIContent(varNames[i]));
                    }
                }
                if (GUILayout.Button(new GUIContent("Generate Mesh", "Regenerate the mesh using the current values. Should be unncessary if Auto Update is enabled.")))
                {
                    mg.GenerateMesh();
                }
                if (GUILayout.Button(new GUIContent("Reset To Defaults", "Reset the U, V and additional parameters to their defaults.")))
                {
                    mg.ResetToDefaults();
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        private void DisplayPiButtons(SerializedProperty prop)
        {
            if (GUILayout.Button(pi, GUILayout.Width(30f)))
            {
                prop.vector2Value = new Vector2(prop.vector2Value.x, Mathf.PI);
            }
            if (GUILayout.Button("2" + pi, GUILayout.Width(30f)))
            {
                prop.vector2Value = new Vector2(prop.vector2Value.x, 2 * Mathf.PI);
            }
            if (GUILayout.Button("4" + pi, GUILayout.Width(30f)))
            {
                prop.vector2Value = new Vector2(prop.vector2Value.x, 4 * Mathf.PI);
            }
        }
        
        private void DisplayColorPickers()
        {
            EditorGUILayout.PropertyField(gradientProp);
        }
        private void IndentedSerializedProperties(SerializedProperty[] props)
        {
            EditorGUI.indentLevel++;
            foreach (SerializedProperty prop in props)
            {
                EditorGUILayout.PropertyField(prop);
            }
            EditorGUI.indentLevel--;
        }
        private void IndentedSerializedProperties(SerializedProperty prop)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(prop);
            EditorGUI.indentLevel--;
        }
    }
}
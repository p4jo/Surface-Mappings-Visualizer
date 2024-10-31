using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MathMesh
{
    [CustomEditor(typeof(MeshVFXManager))]
    public class MeshVFXEditor : Editor
    {
        //VFX Graph
        MeshVFXManager vfxManager;
        SerializedProperty spawnRateProp;
        SerializedProperty pullForceProp;
        SerializedProperty matchVertexCountProp;
        SerializedProperty colorProp;
        SerializedProperty texProp;
        SerializedProperty voxelOutlineProp;
        private void OnEnable()
        {
            vfxManager = serializedObject.targetObject as MeshVFXManager;
            //VFX Graph
            spawnRateProp = serializedObject.FindProperty("spawnRate");
            pullForceProp = serializedObject.FindProperty("pullForce");
            matchVertexCountProp = serializedObject.FindProperty("matchVertexCount");
            colorProp = serializedObject.FindProperty("color");
            texProp = serializedObject.FindProperty("mainTex");
            voxelOutlineProp = serializedObject.FindProperty("voxelOutline");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            if(DisplayVFXProperties() && GUILayout.Button("Reset Visual Effect")){
                vfxManager.ResetVisualEffect();
            }
            serializedObject.ApplyModifiedProperties();
        }
        private bool DisplayVFXProperties()
        {
            if(vfxManager.effect == null){
                EditorGUILayout.HelpBox("No visual effect asset", MessageType.Warning);
                return false; 
            }
            else if(vfxManager.effect.visualEffectAsset == null){
                return false;
            }

            string assetName = vfxManager.effect.visualEffectAsset.name;
            if(assetName != "Wireframe"){
                EditorGUILayout.PropertyField(spawnRateProp);
                EditorGUILayout.PropertyField(matchVertexCountProp);
                EditorGUILayout.PropertyField(pullForceProp);
                EditorGUILayout.PropertyField(texProp);
            }
            if(assetName != "Voxel"){
                EditorGUILayout.PropertyField(colorProp);
            }
            return true;
        }
    }
}
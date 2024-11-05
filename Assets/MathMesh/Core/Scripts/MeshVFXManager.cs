using UnityEngine;
using UnityEngine.VFX;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MathMesh{
    [ExecuteAlways]
    [RequireComponent(typeof(VisualEffect))]
    public class MeshVFXManager : MonoBehaviour
    {
        //VFX vars
        [HideInInspector]
        public VisualEffect effect;
        public Vector3 pullForce;
        public int spawnRate;
        public bool matchVertexCount = true;
        private Vector3 position;
        [GradientUsageAttribute(true)]
        public Gradient color = new Gradient();
        private Vector3 size;
        public Texture2D mainTex;
        private string prevAssetName;
        // Start is called before the first frame update
        public void ApplyVFX(Mesh mesh)
        {
            //VFX stuff
            effect = GetComponent<VisualEffect>();
            if(effect != null){
                size = mesh.bounds.size;
                SetEffectData(); 
            }
        }

        private void SetEffectData(bool HardReset = false)
        {
            //Check for procedural mesh component
            if(TryGetComponent<MeshGenerator>(out MeshGenerator gen)){
                if(gen.mesh == null){ return; }
                if (effect.HasMesh("mesh")) { effect.SetMesh("mesh", gen.mesh); }
                if(gen.doubleSided){
                    if (effect.HasInt("VertexCount")) { effect.SetInt("VertexCount", gen.vertices.Length/2); }
                }
                else{
                    if (effect.HasInt("VertexCount")) { effect.SetInt("VertexCount", gen.vertices.Length); }
                }

                if (effect.HasInt("uSlices")) { effect.SetInt("uSlices", gen.uSlices); }
                if (effect.HasInt("vSlices")) { effect.SetInt("vSlices", gen.vSlices); }
                if (matchVertexCount) { spawnRate = gen.uSlices * gen.vSlices; }

            }
            if (effect.HasVector3("position")) { effect.SetVector3("position", position); }
            if (effect.HasVector3("PullForce")) { effect.SetVector3("PullForce", pullForce); }
            if (effect.HasInt("ParticleSpawnRate")) { effect.SetInt("ParticleSpawnRate", spawnRate); }
            if (effect.HasGradient("gradColor")) { effect.SetGradient("gradColor", color); }
            if (effect.HasVector3("size")) { effect.SetVector3("size", size); }
            if (effect.HasTexture("MainTex") && mainTex != null) { effect.SetTexture("MainTex", mainTex); }
            
            if(effect.visualEffectAsset != null){
                if(effect.visualEffectAsset.name == "Wireframe" || HardReset){
                    effect.Reinit();
                }
            }
            
        }

        #if UNITY_EDITOR
        public void ResetVisualEffect(){
            if(effect == null){
                effect = GetComponent<VisualEffect>();
            }
            if(effect != null){
                if(effect.visualEffectAsset != null){
                    SetEffectData(true);
                }
            }
        }
        void _OnValidate() {
            if(this == null) {
                return;
            }
            if(effect == null){
                effect = GetComponent<VisualEffect>();
            }
            if(effect != null){
                SetEffectData();
            }
        }

        private void OnValidate()
        {
            EditorApplication.delayCall += _OnValidate;
        }

        #endif
    }
}

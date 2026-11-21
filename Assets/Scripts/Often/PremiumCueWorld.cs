using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Often
{
    public sealed class PremiumCueWorld:MonoBehaviour
    {
        Transform standard;
        Bounds bounds;
        Texture2D artwork;
        PremiumCueStyle style;
        Mesh body,effects;
        Material bodyMaterial,effectMaterial;
        readonly CueEffectGeometry geometry=new CueEffectGeometry();
        readonly List<Vector3> positions=new List<Vector3>(5000);
        float charge,flash=-100;
        Camera cameraView;
        float visualTime;Vector3 visualNormal;Vector3? replayNormal;
        [System.Serializable] public sealed class ReplayState {public Bounds bounds;public PremiumCueStyle style;public float time,charge,flash;public Vector3 normal;}
        public Texture2D ReplayArtwork=>artwork;
        public ReplayState CaptureReplayState()=>new ReplayState{bounds=bounds,style=style,time=visualTime,charge=charge,flash=flash,normal=visualNormal};
        public static PremiumCueWorld CreateReplay(GameObject node,Texture2D artwork,ReplayState state)
        {
            var cue=node.AddComponent<PremiumCueWorld>();cue.enabled=false;cue.standard=node.transform.parent;cue.bounds=state.bounds;cue.style=state.style;cue.artwork=artwork;cue.CreateVisuals();return cue;
        }
        public void ApplyReplayState(ReplayState state){bounds=state.bounds;style=state.style;charge=state.charge;flash=state.flash;replayNormal=state.normal;UpdateGeometry(state.time);}
        public void SetCharge(float value){charge=Mathf.Clamp01(value);}
        public void Strike(){flash=Time.time;charge=0;}
        public static PremiumCueWorld Equip(Transform slider,string key)
        {
            var style=PremiumCueDesign.Style(key);var standard=slider?slider.Find("Cue3D/Standard3DCue"):null;
            var image=Resources.Load<Texture2D>(key);var shader=Resources.Load<Shader>("Gameplay/PremiumCues/PremiumCue");
            if(style==PremiumCueStyle.None || !standard || !image || !shader)return null;
            var node=new GameObject("PremiumCue");node.layer=standard.gameObject.layer;node.transform.SetParent(standard,false);
            var cue=node.AddComponent<PremiumCueWorld>();cue.standard=standard;cue.bounds=standard.GetComponent<MeshFilter>().sharedMesh.bounds;cue.artwork=image;cue.style=style;
            cue.CreateVisuals();
            standard.GetComponent<MeshRenderer>().enabled=false;cue.UpdateGeometry(0);return cue;
        }
        void CreateVisuals()
        {
            var shader=Resources.Load<Shader>("Gameplay/PremiumCues/PremiumCue");
            body=new Mesh{name="Premium cue body"};effects=new Mesh{name="Premium cue animated effects"};effects.MarkDynamic();body.MarkDynamic();
            bodyMaterial=new Material(shader){mainTexture=artwork};effectMaterial=new Material(shader);effectMaterial.SetFloat("_DstBlend",1);effectMaterial.renderQueue=3001;
            Renderer("IllustratedBody",body,bodyMaterial);Renderer("MovingEffects",effects,effectMaterial);
        }
        void Renderer(string name,Mesh mesh,Material material)
        {
            var node=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));node.layer=gameObject.layer;node.transform.SetParent(transform,false);node.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        public void Remove(){gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
        void LateUpdate(){UpdateGeometry(Time.time);}
        public void UpdateGeometry(float time)
        {
            if(!standard || !body)return;
            if(!cameraView)cameraView=Camera.main;
            else if(!cameraView.isActiveAndEnabled){var main=Camera.main;if(main)cameraView=main;}
            var axis=standard.TransformVector(Vector3.back*bounds.size.z);float length=axis.magnitude;
            visualTime=time;var normal=replayNormal??(cameraView?cameraView.transform.forward:Vector3.down);visualNormal=normal;
            var side=Vector3.Cross(normal,axis.normalized).normalized;if(side.sqrMagnitude<.5f)side=standard.right;
            var origin=standard.TransformPoint(new Vector3(bounds.center.x,bounds.center.y,bounds.max.z));
            float half=artwork.height/(float)artwork.width*.5f;
            Vector3 Map(Vector3 p)=>standard.InverseTransformPoint(origin+axis*p.x+side*(p.y*length));
            body.vertices=new[]{Map(new Vector3(0,-half)),Map(new Vector3(1,-half)),Map(new Vector3(1,half)),Map(new Vector3(0,half))};
            body.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};body.colors=new[]{Color.white,Color.white,Color.white,Color.white};body.triangles=new[]{0,1,2,0,2,3};body.RecalculateBounds();
            float strength=Mathf.Clamp01(.25f+charge*.45f+Mathf.Max(0,1-(time-flash)*4)*.4f);
            geometry.Build(style,time,strength);positions.Clear();foreach(var p in geometry.Vertices)positions.Add(Map(p));
            effects.Clear();effects.SetVertices(positions);effects.SetColors(geometry.Colors);effects.SetTriangles(geometry.Indices,0);effects.RecalculateBounds();
        }
        void OnDestroy()
        {
            Release(body);Release(effects);Release(bodyMaterial);Release(effectMaterial);
        }
        static void Release(Object o){if(!o)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    // A read-only canvas. It deliberately creates no Button, Animator, particle
    // simulation, gameplay component, payment call or message/network sender.
    public sealed class MatchReplayDisplay : IDisposable
    {
        readonly MatchReplayPresentation data;
        readonly GameObject root;
        readonly RectTransform stage;
        readonly List<Texture2D> textures=new List<Texture2D>();
        readonly List<Material> materials=new List<Material>();
        readonly List<RecordedMatchGraphic> pool=new List<RecordedMatchGraphic>();
        readonly Dictionary<int,RecordedMatchGraphic.Geometry> meshes=new Dictionary<int,RecordedMatchGraphic.Geometry>();
        readonly Dictionary<int,byte[]> rawMeshes=new Dictionary<int,byte[]>();
        long rawBytes,geometryBytes;
        readonly Dictionary<Graphic,bool> suppressed=new Dictionary<Graphic,bool>();
        readonly Dictionary<Renderer,bool> worldSuppressed=new Dictionary<Renderer,bool>();
        readonly Transform controls;
        readonly MatchReplayDisplayHost host;
        public int Frame {get;private set;}=-1;
        public MatchReplayDisplay(Transform parent,MatchReplayPresentation data,Transform controls=null)
        {
            this.data=data;this.controls=controls;root=new GameObject("Recorded match presentation",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(parent,false);
            host=root.AddComponent<MatchReplayDisplayHost>();host.display=this;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            canvas.overrideSorting=true;
            canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2|AdditionalCanvasShaderChannels.TexCoord3;
            stage=new GameObject("Recorded viewport",typeof(RectTransform)).GetComponent<RectTransform>();stage.SetParent(root.transform,false);
            stage.anchorMin=stage.anchorMax=Vector2.one*.5f;stage.pivot=Vector2.zero;stage.sizeDelta=new Vector2(data.width,data.height);
        }
        Texture2D Texture(int id)
        {
            if(id<0)return null;if(id>=data.textures.Count)throw new InvalidDataException("Invalid replay texture");
            while(textures.Count<=id)textures.Add(null);if(textures[id])return textures[id];var bytes=data.textures[id];
            if(bytes.Length<24||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)throw new InvalidDataException("Invalid replay PNG");
            int Width(int offset)=>(bytes[offset]<<24)|(bytes[offset+1]<<16)|(bytes[offset+2]<<8)|bytes[offset+3];
            if(Width(16)<1||Width(16)>8192||Width(20)<1||Width(20)>8192||(long)Width(16)*Width(20)>33554432)throw new InvalidDataException("Replay texture expansion limit");
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
            if(!texture.LoadImage(bytes,true)){Release(texture);throw new InvalidDataException("Cannot decode replay texture");}textures[id]=texture;return texture;
        }
        Material Material(int id)
        {
            while(materials.Count<=id)materials.Add(null);if(materials[id])return materials[id];
            using(var r=new BinaryReader(new MemoryStream(data.materials[id]))){
                string name=r.ReadString();var shader=Shader.Find(name);if(!shader)shader=Shader.Find("UI/Default");
                var m=new Material(shader);m.hideFlags=HideFlags.DontSave;
                int count=r.ReadInt32();if(count<0||count>256)throw new InvalidDataException();
                var keys=new string[count];for(int i=0;i<count;i++)keys[i]=r.ReadString();m.shaderKeywords=keys;
                int main=r.ReadInt32();count=r.ReadInt32();if(count<0||count>1024)throw new InvalidDataException();
                for(int i=0;i<count;i++){
                    string property=r.ReadString();var type=(ShaderPropertyType)r.ReadInt32();bool has=m.HasProperty(property);
                    if(type==ShaderPropertyType.Texture){var texture=Texture(r.ReadInt32());var scale=new Vector2(r.ReadSingle(),r.ReadSingle());var offset=new Vector2(r.ReadSingle(),r.ReadSingle());if(has){m.SetTexture(property,texture);m.SetTextureScale(property,scale);m.SetTextureOffset(property,offset);}}
                    else if(type==ShaderPropertyType.Color||type==ShaderPropertyType.Vector){var v=new Vector4(Finite(r),Finite(r),Finite(r),Finite(r));if(has)m.SetVector(property,v);}
                    else {float value=Finite(r);if(has)m.SetFloat(property,value);}
                }
                bool clipped=r.ReadBoolean();var clipRect=new Vector4(Finite(r),Finite(r),Finite(r),Finite(r));
                m.SetVector("_ClipRect",clipRect);
                if(clipped)m.EnableKeyword("UNITY_UI_CLIP_RECT");else m.DisableKeyword("UNITY_UI_CLIP_RECT");
                if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",Texture(main));
                materials[id]=m;return m;
            }
        }
        static float Finite(BinaryReader r){float value=r.ReadSingle();if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Invalid replay geometry");return value;}
        RecordedMatchGraphic.Geometry Geometry(int id)
        {
            if(meshes.TryGetValue(id,out var geometry))return geometry;
            using(var r=new BinaryReader(new MemoryStream(RawMesh(id,0)))){
                int count=r.ReadInt32();if(count<0||count>65535)throw new InvalidDataException();
                var vertices=new UIVertex[count];
                for(int i=0;i<count;i++){
                    var v=UIVertex.simpleVert;v.position=new Vector3(Finite(r),Finite(r),0);v.color=new Color32(r.ReadByte(),r.ReadByte(),r.ReadByte(),r.ReadByte());
                    Vector4 UV()=>new Vector4(Finite(r),Finite(r),Finite(r),Finite(r));
                    v.uv0=UV();v.uv1=UV();v.uv2=UV();v.uv3=UV();vertices[i]=v;
                }
                int size=r.ReadInt32();if(size<0||size>400000||size%3!=0)throw new InvalidDataException();var triangles=new int[size];
                for(int i=0;i<size;i++){triangles[i]=r.ReadInt32();if(triangles[i]<0||triangles[i]>=count)throw new InvalidDataException();}
                if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException();
                geometry=new RecordedMatchGraphic.Geometry{vertices=vertices,triangles=triangles};meshes.Add(id,geometry);geometryBytes+=vertices.Length*128L+triangles.Length*4L;
                while(geometryBytes>64*1024*1024&&meshes.Count>1){int key=meshes.Keys.First();var old=meshes[key];geometryBytes-=old.vertices.Length*128L+old.triangles.Length*4L;meshes.Remove(key);}return geometry;
            }
        }
        byte[] RawMesh(int id,int depth)
        {
            if(depth>32||id<0||id>=data.meshes.Count)throw new InvalidDataException("Replay geometry chain limit");
            if(rawMeshes.TryGetValue(id,out var raw))return raw;
            raw=MatchReplayMeshCodec.Decode(data.meshes[id],id,baseId=>RawMesh(baseId,depth+1));rawMeshes[id]=raw;rawBytes+=raw.Length;
            while(rawBytes>32*1024*1024&&rawMeshes.Count>1){int key=rawMeshes.Keys.First();rawBytes-=rawMeshes[key].Length;rawMeshes.Remove(key);}return raw;
        }
        public void Apply(int index)
        {
            if(index<0||index>=data.frames.Count)throw new ArgumentOutOfRangeException(nameof(index));
            Canvas.ForceUpdateCanvases();
            var viewport=root.GetComponent<Canvas>().pixelRect;
            float scale=Mathf.Min((viewport.width>0?viewport.width:Screen.width)/data.width,(viewport.height>0?viewport.height:Screen.height)/data.height);
            stage.localScale=Vector3.one*scale;stage.anchoredPosition=-new Vector2(data.width,data.height)*scale*.5f;
            int[] frame=data.frames[index];
            while(pool.Count<frame.Length){
                var g=new GameObject("Recorded visual",typeof(RectTransform),typeof(RecordedMatchGraphic)).GetComponent<RecordedMatchGraphic>();
                g.transform.SetParent(stage,false);g.rectTransform.anchorMin=g.rectTransform.anchorMax=g.rectTransform.pivot=Vector2.zero;g.rectTransform.sizeDelta=stage.sizeDelta;g.rectTransform.anchoredPosition=Vector2.zero;g.raycastTarget=false;pool.Add(g);
            }
            for(int i=0;i<pool.Count;i++){
                var g=pool[i];g.gameObject.SetActive(i<frame.Length);if(i>=frame.Length)continue;
                var draw=data.draws[frame[i]];g.Set(Geometry(draw.mesh),Material(draw.material));
            }
            Frame=index;
            SuppressLive();
        }
        internal void SuppressLive()
        {
            foreach(var g in root.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Graphic>(true))){
                if(g.gameObject.scene!=root.scene||g.transform.IsChildOf(root.transform)||controls&&g.transform.IsChildOf(controls))continue;
                if(!suppressed.ContainsKey(g))suppressed.Add(g,g.enabled);g.enabled=false;
            }
            foreach(var r in root.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Renderer>(true))){
                if(r.gameObject.scene!=root.scene||!MatchReplayVisualCapture.IsWorldEffect(r))continue;
                if(!worldSuppressed.ContainsKey(r))worldSuppressed.Add(r,r.enabled);r.enabled=false;
            }
        }
        public static void Release(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        public void Dispose(){host.display=null;foreach(var pair in suppressed)if(pair.Key)pair.Key.enabled=pair.Value;foreach(var pair in worldSuppressed)if(pair.Key)pair.Key.enabled=pair.Value;suppressed.Clear();worldSuppressed.Clear();Release(root);foreach(var m in materials)Release(m);foreach(var t in textures)Release(t);meshes.Clear();rawMeshes.Clear();}
    }
    [DefaultExecutionOrder(32700)]
    public sealed class MatchReplayDisplayHost : MonoBehaviour
    { internal MatchReplayDisplay display;void LateUpdate()=>display?.SuppressLive(); }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecordedMatchGraphic : MaskableGraphic
    {
        public sealed class Geometry { public UIVertex[] vertices; public int[] triangles; }
        Geometry geometry;Texture recordedTexture;
        public override Texture mainTexture=>recordedTexture?recordedTexture:material&&material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):Texture2D.whiteTexture;
        public void SetTexture(Texture value){if(recordedTexture==value)return;recordedTexture=value;SetMaterialDirty();}
        public void Set(Geometry value,Material appearance){if(geometry!=value){geometry=value;SetVerticesDirty();}if(material!=appearance){material=appearance;SetMaterialDirty();}}
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();if(geometry==null)return;foreach(var v in geometry.vertices)vh.AddVert(v);for(int i=0;i<geometry.triangles.Length;i+=3)vh.AddTriangle(geometry.triangles[i],geometry.triangles[i+1],geometry.triangles[i+2]);}
    }
}

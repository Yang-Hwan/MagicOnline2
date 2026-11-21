using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    // Records resolved display geometry, including the exact glyphs, portrait
    // frames, coins and particle poses, rather than rerunning game/random logic.
    public sealed class MatchReplayVisualCapture : IDisposable
    {
        readonly MatchReplayPresentation data;
        readonly Dictionary<string,int> textureKeys=new Dictionary<string,int>(),materialKeys=new Dictionary<string,int>(),meshKeys=new Dictionary<string,int>(),drawKeys=new Dictionary<string,int>();
        readonly Dictionary<Texture,int> textures=new Dictionary<Texture,int>();
        readonly Dictionary<TMP_Text,string> textValues=new Dictionary<TMP_Text,string>();
        readonly Dictionary<Mesh,Mesh> readableMeshes=new Dictionary<Mesh,Mesh>();
        readonly Dictionary<(Material,Texture,bool,Rect),int> frameMaterials=new Dictionary<(Material,Texture,bool,Rect),int>();
        readonly Dictionary<(Transform,int),(byte[] raw,int id)> previousMeshes=new Dictionary<(Transform,int),(byte[],int)>();
        readonly List<int> meshDepth=new List<int>();
        readonly Mesh scratch=new Mesh();
        readonly SHA256 hash=SHA256.Create();
        readonly List<int> frame=new List<int>();
        readonly List<Vector3> positions=new List<Vector3>();
        readonly List<Color32> colors=new List<Color32>();
        readonly List<Vector4>[] uv={new List<Vector4>(),new List<Vector4>(),new List<Vector4>(),new List<Vector4>()};
        long bytes;
        public MatchReplayVisualCapture(MatchReplayPresentation data){this.data=data;data.width=Mathf.Max(1,Screen.width);data.height=Mathf.Max(1,Screen.height);data.applicationVersion=Application.version;}
        string Key(byte[] blob)=>Convert.ToBase64String(hash.ComputeHash(blob));
        int Intern(byte[] blob,List<byte[]> list,Dictionary<string,int> keys)
        {
            string key=Key(blob);if(keys.TryGetValue(key,out int id))return id;
            bytes+=blob.Length;if(bytes>MatchReplayPresentation.MaxBytes)throw new InvalidDataException("시합 화면 기록이 최대 저장 용량을 초과했습니다.");
            id=list.Count;list.Add(blob);keys.Add(key,id);return id;
        }
        int InternMesh(byte[] raw,Transform transform,int sub)
        {
            string key=Key(raw);if(meshKeys.TryGetValue(key,out int found)){previousMeshes[(transform,sub)]=(raw,found);return found;}
            previousMeshes.TryGetValue((transform,sub),out var previous);
            bool hasBase=previous.raw!=null&&meshDepth[previous.id]<31;
            var encoded=MatchReplayMeshCodec.Encode(raw,hasBase?previous.raw:null,previous.id);
            bytes+=encoded.Length;if(bytes>MatchReplayPresentation.MaxBytes)throw new InvalidDataException("시합 화면 기록이 최대 저장 용량을 초과했습니다.");
            int id=data.meshes.Count;data.meshes.Add(encoded);meshKeys.Add(key,id);
            meshDepth.Add(MatchReplayMeshCodec.IsDelta(encoded)?meshDepth[previous.id]+1:0);previousMeshes[(transform,sub)]=(raw,id);return id;
        }
        int TextureId(Texture texture)
        {
            if(!texture)return -1;
            bool dynamic=texture is RenderTexture;
            if(!dynamic && textures.TryGetValue(texture,out int cached))return cached;
            if(texture.dimension!=TextureDimension.Tex2D)return -1;
            if(texture.width>8192||texture.height>8192||(long)texture.width*texture.height>33554432)throw new InvalidDataException("Replay texture too large");
            var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
            Texture2D copy=null;
            try{
                Graphics.Blit(texture,rt);RenderTexture.active=rt;
                copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();
                int id=Intern(copy.EncodeToPNG(),data.textures,textureKeys);if(!dynamic)textures[texture]=id;return id;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);MatchReplayDisplay.Release(copy);}
        }
        int MaterialId(Material material,Texture main,bool clip,Rect bounds)
        {
            var signature=(material,main,clip,bounds);if(frameMaterials.TryGetValue(signature,out int cached))return cached;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
                var shader=material?material.shader:Shader.Find("UI/Default");w.Write(shader?shader.name:"UI/Default");
                var keywords=material?material.shaderKeywords:Array.Empty<string>();
                keywords=keywords.Where(k=>k!="UNITY_UI_CLIP_RECT").Concat(clip?new[]{"UNITY_UI_CLIP_RECT"}:Array.Empty<string>()).ToArray();
                w.Write(keywords.Length);foreach(var k in keywords)w.Write(k);
                w.Write(TextureId(main));
                int count=shader?shader.GetPropertyCount():0;w.Write(count);
                for(int i=0;i<count;i++){
                    string name=shader.GetPropertyName(i);var type=shader.GetPropertyType(i);w.Write(name);w.Write((int)type);
                    if(type==ShaderPropertyType.Texture){w.Write(material?TextureId(material.GetTexture(name)):-1);var scale=material?material.GetTextureScale(name):Vector2.one;var offset=material?material.GetTextureOffset(name):Vector2.zero;w.Write(scale.x);w.Write(scale.y);w.Write(offset.x);w.Write(offset.y);}
                    else if(type==ShaderPropertyType.Color||type==ShaderPropertyType.Vector){Vector4 value=material?material.GetVector(name):Vector4.zero;if(name=="_ClipRect")value=clip?new Vector4(bounds.xMin,bounds.yMin,bounds.xMax,bounds.yMax):new Vector4(-32767,-32767,32767,32767);Vector(w,value);}
                    else w.Write(material?material.GetFloat(name):0);
                }
                w.Write(clip);Vector(w,clip?new Vector4(bounds.xMin,bounds.yMin,bounds.xMax,bounds.yMax):new Vector4(-32767,-32767,32767,32767));
                int id=Intern(s.ToArray(),data.materials,materialKeys);frameMaterials.Add(signature,id);return id;
            }
        }
        static void Vector(BinaryWriter w,Vector4 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}
        public static Texture MainTexture(Material material)
        {
            if(!material||!material.shader)return null;
            var shader=material.shader;
            // Material.mainTexture falls back to _MainTex even when a Shader
            // Graph has no such property, emitting an error on every frame.
            for(int i=0;i<shader.GetPropertyCount();i++)
                if(shader.GetPropertyType(i)==ShaderPropertyType.Texture&&
                   (shader.GetPropertyFlags(i)&ShaderPropertyFlags.MainTexture)!=0)
                    return material.GetTexture(shader.GetPropertyNameId(i));
            return material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;
        }
        Vector3 ScreenPoint(Vector3 world,Camera camera)
        {
            Vector3 p=camera?camera.WorldToScreenPoint(world):world;
            int width=camera&&camera.targetTexture?camera.targetTexture.width:Screen.width;
            int height=camera&&camera.targetTexture?camera.targetTexture.height:Screen.height;
            p.x*=data.width/(float)Mathf.Max(1,width);p.y*=data.height/(float)Mathf.Max(1,height);return p;
        }
        void MeshDraw(Mesh mesh,int sub,Transform transform,Camera camera,Material material,Texture main,Color tint,bool clip,Rect clipRect,bool normalizedWorld=false)
        {
            if(!mesh||mesh.vertexCount==0)return;
            mesh.GetVertices(positions);
            for(int c=0;c<4;c++)mesh.GetUVs(c,uv[c]);
            mesh.GetColors(colors);var indices=mesh.GetTriangles(sub);
            if(indices.Length==0)return;
            // Project the entire draw with managed matrix math. Calling two
            // Transform/Camera native APIs for every vertex dominated capture.
            var localToWorld=transform?transform.localToWorldMatrix:Matrix4x4.identity;
            var projection=camera?camera.projectionMatrix*camera.worldToCameraMatrix*localToWorld:localToWorld;
            var depth=camera?camera.worldToCameraMatrix*localToWorld:Matrix4x4.identity;
            var viewport=camera?camera.rect:new Rect(0,0,1,1);
            void Emit(int[] remap,int[] triangles){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
                int count=remap?.Length??positions.Count;w.Write(count);bool anyInFront=!camera;
                for(int vertex=0;vertex<count;vertex++){
                    int i=remap==null?vertex:remap[vertex];
                    Vector3 point;
                    if(camera){var p=positions[i];var projected=projection*new Vector4(p.x,p.y,p.z,1);float inv=1f/projected.w;
                        point=new Vector3((viewport.x+(projected.x*inv+1)*.5f*viewport.width)*data.width,(viewport.y+(projected.y*inv+1)*.5f*viewport.height)*data.height,-depth.MultiplyPoint3x4(p).z);}
                    else point=localToWorld.MultiplyPoint3x4(positions[i]);
                    anyInFront|=point.z>0;
                    w.Write(point.x);w.Write(point.y);
                    Color color=colors.Count==positions.Count?(Color)colors[i]:Color.white;color*=tint;
                    var color32=(Color32)color;w.Write(color32.r);w.Write(color32.g);w.Write(color32.b);w.Write(color32.a);
                    for(int c=0;c<4;c++)Vector(w,uv[c].Count==positions.Count?uv[c][i]:Vector4.zero);
                }
                if(!anyInFront)return;
                w.Write(triangles.Length);foreach(int index in triangles)w.Write(index);
                int meshId=InternMesh(s.ToArray(),transform,sub),materialId=MaterialId(material,main,clip,clipRect);
                string key=meshId+":"+materialId;
                if(!drawKeys.TryGetValue(key,out int id)){id=data.draws.Count;data.draws.Add(new MatchReplayPresentation.Draw{mesh=meshId,material=materialId});drawKeys.Add(key,id);}
                frame.Add(id);
            }}
            if(positions.Count<=65535)Emit(null,indices);
            else{
                var map=new Dictionary<int,int>();var remap=new List<int>();var triangles=new List<int>();
                for(int at=0;at<indices.Length;at+=3){
                    if(map.Count>60000){Emit(remap.ToArray(),triangles.ToArray());map.Clear();remap.Clear();triangles.Clear();}
                    for(int v=0;v<3;v++){int index=indices[at+v];if(!map.TryGetValue(index,out int local)){local=remap.Count;map.Add(index,local);remap.Add(index);}triangles.Add(local);}
                }
                if(triangles.Count>0)Emit(remap.ToArray(),triangles.ToArray());
            }
        }
        static bool Excluded(Transform t)
        {
            for(var p=t;p;p=p.parent)if(p.name=="MatchReplaySavePanel"||p.name=="Practice Replay Controls"||p.name=="Recorded match presentation")return true;
            return false;
        }
        Rect Clip(Transform t,Camera camera,out bool clipped)
        {
            var rect=new Rect(0,0,data.width,data.height);clipped=false;var corners=new Vector3[4];
            foreach(var mask in t.GetComponentsInParent<RectMask2D>()){
                if(!mask.isActiveAndEnabled)continue;mask.rectTransform.GetWorldCorners(corners);
                var a=ScreenPoint(corners[0],camera);var b=ScreenPoint(corners[2],camera);
                rect=Rect.MinMaxRect(Mathf.Max(rect.xMin,a.x),Mathf.Max(rect.yMin,a.y),Mathf.Min(rect.xMax,b.x),Mathf.Min(rect.yMax,b.y));clipped=true;
            }
            return rect;
        }
        float Alpha(Transform t)
        {
            float alpha=1;
            for(var p=t;p;p=p.parent){var groups=p.GetComponents<CanvasGroup>();bool stop=false;foreach(var g in groups)if(g.enabled){alpha*=g.alpha;stop|=g.ignoreParentGroups;}if(stop)break;}
            return alpha;
        }
        void Ui(Transform t,float time)
        {
            if(!t.gameObject.activeInHierarchy||Excluded(t))return;
            var g=t.GetComponent<Graphic>();var cr=g?g.canvasRenderer:null;Camera camera=null;Rect clip=default;bool clipped=false;
            if(g&&g.isActiveAndEnabled&&cr&&!cr.cull&&g.canvas){
                var canvas=g.canvas.rootCanvas;camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                var uiMesh=cr.GetMesh();clip=Clip(t,camera,out clipped);
                Color tint=cr.GetColor();tint.a*=Alpha(t);
                if(uiMesh)for(int sub=0;sub<uiMesh.subMeshCount;sub++){
                    var appearance=sub<cr.materialCount?cr.GetMaterial(sub):g.materialForRendering;
                    // TMP's root Graphic.mainTexture can be the white UI texture;
                    // its CanvasRenderer material owns the actual SDF font atlas.
                    var texture=g is TMP_Text&&appearance?MainTexture(appearance):g.mainTexture;
                    MeshDraw(uiMesh,sub,t,camera,appearance,texture,tint,clipped,clip);
                }
                if(g is TMP_Text text && (!textValues.TryGetValue(text,out string old)||old!=text.text)){
                    textValues[text]=text.text;data.events.Add(new MatchReplayPresentation.Event{time=time,kind="text",value=t.name+": "+text.text});
                }
            }
            for(int i=0;i<t.childCount;i++)Ui(t.GetChild(i),time);
            // Preserve stencil-mask exit draws after the mask's children. This
            // retains circular portraits and custom HUD silhouettes when seeking.
            if(g&&cr&&g.isActiveAndEnabled&&cr.hasPopInstruction){
                var uiMesh=cr.GetMesh();
                if(uiMesh)for(int i=0;i<cr.popMaterialCount;i++)MeshDraw(uiMesh,0,t,camera,cr.GetPopMaterial(i),g.mainTexture,Color.white,clipped,clip);
            }
        }
        void World(Camera camera,UnityEngine.SceneManagement.Scene scene)
        {
            if(!camera)return;
            var planes=GeometryUtility.CalculateFrustumPlanes(camera);
            var renderers=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Renderer>(false))
                .Where(r=>r.gameObject.scene==scene&&r.enabled&&r.gameObject.activeInHierarchy&&!Excluded(r.transform))
                .OrderBy(r=>SortingLayer.GetLayerValueFromID(r.sortingLayerID)).ThenBy(r=>r.sortingOrder);
            foreach(var renderer in renderers){
                if(!IsWorldEffect(renderer))continue;
                if((camera.cullingMask&(1<<renderer.gameObject.layer))==0||!GeometryUtility.TestPlanesAABB(planes,renderer.bounds))continue;
                if(renderer is ParticleSystemRenderer emptyParticles&&emptyParticles.TryGetComponent<ParticleSystem>(out var system)&&system.particleCount==0)continue;
                var materials=renderer.sharedMaterials;scratch.Clear();Mesh mesh=null;
                if(renderer is ParticleSystemRenderer particles){particles.BakeMesh(scratch,camera,false);mesh=scratch;}
                else if(renderer is SkinnedMeshRenderer skinned){skinned.BakeMesh(scratch);mesh=scratch;}
                else if(renderer is LineRenderer line){line.BakeMesh(scratch,camera,false);mesh=scratch;}
                else if(renderer is SpriteRenderer sprite && sprite.sprite){
                    var s=sprite.sprite;scratch.vertices=Array.ConvertAll(s.vertices,v=>new Vector3(sprite.flipX?-v.x:v.x,sprite.flipY?-v.y:v.y,0));scratch.uv=s.uv;scratch.triangles=Array.ConvertAll(s.triangles,i=>(int)i);mesh=scratch;
                }
                else if(renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;
                if(!mesh)continue;
                if(!mesh.isReadable){if(!readableMeshes.TryGetValue(mesh,out var readable)){readable=MatchReplayMeshReadback.Read(mesh);readableMeshes.Add(mesh,readable);}mesh=readable;}
                for(int sub=0;sub<mesh.subMeshCount;sub++){
                    var material=materials.Length>0?materials[Mathf.Min(sub,materials.Length-1)]:null;
                    var sprite=renderer as SpriteRenderer;
                    MeshDraw(mesh,sub,renderer.transform,camera,material,sprite&&sprite.sprite?sprite.sprite.texture:MainTexture(material),sprite?sprite.color:Color.white,false,default);
                }
            }
        }
        public static bool IsWorldEffect(Renderer renderer)
        {
            string path=renderer.name;for(var p=renderer.transform.parent;p;p=p.parent)path=p.name+"/"+path;
            return renderer is ParticleSystemRenderer||renderer.GetComponent<TMP_Text>()||renderer.GetComponentInParent<Canvas>(true)||path.IndexOf("Facilities",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("DetailTurn",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("MovStory",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Sentiment",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Effects",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("SupportLines",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("Cue",StringComparison.OrdinalIgnoreCase)>=0;
        }
        public void Capture(UnityEngine.SceneManagement.Scene scene,Camera camera,float time)
        {
            frame.Clear();frameMaterials.Clear();World(camera,scene);
            foreach(var canvas in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Canvas>(false)).Where(c=>c.isRootCanvas&&c.isActiveAndEnabled).OrderBy(c=>SortingLayer.GetLayerValueFromID(c.sortingLayerID)).ThenBy(c=>c.sortingOrder))Ui(canvas.transform,time);
            var ids=frame.ToArray();var previous=data.frames.Count>0?data.frames[data.frames.Count-1]:null;
            data.frames.Add(previous!=null&&previous.SequenceEqual(ids)?previous:ids);
            data.views.Add(MatchReplayPresentation.View.Capture(camera));
        }
        public void Dispose(){hash.Dispose();MatchReplayDisplay.Release(scratch);foreach(var mesh in readableMeshes.Values)MatchReplayDisplay.Release(mesh);readableMeshes.Clear();}
    }
}

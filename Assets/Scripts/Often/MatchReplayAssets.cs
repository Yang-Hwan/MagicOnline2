using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Build-time asset references: replays store stable IDs, never texture pixels.
    public sealed class MatchReplayAssets : ScriptableObject
    {
        public string[] ids=Array.Empty<string>();
        public string[] assetNames=Array.Empty<string>(),assetTypes=Array.Empty<string>(),groups=Array.Empty<string>();
        public int[] slots=Array.Empty<int>();
        public string[] particleKeys=Array.Empty<string>();
        public string particleResource="Gameplay/Replay/Particles";
        Dictionary<UnityEngine.Object,string> forward;
        Dictionary<string,int> reverse;
        Dictionary<string,List<int>> byName;
        static MatchReplayAssets instance;
        public static MatchReplayAssets Current=>instance?instance:instance=Resources.Load<MatchReplayAssets>("Gameplay/Replay/Assets");
        public void InvalidateCache(){forward=null;reverse=null;byName=null;}
        public Shader ResolveShader(string name)
        {
            Prepare();
            if(byName.TryGetValue(typeof(Shader).FullName+"|"+name,out var candidates))foreach(int at in candidates)if(Resolve(ids[at]) is Shader shader)return shader;
            return Shader.Find(name);
        }
        void Prepare(){if(forward!=null)return;forward=new Dictionary<UnityEngine.Object,string>();reverse=new Dictionary<string,int>();byName=new Dictionary<string,List<int>>();for(int i=0;i<ids.Length;i++){reverse[ids[i]]=i;string key=assetTypes[i]+"|"+assetNames[i];if(!byName.TryGetValue(key,out var list)){list=new List<int>();byName.Add(key,list);}list.Add(i);}}
        public string Id(UnityEngine.Object asset)
        {
            if(!asset)return "";Prepare();if(forward.TryGetValue(asset,out var id))return id;
            if(asset==Texture2D.whiteTexture)return "@white";
            if(asset is Mesh&&(asset.name=="Quad"||asset.name=="Cube"||asset.name=="Sphere"||asset.name=="Capsule"||asset.name=="Cylinder"||asset.name=="Plane"))return "@mesh:"+asset.name;
            string key=asset.GetType().FullName+"|"+asset.name.Replace(" (Instance)","");
            if(byName.TryGetValue(key,out var candidates)){
                if(candidates.Count==1){id=ids[candidates[0]];forward[asset]=id;return id;}
                foreach(int at in candidates)if(Resolve(ids[at])==asset){id=ids[at];forward[asset]=id;return id;}
            }
            throw new InvalidOperationException("리플레이 리소스 목록에 없는 에셋: "+asset.GetType().Name+" / "+asset.name);
        }
        public UnityEngine.Object Resolve(string id){if(string.IsNullOrEmpty(id))return null;if(id=="@white")return Texture2D.whiteTexture;if(id.StartsWith("@mesh:",StringComparison.Ordinal)){var builtin=Resources.GetBuiltinResource<Mesh>(id.Substring(6)+".fbx");if(builtin)return builtin;throw new InvalidOperationException("Built-in replay mesh missing: "+id);}Prepare();if(reverse.TryGetValue(id,out int at)){var reference=Resources.Load<ReplayAssetReference>(groups[at]);if(reference&&slots[at]<reference.objects.Length)return reference.objects[slots[at]];}throw new InvalidOperationException("리플레이 에셋을 찾을 수 없습니다: "+id);}
        static string Path(Transform t){string path=t.name.Replace("(Clone)","").Trim();for(var p=t.parent;p;p=p.parent)path=p.name.Replace("(Clone)","").Trim()+"/"+path;return path;}
        public static string ParticleKey(ParticleSystem system){var r=system.GetComponent<ParticleSystemRenderer>();return system.gameObject.scene.name+"\t"+Path(system.transform)+"|"+(r&&r.sharedMaterial?r.sharedMaterial.name.Replace(" (Instance)",""):"");}
        static string ParticleName(string name)=>name.Replace("_","").Replace("-","").Replace(" ","").ToUpperInvariant();
        public string FindParticleKey(ParticleSystem system){string key=ParticleKey(system);int exact=Array.IndexOf(particleKeys,key);if(exact>=0)return particleKeys[exact];string[] input=key.Split('\t');string[] path=input[1].Split('|')[0].Split('/');string material=input[1].Substring(input[1].LastIndexOf('|'));int best=-1,score=0,materialOnly=-1,materialMatches=0;for(int i=0;i<particleKeys.Length;i++){string[] candidate=particleKeys[i].Split('\t');if(candidate.Length!=2||!candidate[1].EndsWith(material,StringComparison.Ordinal))continue;materialOnly=i;materialMatches++;string[] parts=candidate[1].Split('|')[0].Split('/');int count=0;while(count<Math.Min(parts.Length,path.Length)&&ParticleName(parts[parts.Length-1-count])==ParticleName(path[path.Length-1-count]))count++;int weight=count*2+(candidate[0]==input[0]?1:0);if(count>0&&weight>score){best=i;score=weight;}}if(best>=0)return particleKeys[best];if(materialMatches==1)return particleKeys[materialOnly];throw new InvalidOperationException("리플레이 파티클 ID 누락: "+key);}
        public GameObject Particle(string key){int at=Array.IndexOf(particleKeys,key);if(at>=0){var prefab=Resources.Load<GameObject>(particleResource);if(prefab&&at<prefab.transform.childCount)return prefab.transform.GetChild(at).gameObject;}throw new InvalidOperationException("리플레이 파티클 템플릿 누락: "+key);}
    }
}

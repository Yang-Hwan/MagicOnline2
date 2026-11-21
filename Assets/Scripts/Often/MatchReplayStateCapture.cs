using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Assets.Scripts.Sight.Surface.Pavilion;

namespace Assets.Scripts.Often
{
    [Serializable] public sealed class ReplayVisualNode
    {
        public int id,parent=-1;public string name,kind,asset,texture,shader,particle;
        public int[] materials,subEmitters;public bool mask,maskGraphic,rectMask,particleBuffer,rectTransform,hasCanvas;public int sorting,layer,rendererLayer;
        public string settings;
        public ReplayCanvasSettings canvas;public ReplayTextSettings text;public ReplayImageSettings image;public ReplayBones bones;
        public ReplaySpriteSettings sprite;public bool sortingGroup;public int sortingLayer,groupOrder;
    }
    [Serializable] public struct ReplayVisualState : IEquatable<ReplayVisualState>
    {
        public int id,frame,sibling;public bool active,enabled;
        public Vector3 position,scale;public Quaternion rotation;
        public Vector2 anchorMin,anchorMax,pivot,size,anchored;
        public Color color,canvasTint;public float alpha,fill,particleTime;public uint seed;
        public bool emitting;public string text,asset,texture,geometry,material,particles;
        public Rect uv;
        public bool Equals(ReplayVisualState b)=>id==b.id&&sibling==b.sibling&&active==b.active&&enabled==b.enabled&&position==b.position&&scale==b.scale&&rotation==b.rotation&&anchorMin==b.anchorMin&&anchorMax==b.anchorMax&&pivot==b.pivot&&size==b.size&&anchored==b.anchored&&color==b.color&&canvasTint==b.canvasTint&&alpha==b.alpha&&fill==b.fill&&particleTime==b.particleTime&&seed==b.seed&&emitting==b.emitting&&text==b.text&&asset==b.asset&&texture==b.texture&&geometry==b.geometry&&material==b.material&&particles==b.particles&&uv==b.uv;
    }
    [Serializable] public sealed class ReplayLocalGeometry {public Vector3[] positions;public Color32[] colors;public Vector4[] uv0,uv1,uv2,uv3;public int[] indices;}
    [Serializable] public sealed class ReplayMaterialState {public string shader;public string[] names,textureIds;public Vector4[] values;public int[] types;public string[] keywords;}
    [Serializable] public sealed class ReplaySpriteAsset {public string id,texture;public Rect rect;public Vector2 pivot;public float pixelsPerUnit;public Vector4 border;}

    // Records property changes and effect clocks. No GPU readback, baking,
    // texture encoding, per-vertex projection, hashing or compression in a match.
    public sealed class MatchReplayStateCapture : IDisposable
    {
        public const string Format="state-events-2";
        sealed class Tracked
        {
            public Transform transform;public Graphic graphic;public Renderer renderer;public ParticleSystem particle;public CanvasGroup group;
            public ReplayVisualNode node;public ReplayVisualState previous;public bool hasState,dirty=true;
            public string geometry;public UnityEngine.Events.UnityAction callback;
            public MatchReplayHierarchyObserver observer;
            public ReplayLocalGeometry localGeometry;
            public ReplayElasticState elastic;public ReplayLine line;
            public Material[] appearances;public int[] appearanceIds;public string appearanceKey;
            public ParticleSystem.Particle[] particleScratch;public bool hadParticles;
            public ReplayParticleBuffer particleValue;
            public PremiumCueWorld premium;
            public PremiumCueWorld.ReplayState premiumState;
        }
        readonly MatchReplayPresentation data;
        readonly MatchReplayAssets assets;
        readonly Dictionary<Transform,Tracked> tracked=new Dictionary<Transform,Tracked>();
        readonly List<Tracked> ordered=new List<Tracked>();
        readonly Dictionary<Material,int> materialIds=new Dictionary<Material,int>();
        readonly List<Material> materials=new List<Material>();
        readonly List<string> materialValues=new List<string>();
        readonly List<ReplayMaterialState> materialStates=new List<ReplayMaterialState>();
        readonly HashSet<int> sampledMaterials=new HashSet<int>();
        readonly Dictionary<UnityEngine.Object,string> runtimeIds=new Dictionary<UnityEngine.Object,string>();
        readonly List<Texture> deferredTextures=new List<Texture>();
        bool completedAssets;float clock;
        int nextPart;
        readonly Dictionary<MatchHudView,string> hudParts=new Dictionary<MatchHudView,string>();
        readonly Dictionary<MatchHudView,(MatchHudView.ReplayVisual state,string id)> previousHuds=new Dictionary<MatchHudView,(MatchHudView.ReplayVisual,string)>();
        readonly HashSet<Transform> observed=new HashSet<Transform>(),dirtyRoots=new HashSet<Transform>();
        readonly List<MatchReplayHierarchyObserver> observers=new List<MatchReplayHierarchyObserver>();
        readonly List<GameObject> sceneRoots=new List<GameObject>();
        void HierarchyChanged(Transform parent)=>dirtyRoots.Add(parent);
        public double LastMilliseconds {get;private set;}
        public int ChangeCount {get;private set;}
        public MatchReplayStateCapture(MatchReplayPresentation data)
        {
            this.data=data;assets=MatchReplayAssets.Current;
            if(!assets)throw new InvalidOperationException("리플레이 에셋 목록을 생성한 뒤 빌드해 주세요.");
            data.presentationVersion=Format;data.width=Mathf.Max(1,Screen.width);data.height=Mathf.Max(1,Screen.height);data.applicationVersion=Application.version;
        }
        static bool Excluded(Transform t){for(var p=t;p;p=p.parent)if(p.name=="MatchReplaySavePanel"||p.name=="Practice Replay Controls"||p.name=="Recorded match presentation"||p.name=="Replay state presentation")return true;return false;}
        string Asset(UnityEngine.Object value)
        {
            if(!value)return "";if(runtimeIds.TryGetValue(value,out string known))return known;
            try{return assets.Id(value);}catch(InvalidOperationException){
                if(value is Sprite sprite){string id="@sprite:"+runtimeIds.Count;runtimeIds[value]=id;Emit(clock,"visual-sprite",new ReplaySpriteAsset{id=id,texture=Asset(sprite.texture),rect=sprite.rect,pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height),pixelsPerUnit=sprite.pixelsPerUnit,border=sprite.border});return id;}
                if(value is Texture texture){string id="@texture:"+deferredTextures.Count;runtimeIds[value]=id;deferredTextures.Add(texture);return id;}
                throw;
            }
        }
        public void CompleteAssets()
        {
            if(completedAssets)return;completedAssets=true;
            data.FlushPending();
            // Downloaded avatars/dynamic font atlases are copied only after the
            // match finishes. Authored textures are always referenced by ID.
            foreach(var texture in deferredTextures){
                if(!texture)throw new InvalidOperationException("Runtime replay texture was destroyed before save");
                var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);Texture2D copy=null;
                try{Graphics.Blit(texture,target);RenderTexture.active=target;copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();data.textures.Add(copy.EncodeToPNG());}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);MatchReplayDisplay.Release(copy);}
            }
        }
        int Material(Material m){if(!m)return -1;if(materialIds.TryGetValue(m,out int id)){sampledMaterials.Add(id);return id;}id=materials.Count;materialIds.Add(m,id);materials.Add(m);materialValues.Add(null);materialStates.Add(null);sampledMaterials.Add(id);return id;}
        Tracked Track(Transform t,float time)
        {
            if(tracked.TryGetValue(t,out var found))return found;
            int parent=t.parent?Track(t.parent,time).node.id:-1;
            var g=t.GetComponent<Graphic>();var renderer=t.GetComponent<Renderer>();var ps=t.GetComponent<ParticleSystem>();
            if(ps&&(!(renderer is ParticleSystemRenderer particleRenderer)||!particleRenderer.sharedMaterial||particleRenderer.renderMode==ParticleSystemRenderMode.None)){ps=null;renderer=null;}
            var node=new ReplayVisualNode{id=ordered.Count,parent=parent,name=t.name,kind="transform",layer=t.gameObject.layer,rectTransform=t is RectTransform};
            var item=new Tracked{transform=t,graphic=g,renderer=renderer,particle=ps,group=t.GetComponent<CanvasGroup>(),node=node};
            item.premium=t.GetComponent<PremiumCueWorld>();if(item.premium){node.kind="premium-cue";node.asset=Asset(item.premium.ReplayArtwork);}
            tracked.Add(t,item);ordered.Add(item);
            if(t.TryGetComponent<UnityEngine.Rendering.SortingGroup>(out var sorting)){node.sortingGroup=true;node.sortingLayer=sorting.sortingLayerID;node.groupOrder=sorting.sortingOrder;}
            if(t.TryGetComponent<Canvas>(out var canvas)){node.hasCanvas=true;node.kind="canvas";node.sorting=canvas.sortingOrder;node.canvas=new ReplayCanvasSettings{root=canvas.isRootCanvas,world=canvas.renderMode==RenderMode.WorldSpace,size=((RectTransform)t).rect.size,sortingLayer=canvas.sortingLayerID,overrideSorting=canvas.overrideSorting};}
            if(g){
                node.kind=g is TextMeshPro?"text-world":g is TMP_Text?"text":g is Image?"image":g is RawImage?"raw":g is ElasticControlGraphic?"elastic":g is MatchHudGraphic?"hud":g is MatchHudArtGraphic?"hud-art":g is MatchHudPortraitGlow?"glow":g is TechniqueSparkGraphic?"sparks":"graphic";
                var mask=t.GetComponent<Mask>();node.mask=mask;node.maskGraphic=mask&&mask.showMaskGraphic;node.rectMask=t.GetComponent<RectMask2D>();
                if(g is TMP_Text text){node.asset=Asset(text.font);node.text=new ReplayTextSettings{textSize=text.fontSize,style=(int)text.fontStyle,alignment=(int)text.alignment,auto=text.enableAutoSizing,min=text.fontSizeMin,max=text.fontSizeMax,wrap=(int)text.textWrappingMode,rich=text.richText,overflow=(int)text.overflowMode,margin=text.margin};}
                if(g is Image image)node.image=new ReplayImageSettings{type=(int)image.type,fillMethod=(int)image.fillMethod,fillOrigin=image.fillOrigin,clockwise=image.fillClockwise,preserve=image.preserveAspect,fillCenter=image.fillCenter,pixelsPerUnit=image.pixelsPerUnitMultiplier};
                node.materials=new[]{Material(g.material)};
                item.callback=()=>item.dirty=true;g.RegisterDirtyVerticesCallback(item.callback);g.RegisterDirtyMaterialCallback(item.callback);
            }
            else if(renderer){
                node.kind=ps?"particle":renderer is SpriteMask?"sprite-mask":renderer is SpriteRenderer?"sprite":renderer is LineRenderer?"line":renderer is SkinnedMeshRenderer?"skinned":"mesh";
                node.sorting=renderer.sortingOrder;node.rendererLayer=renderer.sortingLayerID;node.layer=renderer.gameObject.layer;
                item.appearances=renderer is SpriteMask?Array.Empty<Material>():renderer.sharedMaterials;node.materials=Array.ConvertAll(item.appearances,Material);item.appearanceIds=node.materials;item.appearanceKey="world:"+string.Join(",",node.materials);
                if(renderer is SpriteRenderer sprite)node.sprite=new ReplaySpriteSettings{interaction=(int)sprite.maskInteraction,drawMode=(int)sprite.drawMode,size=sprite.size,sortingLayer=sprite.sortingLayerID};
                if(renderer is SpriteMask mask)node.sprite=new ReplaySpriteSettings{customRange=mask.isCustomRangeActive,frontLayer=mask.frontSortingLayerID,frontOrder=mask.frontSortingOrder,backLayer=mask.backSortingLayerID,backOrder=mask.backSortingOrder};
                if(ps){node.particle=assets.FindParticleKey(ps);node.particleBuffer=true;}
                else if(renderer is SkinnedMeshRenderer skinned){node.asset=Asset(skinned.sharedMesh);node.bones=new ReplayBones{root=skinned.rootBone?Track(skinned.rootBone,time).node.id:-1,bones=Array.ConvertAll(skinned.bones,b=>b?Track(b,time).node.id:-1)};}
                else if(t.TryGetComponent<MeshFilter>(out var filter))node.asset=Asset(filter.sharedMesh);
            }
            Emit(time,"visual-node",node);return item;
        }
        void Discover(Scene scene,float time)
        {
            scene.GetRootGameObjects(sceneRoots);
            foreach(var root in sceneRoots)if(!observed.Contains(root.transform)&&!Excluded(root.transform))dirtyRoots.Add(root.transform);
            var pending=dirtyRoots.ToArray();dirtyRoots.Clear();
            foreach(var branch in pending){
                if(!branch||Excluded(branch))continue;
                bool nested=false;for(var p=branch.parent;p;p=p.parent)if(Array.IndexOf(pending,p)>=0){nested=true;break;}if(nested)continue;
                var root=branch.gameObject;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))if(observed.Add(t)){var observer=t.GetComponent<MatchReplayHierarchyObserver>()??t.gameObject.AddComponent<MatchReplayHierarchyObserver>();observer.Changed+=HierarchyChanged;observers.Add(observer);}
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))if(canvas.isRootCanvas&&!Excluded(canvas.transform)){
                    Track(canvas.transform,time);
                }
                foreach(var g in root.GetComponentsInChildren<Graphic>(true))if(!Excluded(g.transform))Track(g.transform,time);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(MatchReplayVisualCapture.IsWorldEffect(r)&&!Excluded(r.transform)){
                    var premium=r.GetComponentInParent<PremiumCueWorld>(true);if(premium){Track(premium.transform,time);continue;}
                    if(r is ParticleSystemRenderer particles&&(!particles.sharedMaterial||particles.renderMode==ParticleSystemRenderMode.None))continue;
                    Track(r.transform,time);
                }
            }
        }
        void Emit(float time,string kind,object value){data.Queue(time,kind,value);ChangeCount++;}
        string Part(float time,object value){string id="@part:"+nextPart++;Emit(time,"visual-part:"+id,value);return id;}
        ReplayLocalGeometry Geometry(Graphic g)
        {
            var m=g.canvasRenderer.GetMesh();if(!m)return null;
            var uv=new List<Vector4>[4];for(int i=0;i<4;i++){uv[i]=new List<Vector4>();m.GetUVs(i,uv[i]);}
            return new ReplayLocalGeometry{positions=m.vertices,colors=m.colors32,uv0=uv[0].ToArray(),uv1=uv[1].ToArray(),uv2=uv[2].ToArray(),uv3=uv[3].ToArray(),indices=m.triangles};
        }
        static bool SameGeometry(ReplayLocalGeometry a,ReplayLocalGeometry b)=>a!=null&&b!=null&&a.positions.SequenceEqual(b.positions)&&a.colors.SequenceEqual(b.colors)&&a.uv0.SequenceEqual(b.uv0)&&a.uv1.SequenceEqual(b.uv1)&&a.uv2.SequenceEqual(b.uv2)&&a.uv3.SequenceEqual(b.uv3)&&a.indices.SequenceEqual(b.indices);
        static bool SameHud(MatchHudView.ReplayVisual a,MatchHudView.ReplayVisual b){if(a==null||b==null||a.side!=b.side||a.ratio!=b.ratio||a.seconds!=b.seconds||a.blend!=b.blend||a.time!=b.time||a.timer!=b.timer||a.quick!=b.quick||a.levels!=b.levels||a.series!=b.series)return false;for(int i=0;i<2;i++){var x=a.counters[i];var y=b.counters[i];if(x.Target!=y.Target||x.Actual!=y.Actual||x.Desired!=y.Desired||x.Displayed!=y.Displayed||x.Moving!=y.Moving||x.FromScore!=y.FromScore||x.ToScore!=y.ToScore||x.Started!=y.Started)return false;}return true;}
        static bool SameLine(ReplayLine a,ReplayLine b)=>a!=null&&b!=null&&a.world==b.world&&a.width==b.width&&a.positions.SequenceEqual(b.positions)&&a.color.colorKeys.SequenceEqual(b.color.colorKeys)&&a.color.alphaKeys.SequenceEqual(b.color.alphaKeys)&&a.widthCurve.keys.SequenceEqual(b.widthCurve.keys);
        void CaptureMaterials(float time,int frame)
        {
            foreach(int id in sampledMaterials){
                var m=materials[id];if(!m)continue;var shader=m.shader;int n=shader.GetPropertyCount();bool changed=materialStates[id]==null;
                var state=materialStates[id];if(state==null||state.shader!=shader.name){changed=true;state=new ReplayMaterialState{shader=shader.name,names=new string[n],values=new Vector4[n],textureIds=new string[n],types=new int[n],keywords=m.shaderKeywords};for(int i=0;i<n;i++){state.names[i]=shader.GetPropertyName(i);state.types[i]=(int)shader.GetPropertyType(i);}materialStates[id]=state;}
                for(int i=0;i<n;i++){
                    string name=state.names[i];var type=(UnityEngine.Rendering.ShaderPropertyType)state.types[i];
                    var value=Vector4.zero;string texture=null;
                    if(type==UnityEngine.Rendering.ShaderPropertyType.Texture){texture=Asset(m.GetTexture(name));var s=m.GetTextureScale(name);var o=m.GetTextureOffset(name);value=new Vector4(s.x,s.y,o.x,o.y);}
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Color||type==UnityEngine.Rendering.ShaderPropertyType.Vector)value=m.GetVector(name);
                    else value=new Vector4(m.GetFloat(name),0,0,0);
                    changed|=state.values[i]!=value||state.textureIds[i]!=texture;state.values[i]=value;state.textureIds[i]=texture;
                }
                var keywords=m.shaderKeywords;if(!state.keywords.SequenceEqual(keywords)){changed=true;state.keywords=keywords;}
                if(!changed)continue;
                Emit(time,"visual-material:"+id+":"+frame,new ReplayMaterialState{shader=state.shader,names=state.names,types=state.types,values=(Vector4[])state.values.Clone(),textureIds=(string[])state.textureIds.Clone(),keywords=(string[])state.keywords.Clone()});
            }
        }
        public void Capture(Scene scene,Camera camera,float time)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();int frame=data.frames.Count;clock=time;sampledMaterials.Clear();hudParts.Clear();
            Discover(scene,time);
            double discoveryMs=watch.Elapsed.TotalMilliseconds;
            // Track() may append bones, hence use indexed traversal.
            for(int i=0;i<ordered.Count;i++){
                var item=ordered[i];var t=item.transform;
                long nodeStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                if(!t){if(item.hasState&&item.previous.active){var gone=item.previous;gone.active=false;gone.frame=frame;Emit(time,"visual-state",gone);item.previous=gone;}continue;}
                if(!t.gameObject.activeInHierarchy&&item.hasState&&!item.previous.active)continue;
                var s=new ReplayVisualState{id=item.node.id,frame=frame,active=t.gameObject.activeInHierarchy,sibling=t.GetSiblingIndex(),position=t.localPosition,rotation=t.localRotation,scale=t.localScale,alpha=item.group?item.group.alpha:1,enabled=true};
                if(t is RectTransform rect){s.anchorMin=rect.anchorMin;s.anchorMax=rect.anchorMax;s.pivot=rect.pivot;s.size=rect.sizeDelta;s.anchored=rect.anchoredPosition;}
                var g=item.graphic;
                if(item.premium){var value=item.premium.CaptureReplayState();var old=item.premiumState;if(old==null||old.time!=value.time||old.charge!=value.charge||old.flash!=value.flash||old.style!=value.style||old.normal!=value.normal||!old.bounds.Equals(value.bounds)){item.premiumState=value;item.geometry=Part(time,value);}s.geometry=item.geometry;}
                if(g){
                    s.enabled=g.enabled;s.color=g.color;s.canvasTint=g.canvasRenderer.GetColor();s.texture=Asset(g.mainTexture);s.material=Material(g is TMP_Text fontText?fontText.fontSharedMaterial:g.material).ToString();
                    if(g is TMP_Text text){s.text=text.text;s.asset=Asset(text.font);s.fill=text.fontSize;}
                    else if(g is Image image){s.asset=Asset(image.overrideSprite?image.overrideSprite:image.sprite);s.fill=image.fillAmount;}
                    else if(g is RawImage raw)s.uv=raw.uvRect;
                    else if(g is ElasticControlGraphic elastic){if(item.dirty||!item.hasState){var value=ReplayElasticState.Capture(elastic);if(!value.Same(item.elastic)){item.elastic=value;item.geometry=Part(time,value);}}s.geometry=item.geometry;}
                    else if(g is MatchHudGraphic||g is MatchHudArtGraphic){var view=g.GetComponentInParent<MatchHudView>();if(view){if(!hudParts.TryGetValue(view,out string id)){var value=view.CaptureReplayVisualState();previousHuds.TryGetValue(view,out var previous);id=SameHud(value,previous.state)?previous.id:Part(time,value);previousHuds[view]=(value,id);hudParts.Add(view,id);}s.geometry=id;}}
                    else if(g is MatchHudPortraitGlow glow)s.fill=glow.ReplayTime;
                    else if(g is TechniqueSparkGraphic sparks)s.fill=sparks.ReplayTime;
                    else {if(item.dirty||!item.hasState){var geometry=Geometry(g);if(geometry!=null&&!SameGeometry(geometry,item.localGeometry)){item.localGeometry=geometry;item.geometry=Part(time,geometry);}}s.geometry=item.geometry;}
                    item.dirty=false;
                }
                var r=item.renderer;
                if(r&&!g){s.enabled=r.enabled;if(s.active&&s.enabled&&!(r is SpriteMask)){var appearance=r.sharedMaterials;if(item.appearances==null||!item.appearances.SequenceEqual(appearance)){item.appearances=appearance;item.appearanceIds=Array.ConvertAll(appearance,Material);item.appearanceKey="world:"+string.Join(",",item.appearanceIds);}if(item.appearanceIds!=null)foreach(int id in item.appearanceIds)if(id>=0)sampledMaterials.Add(id);}s.material=item.appearanceKey;if(r is SpriteRenderer sprite){s.asset=Asset(sprite.sprite);s.color=sprite.color;s.uv=new Rect(sprite.flipX?1:0,sprite.flipY?1:0,0,0);}
                    if(r is SpriteMask mask){s.asset=Asset(mask.sprite);s.fill=mask.alphaCutoff;}
                    if(r is LineRenderer line){var p=new Vector3[line.positionCount];line.GetPositions(p);var value=new ReplayLine{positions=p,world=line.useWorldSpace,width=line.widthMultiplier,color=line.colorGradient,widthCurve=line.widthCurve};if(!SameLine(value,item.line)){item.line=value;item.geometry=Part(time,value);}s.geometry=item.geometry;}
                }
                if(item.particle){var ps=item.particle;s.particleTime=ps.time;s.seed=ps.randomSeed;s.emitting=ps.isEmitting;s.particles=item.previous.particles;if(s.active&&s.enabled&&(ps.particleCount>0||item.hadParticles)){var buffer=ReplayParticleBuffer.Capture(ps,ref item.particleScratch);if(!buffer.Same(item.particleValue)){item.particleValue=buffer;s.particles=Part(time,buffer);}item.hadParticles=buffer.count>0;}}
                if(!item.hasState||!s.Equals(item.previous)){Emit(time,"visual-state",s);item.previous=s;item.hasState=true;}
                if(frame>3){double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-nodeStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;if(ms>MatchReplayPerformanceProbe.HeavyNodeMilliseconds){MatchReplayPerformanceProbe.HeavyNodeMilliseconds=ms;MatchReplayPerformanceProbe.HeavyNode=t.name+" / "+item.node.kind;}}
            }
            double stateMs=watch.Elapsed.TotalMilliseconds-discoveryMs;
            CaptureMaterials(time,frame);
            if(frame>3){MatchReplayPerformanceProbe.Profiles++;MatchReplayPerformanceProbe.DiscoveryMilliseconds+=discoveryMs;MatchReplayPerformanceProbe.StateMilliseconds+=stateMs;MatchReplayPerformanceProbe.MaterialMilliseconds+=watch.Elapsed.TotalMilliseconds-stateMs-discoveryMs;}
            Emit(time,"visual-clock:"+frame,Time.time.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            data.frames.Add(Array.Empty<int>());data.views.Add(MatchReplayPresentation.View.Capture(camera));
            watch.Stop();LastMilliseconds=watch.Elapsed.TotalMilliseconds;
        }
        public void Dispose(){foreach(var item in ordered)if(item.graphic){item.graphic.UnregisterDirtyVerticesCallback(item.callback);item.graphic.UnregisterDirtyMaterialCallback(item.callback);}foreach(var observer in observers)if(observer)observer.Changed-=HierarchyChanged;observers.Clear();observed.Clear();dirtyRoots.Clear();tracked.Clear();ordered.Clear();}
    }
    [Serializable] public sealed class ReplayTextSettings {public float textSize,min,max;public int style,alignment,wrap,overflow;public bool auto,rich;public Vector4 margin;}
    [Serializable] public sealed class ReplayImageSettings {public int type,fillMethod,fillOrigin;public bool clockwise,preserve,fillCenter;public float pixelsPerUnit;}
    [Serializable] public sealed class ReplaySpriteSettings {public int interaction,drawMode,sortingLayer,frontLayer,frontOrder,backLayer,backOrder;public Vector2 size;public bool customRange;}
    [Serializable] public sealed class ReplayElasticState {
        public bool rubber,preserveEndCaps,domeSilhouette,roundedSilhouette,coverSilhouette;public float tension,sourceBottomBorder,sourceTopBorder,cornerRadius;public Rect uvRect;public Vector2 silhouetteEndRadii,sourcePixelSize;
        public static ReplayElasticState Capture(ElasticControlGraphic g)=>new ReplayElasticState{rubber=g.rubber,preserveEndCaps=g.preserveEndCaps,domeSilhouette=g.domeSilhouette,roundedSilhouette=g.roundedSilhouette,coverSilhouette=g.coverSilhouette,tension=g.tension,sourceBottomBorder=g.sourceBottomBorder,sourceTopBorder=g.sourceTopBorder,cornerRadius=g.cornerRadius,uvRect=g.uvRect,silhouetteEndRadii=g.silhouetteEndRadii,sourcePixelSize=g.sourcePixelSize};
        public bool Same(ReplayElasticState b)=>b!=null&&rubber==b.rubber&&preserveEndCaps==b.preserveEndCaps&&domeSilhouette==b.domeSilhouette&&roundedSilhouette==b.roundedSilhouette&&coverSilhouette==b.coverSilhouette&&tension==b.tension&&sourceBottomBorder==b.sourceBottomBorder&&sourceTopBorder==b.sourceTopBorder&&cornerRadius==b.cornerRadius&&uvRect==b.uvRect&&silhouetteEndRadii==b.silhouetteEndRadii&&sourcePixelSize==b.sourcePixelSize;
    }
    [Serializable] public sealed class ReplayCanvasSettings {public bool root,world,overrideSorting;public int sortingLayer;public Vector2 size;}
    [Serializable] public sealed class ReplayBones {public int root;public int[] bones;}
    [Serializable] public sealed class ReplayLine {public Vector3[] positions;public bool world;public float width;public Gradient color;public AnimationCurve widthCurve;}
}

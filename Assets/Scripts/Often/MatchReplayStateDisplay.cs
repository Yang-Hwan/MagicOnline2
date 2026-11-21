using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Assets.Scripts.Sight.Surface.Pavilion;

namespace Assets.Scripts.Often
{
    // Creates only render components. Recorded events cannot execute game logic.
    public sealed class MatchReplayStateDisplay : IDisposable
    {
        sealed class Item {public ReplayVisualNode node;public GameObject go;public Graphic graphic;public Renderer renderer;public ParticleSystem particle;public CanvasGroup group;public MatchHudView hud;public PremiumCueWorld premium;public ReplayVisualState state;public bool initialized;}
        struct Change {public int frame,id;public ReplayVisualState state;public ReplayMaterialState material;public bool clock;public float time;}
        readonly MatchReplayPresentation data;readonly MatchReplayAssets assets;readonly GameObject root;
        readonly Dictionary<int,Item> items=new Dictionary<int,Item>();readonly Dictionary<int,Material> materials=new Dictionary<int,Material>();
        readonly List<Change> changes=new List<Change>();readonly Dictionary<Behaviour,bool> hidden=new Dictionary<Behaviour,bool>();readonly Dictionary<Renderer,bool> hiddenRenderers=new Dictionary<Renderer,bool>();
        readonly Transform controls;int cursor,lastFrame=-1;
        float shaderTime;
        readonly Dictionary<string,Sprite> runtimeSprites=new Dictionary<string,Sprite>();readonly Dictionary<string,ReplaySpriteAsset> spriteAssets=new Dictionary<string,ReplaySpriteAsset>();readonly Dictionary<int,Texture2D> runtimeTextures=new Dictionary<int,Texture2D>();
        readonly Dictionary<string,string> parts=new Dictionary<string,string>();
        string Part(string value)=>value!=null&&value.StartsWith("@part:",StringComparison.Ordinal)?parts[value]:value;
        public Transform Root=>root.transform;
        public MatchReplayStateDisplay(Transform parent,MatchReplayPresentation data,Transform controls)
        {
            this.data=data;this.controls=controls;assets=MatchReplayAssets.Current;if(!assets)throw new InvalidOperationException("리플레이 에셋 목록이 없습니다.");
            root=new GameObject("Replay state presentation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,parent.gameObject.scene);
            foreach(var e in data.events){
                if(e.kind=="visual-sprite"){var sprite=JsonUtility.FromJson<ReplaySpriteAsset>(e.value);spriteAssets[sprite.id]=sprite;}
                else if(e.kind.StartsWith("visual-part:",StringComparison.Ordinal))parts[e.kind.Substring(12)]=e.value;
                if(e.kind=="visual-node"){var node=JsonUtility.FromJson<ReplayVisualNode>(e.value);if(items.ContainsKey(node.id))throw new InvalidOperationException("Duplicate replay visual node");items.Add(node.id,new Item{node=node});}
                else if(e.kind=="visual-state"){var s=JsonUtility.FromJson<ReplayVisualState>(e.value);changes.Add(new Change{frame=s.frame,id=s.id,state=s});}
                else if(e.kind.StartsWith("visual-material:",StringComparison.Ordinal)){var parts=e.kind.Split(':');changes.Add(new Change{frame=int.Parse(parts[2]),id=int.Parse(parts[1]),material=JsonUtility.FromJson<ReplayMaterialState>(e.value)});}
                else if(e.kind.StartsWith("visual-clock:",StringComparison.Ordinal))changes.Add(new Change{frame=int.Parse(e.kind.Substring(13)),clock=true,time=float.Parse(e.value,System.Globalization.CultureInfo.InvariantCulture)});
            }
            // Stable ordering preserves multiple state changes within one frame.
            var sorted=changes.OrderBy(c=>c.frame).ToArray();changes.Clear();changes.AddRange(sorted);
            foreach(var item in items.Values.OrderBy(v=>v.node.id))Create(item);
            foreach(var item in items.Values){var n=item.node;if(n.parent>=0&&items.TryGetValue(n.parent,out var parentItem))item.go.transform.SetParent(parentItem.particle?parentItem.particle.transform:parentItem.go.transform,false);else item.go.transform.SetParent(root.transform,false);}
            foreach(var item in items.Values)if(item.graphic is MatchHudArtGraphic art)art.Initialize(item.hud);
            foreach(var item in items.Values)if(item.particle&&!item.node.particleBuffer&&item.node.subEmitters!=null){var sub=item.particle.subEmitters;for(int i=0;i<item.node.subEmitters.Length;i++){int id=item.node.subEmitters[i];if(id>=0&&items.TryGetValue(id,out var emitter)&&emitter.particle)sub.SetSubEmitterSystem(i,emitter.particle);}}
            foreach(var item in items.Values)if(item.node.kind=="skinned"&&item.renderer is SkinnedMeshRenderer skin){var bones=item.node.bones??JsonUtility.FromJson<ReplayBones>(item.node.settings);skin.bones=Array.ConvertAll(bones.bones,id=>id>=0&&items.ContainsKey(id)?items[id].go.transform:null);skin.rootBone=bones.root>=0?items[bones.root].go.transform:null;}
            Suppress();
        }
        UnityEngine.Object Asset(string id){
            if(string.IsNullOrEmpty(id))return null;
            if(id.StartsWith("@texture:",StringComparison.Ordinal)){int at=int.Parse(id.Substring(9));if(at<0||at>=data.textures.Count)throw new InvalidOperationException("Missing runtime replay texture");if(!runtimeTextures.TryGetValue(at,out var texture)){texture=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!texture.LoadImage(data.textures[at]))throw new InvalidOperationException("Invalid runtime replay texture");runtimeTextures.Add(at,texture);}return texture;}
            if(id.StartsWith("@sprite:",StringComparison.Ordinal)){if(!runtimeSprites.TryGetValue(id,out var sprite)){var description=spriteAssets[id];sprite=Sprite.Create((Texture2D)Asset(description.texture),description.rect,description.pivot,description.pixelsPerUnit,0,SpriteMeshType.FullRect,description.border);runtimeSprites.Add(id,sprite);}return sprite;}
            return assets.Resolve(id);
        }
        void Create(Item item)
        {
            var n=item.node;bool ui=n.rectTransform||n.kind=="canvas"||n.kind=="text"||n.kind=="text-world"||n.kind=="image"||n.kind=="raw"||n.kind=="graphic"||n.kind=="elastic"||n.kind=="hud"||n.kind=="hud-art"||n.kind=="glow"||n.kind=="sparks";
            item.go=ui?new GameObject(n.name,typeof(RectTransform)):new GameObject(n.name);item.go.SetActive(false);item.go.layer=n.layer;
            if(n.sortingGroup){var group=item.go.AddComponent<SortingGroup>();group.sortingLayerID=n.sortingLayer;group.sortingOrder=n.groupOrder;}
            if(n.kind=="canvas"||n.hasCanvas){
                var settings=n.canvas??JsonUtility.FromJson<ReplayCanvasSettings>(n.settings);
                var canvas=item.go.AddComponent<Canvas>();canvas.renderMode=settings.world?RenderMode.WorldSpace:RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=settings.overrideSorting;canvas.sortingOrder=n.sorting;canvas.sortingLayerID=settings.sortingLayer;canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2|AdditionalCanvasShaderChannels.TexCoord3;
                if(settings.root&&!settings.world){var scaler=item.go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=settings.size;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;}
            }
            if(n.kind=="text"||n.kind=="text-world"){
                TMP_Text text=n.kind=="text-world"?(TMP_Text)item.go.AddComponent<TextMeshPro>():item.go.AddComponent<TextMeshProUGUI>();text.font=Asset(n.asset) as TMP_FontAsset;
                var s=n.text??JsonUtility.FromJson<ReplayTextSettings>(n.settings);text.fontSize=s.textSize;text.fontStyle=(FontStyles)s.style;text.alignment=(TextAlignmentOptions)s.alignment;text.enableAutoSizing=s.auto;text.fontSizeMin=s.min;text.fontSizeMax=s.max;text.textWrappingMode=(TextWrappingModes)s.wrap;text.richText=s.rich;text.overflowMode=(TextOverflowModes)s.overflow;text.margin=s.margin;item.graphic=text;
            }
            else if(n.kind=="image"){var image=item.go.AddComponent<Image>();if(!string.IsNullOrEmpty(n.settings))JsonUtility.FromJsonOverwrite(n.settings,image);if(n.image!=null){var s=n.image;image.type=(Image.Type)s.type;image.fillMethod=(Image.FillMethod)s.fillMethod;image.fillOrigin=s.fillOrigin;image.fillClockwise=s.clockwise;image.preserveAspect=s.preserve;image.fillCenter=s.fillCenter;image.pixelsPerUnitMultiplier=s.pixelsPerUnit;}image.sprite=null;image.overrideSprite=null;image.material=null;item.graphic=image;}
            else if(n.kind=="raw"){var image=item.go.AddComponent<RawImage>();if(!string.IsNullOrEmpty(n.settings))JsonUtility.FromJsonOverwrite(n.settings,image);image.material=null;image.texture=null;item.graphic=image;}
            else if(n.kind=="graphic")item.graphic=item.go.AddComponent<RecordedMatchGraphic>();
            else if(n.kind=="elastic")item.graphic=item.go.AddComponent<ElasticControlGraphic>();
            else if(n.kind=="glow")item.graphic=item.go.AddComponent<MatchHudPortraitGlow>();
            else if(n.kind=="sparks")item.graphic=item.go.AddComponent<TechniqueSparkGraphic>();
            else if(n.kind=="hud"||n.kind=="hud-art"){
                item.hud=item.go.AddComponent<MatchHudView>();item.hud.enabled=false;
                if(n.kind=="hud"){var graphic=item.go.AddComponent<MatchHudGraphic>();graphic.Initialize(item.hud);item.graphic=graphic;}
                else item.graphic=item.go.AddComponent<MatchHudArtGraphic>();
            }
            else if(n.kind=="particle"){
                var template=assets.Particle(n.particle);var visual=UnityEngine.Object.Instantiate(template,item.go.transform,false);visual.layer=n.layer;visual.SetActive(true);item.particle=visual.GetComponent<ParticleSystem>();item.renderer=visual.GetComponent<ParticleSystemRenderer>();item.particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=item.particle.main;main.playOnAwake=false;main.useUnscaledTime=false;main.stopAction=ParticleSystemStopAction.None;var collision=item.particle.collision;collision.sendCollisionMessages=false;collision.colliderForce=0;
            }
            else if(n.kind=="sprite"){var sprite=item.go.AddComponent<SpriteRenderer>();if(n.sprite!=null){sprite.maskInteraction=(SpriteMaskInteraction)n.sprite.interaction;sprite.drawMode=(SpriteDrawMode)n.sprite.drawMode;sprite.size=n.sprite.size;sprite.sortingLayerID=n.sprite.sortingLayer;}item.renderer=sprite;}
            else if(n.kind=="sprite-mask"){var mask=item.go.AddComponent<SpriteMask>();if(n.sprite!=null){mask.isCustomRangeActive=n.sprite.customRange;mask.frontSortingLayerID=n.sprite.frontLayer;mask.frontSortingOrder=n.sprite.frontOrder;mask.backSortingLayerID=n.sprite.backLayer;mask.backSortingOrder=n.sprite.backOrder;}item.renderer=mask;}
            else if(n.kind=="line")item.renderer=item.go.AddComponent<LineRenderer>();
            else if(n.kind=="skinned"){var skin=item.go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=Asset(n.asset) as Mesh;item.renderer=skin;}
            else if(n.kind=="mesh"){item.go.AddComponent<MeshFilter>().sharedMesh=Asset(n.asset) as Mesh;item.renderer=item.go.AddComponent<MeshRenderer>();}
            if(item.renderer){item.renderer.sortingOrder=n.sorting;item.renderer.sortingLayerID=n.rendererLayer;}
            if(item.graphic){item.graphic.raycastTarget=false;if(item.graphic is MaskableGraphic safe)safe.onCullStateChanged=new MaskableGraphic.CullStateChangedEvent();if(n.mask)item.go.AddComponent<Mask>().showMaskGraphic=n.maskGraphic;if(n.rectMask)item.go.AddComponent<RectMask2D>();}
            item.group=item.go.AddComponent<CanvasGroup>();item.group.blocksRaycasts=false;item.group.interactable=false;
        }
        void Material(int id,ReplayMaterialState state)
        {
            if(!materials.TryGetValue(id,out var m)){var shader=Shader.Find(state.shader);if(!shader)throw new InvalidOperationException("Replay shader missing: "+state.shader);m=new Material(shader);materials.Add(id,m);}
            m.shaderKeywords=state.keywords;
            for(int i=0;i<state.names.Length;i++){
                string name=state.names[i];if(!m.HasProperty(name))continue;var type=(ShaderPropertyType)state.types[i];var value=state.values[i];
                if(type==ShaderPropertyType.Texture){m.SetTexture(name,Asset(state.textureIds[i]) as Texture);m.SetTextureScale(name,new Vector2(value.x,value.y));m.SetTextureOffset(name,new Vector2(value.z,value.w));}
                else if(type==ShaderPropertyType.Color||type==ShaderPropertyType.Vector)m.SetVector(name,value);else m.SetFloat(name,value.x);
            }
        }
        void State(Item item,ReplayVisualState s,bool seeking)
        {
            var t=item.go.transform;var old=item.state;item.go.SetActive(s.active);t.localPosition=s.position;t.localRotation=s.rotation;t.localScale=s.scale;
            var canvasSettings=item.node.canvas??(item.node.kind=="canvas"?JsonUtility.FromJson<ReplayCanvasSettings>(item.node.settings):null);
            bool rootCanvas=canvasSettings!=null&&canvasSettings.root&&!canvasSettings.world;
            if(t is RectTransform rect&&!rootCanvas){rect.anchorMin=s.anchorMin;rect.anchorMax=s.anchorMax;rect.pivot=s.pivot;rect.sizeDelta=s.size;rect.anchoredPosition=s.anchored;}
            if(rootCanvas){t.localScale=Vector3.one;}
            if(t.parent)t.SetSiblingIndex(Mathf.Clamp(s.sibling,0,t.parent.childCount-1));item.group.alpha=s.alpha;
            if(item.node.kind=="premium-cue"&&!string.IsNullOrEmpty(s.geometry)){var state=JsonUtility.FromJson<PremiumCueWorld.ReplayState>(Part(s.geometry));if(!item.premium)item.premium=PremiumCueWorld.CreateReplay(item.go,Asset(item.node.asset) as Texture2D,state);item.premium.ApplyReplayState(state);}
            var g=item.graphic;if(g){g.enabled=s.enabled;g.color=s.color;
                g.canvasRenderer.SetColor(data.presentationVersion=="state-events-1"?Color.white:s.canvasTint);
                if(g is TMP_Text text){text.text=s.text??"";text.font=Asset(s.asset) as TMP_FontAsset;text.fontSize=s.fill;}
                else if(g is Image image){image.sprite=Asset(s.asset) as Sprite;image.fillAmount=s.fill;}
                else if(g is RawImage raw){raw.texture=Asset(s.texture) as Texture;raw.uvRect=s.uv;}
                else if(g is ElasticControlGraphic elastic&&!string.IsNullOrEmpty(s.geometry)){JsonUtility.FromJsonOverwrite(Part(s.geometry),elastic);elastic.onCullStateChanged=new MaskableGraphic.CullStateChangedEvent();elastic.texture=Asset(s.texture) as Texture;elastic.raycastTarget=false;elastic.SetAllDirty();}
                else if(item.hud&&!string.IsNullOrEmpty(s.geometry)){item.hud.ApplyReplayVisual(Part(s.geometry));g.SetVerticesDirty();}
                else if(g is MatchHudPortraitGlow glow)glow.SetTime(s.fill);
                else if(g is TechniqueSparkGraphic sparks)sparks.SetTime(s.fill);
                else if(g is RecordedMatchGraphic recorded&&!string.IsNullOrEmpty(s.geometry)&&(!item.initialized||old.geometry!=s.geometry)){
                    var geo=JsonUtility.FromJson<ReplayLocalGeometry>(Part(s.geometry));var vertices=new UIVertex[geo.positions.Length];
                    for(int i=0;i<vertices.Length;i++){var v=UIVertex.simpleVert;v.position=geo.positions[i];if(geo.colors.Length==vertices.Length)v.color=geo.colors[i];if(geo.uv0.Length==vertices.Length)v.uv0=geo.uv0[i];if(geo.uv1.Length==vertices.Length)v.uv1=geo.uv1[i];if(geo.uv2.Length==vertices.Length)v.uv2=geo.uv2[i];if(geo.uv3.Length==vertices.Length)v.uv3=geo.uv3[i];vertices[i]=v;}
                    recorded.Set(new RecordedMatchGraphic.Geometry{vertices=vertices,triangles=geo.indices},recorded.material);
                }
            }
            if(item.renderer){item.renderer.enabled=s.enabled;
                if(item.renderer is SpriteRenderer sprite){sprite.sprite=Asset(s.asset) as Sprite;sprite.color=s.color;sprite.flipX=s.uv.x!=0;sprite.flipY=s.uv.y!=0;}
                if(item.renderer is SpriteMask mask){mask.sprite=Asset(s.asset) as Sprite;mask.alphaCutoff=s.fill;}
                if(item.renderer is LineRenderer line&&!string.IsNullOrEmpty(s.geometry)&&old.geometry!=s.geometry){var state=JsonUtility.FromJson<ReplayLine>(Part(s.geometry));line.useWorldSpace=state.world;line.positionCount=state.positions.Length;line.SetPositions(state.positions);line.widthMultiplier=state.width;line.widthCurve=state.widthCurve;line.colorGradient=state.color;}
            }
            if(item.particle&&item.node.particleBuffer){var ps=item.particle;if(s.active&&s.enabled){ps.useAutoRandomSeed=false;ps.randomSeed=s.seed;var emission=ps.emission;emission.enabled=false;var sub=ps.subEmitters;sub.enabled=false;ps.Play(false);ps.Pause(false);ps.time=s.particleTime;if(!string.IsNullOrEmpty(s.particles))ReplayParticleBuffer.Apply(ps,Part(s.particles));else ps.Clear(false);}else ps.Clear(false);}
            else if(item.particle&&s.active&&s.enabled){var ps=item.particle;bool restart=!item.initialized||!old.active||!old.enabled||old.seed!=s.seed||s.particleTime<old.particleTime;ps.useAutoRandomSeed=false;if(restart)ps.randomSeed=s.seed;var emission=ps.emission;emission.enabled=s.emitting;
                ps.Simulate(restart?s.particleTime:Mathf.Max(0,s.particleTime-old.particleTime),false,restart,true);ps.Pause(false);
            }
            item.state=s;item.initialized=true;
        }
        public void Apply(int frame)
        {
            bool seeking=frame<lastFrame||frame>lastFrame+1;
            if(frame<lastFrame){cursor=0;foreach(var item in items.Values){item.go.SetActive(false);item.initialized=false;}foreach(var m in materials.Values)MatchReplayDisplay.Release(m);materials.Clear();}
            while(cursor<changes.Count&&changes[cursor].frame<=frame){var change=changes[cursor++];if(change.clock)shaderTime=change.time;else if(change.material!=null)Material(change.id,change.material);else if(items.TryGetValue(change.id,out var item))State(item,change.state,seeking);}
            // Engine-owned clock uniforms reject material overrides. Visual
            // animations use their recorded clocks/particle state instead.
            foreach(var item in items.Values){
                if(item.node.materials==null)continue;var appearances=new List<Material>();foreach(int id in item.node.materials)if(id>=0&&materials.TryGetValue(id,out var material))appearances.Add(material);
                if(item.renderer&&item.state.material!=null&&item.state.material.StartsWith("world:",StringComparison.Ordinal)){appearances.Clear();foreach(string token in item.state.material.Substring(6).Split(','))if(int.TryParse(token,out int id)&&id>=0&&materials.TryGetValue(id,out var material))appearances.Add(material);}
                if(item.graphic&&int.TryParse(item.state.material,out int current)&&materials.TryGetValue(current,out var currentMaterial)){appearances.Clear();appearances.Add(currentMaterial);}
                if(item.renderer&&!(item.renderer is SpriteMask))item.renderer.sharedMaterials=appearances.ToArray();
                if(item.graphic&&appearances.Count>0){if(item.graphic is TMP_Text text)text.fontSharedMaterial=appearances[0];else item.graphic.material=appearances[0];}
                if(item.graphic is RecordedMatchGraphic recorded)recorded.SetTexture(Asset(item.state.texture) as Texture);
            }
            lastFrame=frame;Suppress();
        }
        public void Suppress()
        {
            foreach(var go in root.scene.GetRootGameObjects()){
                foreach(var g in go.GetComponentsInChildren<Graphic>(true)){if(g.transform.IsChildOf(root.transform)||controls&&g.transform.IsChildOf(controls))continue;if(!hidden.ContainsKey(g))hidden.Add(g,g.enabled);g.enabled=false;}
                foreach(var r in go.GetComponentsInChildren<Renderer>(true)){if(r.transform.IsChildOf(root.transform)||!MatchReplayVisualCapture.IsWorldEffect(r))continue;if(!hiddenRenderers.ContainsKey(r))hiddenRenderers.Add(r,r.enabled);r.enabled=false;}
            }
        }
        public void Dispose(){foreach(var pair in hidden)if(pair.Key)pair.Key.enabled=pair.Value;foreach(var pair in hiddenRenderers)if(pair.Key)pair.Key.enabled=pair.Value;MatchReplayDisplay.Release(root);foreach(var m in materials.Values)MatchReplayDisplay.Release(m);foreach(var s in runtimeSprites.Values)MatchReplayDisplay.Release(s);foreach(var t in runtimeTextures.Values)MatchReplayDisplay.Release(t);}
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad] public static class PracticeDemolitionValidation
{
    const string Request="Logs/practice-demolition.request";
    [Serializable] sealed class Report {public bool passed;public int checks;public string error;public double restoredDifference;}
    static PracticeDemolitionValidation(){EditorApplication.update+=()=>{if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(Request);EditorApplication.delayCall+=Run;};}
    [MenuItem("Tools/Magic Online/Validate s5_10 Demolition")]
    public static void Run()
    {
        var report=new Report();Scene scene=default;RenderTexture target=null;
        var configuration=typeof(PracticeSceneFlow).GetProperty("Configuration",BindingFlags.Static|BindingFlags.Public);
        var savedConfig=PracticeSceneFlow.Configuration;
        void Check(bool condition,string message){if(!condition)throw new Exception(message);report.checks++;}
        try{
            Directory.CreateDirectory("Logs/Demolition");
            scene=EditorSceneManager.OpenPreviewScene(PracticeSceneFlow.ScenePath);
            var table=scene.GetRootGameObjects().Single(g=>g.name=="Table").transform;
            var cloth=table.Find("ClothSpace");var actorRoot=table.Find("Addition/Sentiment");
            var actor=actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true).Single(a=>a.name=="s5_10");
            var originalMatrix=cloth.localToWorldMatrix;
            var bodies=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Rigidbody>(true)).ToArray();
            var bodyPositions=bodies.Select(b=>b.position).ToArray();var bodyRotations=bodies.Select(b=>b.rotation).ToArray();
            foreach(var canvas in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)))canvas.enabled=false;
            var cameraGo=new GameObject("Demolition validation camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera=cameraGo.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.enabled=false;camera.orthographic=true;camera.aspect=16f/9;camera.orthographicSize=cloth.TransformVector(Vector3.forward).magnitude*.68f;
            camera.transform.SetPositionAndRotation(cloth.position+Vector3.up*6,Quaternion.Euler(90,0,0));camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.03f);
            var lightGo=new GameObject("Demolition validation light",typeof(Light));SceneManager.MoveGameObjectToScene(lightGo,scene);lightGo.GetComponent<Light>().type=LightType.Directional;lightGo.GetComponent<Light>().intensity=1.2f;lightGo.transform.rotation=Quaternion.Euler(65,-15,0);
            target=new RenderTexture(1280,720,24);camera.targetTexture=target;
            Color32[] Render(string name){camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes("Logs/Demolition/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}}
            foreach(int count in new[]{3,4}){
                configuration.SetValue(null,MatchConfiguration.Practice(count));
                var owner=new GameObject("Demolition test controller",typeof(RectTransform),typeof(Canvas),typeof(PracticeSentimentController));SceneManager.MoveGameObjectToScene(owner,scene);owner.GetComponent<Canvas>().enabled=false;
                var controller=owner.GetComponent<PracticeSentimentController>();controller.Initialize(actorRoot);
                Check(actor.GetComponent<PracticeTableDemolition>(),count+"-ball selector configures only s5_10");
                Check(actorRoot.GetComponentsInChildren<PracticeTableDemolition>(true).Length==1,"Other emotions retain their existing presentation");
                var baseline=Render(count+"-before");controller.Play(actor,Vector3.zero,true);
                var effect=actor.GetComponent<PracticeTableDemolition>();
                effect.Overlay.SetCamera(camera);
                Check(effect.Overlay.Canvas.renderMode==RenderMode.ScreenSpaceOverlay&&effect.Overlay.Canvas.sortingOrder>109,"Hammer renders above HUD and cue controls");
                effect.Overlay.Canvas.renderMode=RenderMode.ScreenSpaceCamera;effect.Overlay.Canvas.worldCamera=camera;effect.Overlay.Canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                Check(!effect.Finished&&effect.VisualRoot.activeSelf,"Selection and play button path starts demolition");
                effect.Sample(.25f);Check(effect.VisibleCracks==0,"Windup does not reveal cracks before impact");Render(count+"-windup");
                var hammer=effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Demolition hammer");
                for(int strike=0;strike<3;strike++){
                    effect.Sample(PracticeTableDemolition.ImpactTime(strike)-PracticeTableDemolition.StrikeSeconds);
                    var raised=hammer.transform.up;effect.Sample(PracticeTableDemolition.ImpactTime(strike));
                    float swing=Vector3.Dot(hammer.transform.up-raised,cloth.right);
                    Check(strike==0?swing<-.5f:swing>.5f,"First view strikes left, angled right view strikes right: "+strike);
                    Check(strike==0?hammer.sprite.rect.x==0:hammer.sprite.rect.x>hammer.sprite.texture.width*.6f,"Side-on middle artwork is never used for a table strike");
                }
                Check(PracticeTableDemolition.StrikeSeconds<=.1f,"Downstroke takes at most 100 milliseconds");
                effect.Sample(PracticeTableDemolition.ImpactTime(0)+.13f);Check(effect.VisibleCracks==1,"First strike shows one fracture");Render(count+"-first");
                var chips=effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Slate chip")&&r.enabled).ToArray();
                Check(chips.Length==12&&chips.All(r=>AssetDatabase.GetAssetPath(r.sprite.texture).EndsWith("/ConcreteShards.png",StringComparison.Ordinal)&&r.color.r>=.89f),"Twelve concrete-textured fragments preserve their painted surface detail");
                Check(chips.Select(r=>r.sprite).Distinct().Count()==6,"Six different broken concrete images are mixed in each burst");
                var ranked=chips.OrderBy(r=>r.transform.localScale.x).ToArray();
                Check(ranked.Last().transform.localScale.x/ranked.First().transform.localScale.x>2.6f,"Fragment sizes span small, medium and large pieces");
                Check(chips.Max(r=>r.transform.localScale.y/r.transform.localScale.x)-chips.Min(r=>r.transform.localScale.y/r.transform.localScale.x)>.3f,"Fragment proportions vary as well as size");
                var earlier=chips.ToDictionary(r=>r,r=>r.transform.position);
                effect.Sample(PracticeTableDemolition.ImpactTime(0)+.17f);
                Vector3 Velocity(SpriteRenderer r){var delta=r.transform.position-earlier[r];delta.y=0;return delta/.04f;}
                float smallSpeed=ranked.Take(4).Average(r=>Velocity(r).magnitude),largeSpeed=ranked.Skip(8).Average(r=>Velocity(r).magnitude);
                Check(smallSpeed>largeSpeed*1.5f,"Small fragments fly faster than heavy large pieces");
                var meanDirection=chips.Aggregate(Vector3.zero,(sum,r)=>sum+Velocity(r).normalized)/chips.Length;
                Check(meanDirection.magnitude>.3f&&Vector3.Dot(meanDirection,-cloth.right)>.25f,"Fragment flight is clustered toward the strike, not an evenly spaced circle");
                effect.Sample(PracticeTableDemolition.ImpactTime(0)+.13f);
                effect.Sample(PracticeTableDemolition.ImpactTime(1)+.13f);Check(effect.VisibleCracks==2,"Second strike retains the first fracture");Render(count+"-second");
                effect.Sample(PracticeTableDemolition.ImpactTime(2)+.13f);Check(effect.VisibleCracks==3,"Three strikes accumulate distinct fractures");Render(count+"-third");
                Check(Vector3.Distance(effect.StrikePoints[0],effect.StrikePoints[1])>.4f&&Vector3.Distance(effect.StrikePoints[1],effect.StrikePoints[2])>.2f,"Hammer visits different table locations");
                Check(effect.VisualRoot.GetComponentsInChildren<Collider>(true).Length==0&&effect.VisualRoot.GetComponentsInChildren<Rigidbody>(true).Length==0,"Cosmetic effect has no collision or rigid body");
                effect.Sample(PracticeTableDemolition.Duration+.01f);Check(effect.Finished&&!effect.VisualRoot.activeSelf&&effect.VisibleCracks==0,"Natural completion removes every visual");
                var restored=Render(count+"-restored");long difference=0;for(int i=0;i<baseline.Length;i++)difference+=Math.Abs(baseline[i].r-restored[i].r)+Math.Abs(baseline[i].g-restored[i].g)+Math.Abs(baseline[i].b-restored[i].b);
                report.restoredDifference=Math.Max(report.restoredDifference,difference/(double)(baseline.Length*765));Check(report.restoredDifference<.001,"Original table pixels return after completion");
                controller.Play(actor,Vector3.zero,true);effect.Sample(PracticeTableDemolition.ImpactTime(1)+.13f);controller.Hide();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Switching selection or starting a new shot cancels all cracks");
                controller.Play(actor,Vector3.zero,true);effect.Sample(PracticeTableDemolition.ImpactTime(0)+.13f);Check(effect.VisibleCracks==1,"Repeated play starts a fresh sequence");actor.gameObject.SetActive(false);effect.Stop();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Actor disable leaves no orphan visuals");
                Check(cloth.localToWorldMatrix==originalMatrix&&bodies.Select((b,i)=>b.position==bodyPositions[i]&&b.rotation==bodyRotations[i]).All(same=>same),"Table and ball physics remain unchanged");
                effect.Overlay.Canvas.renderMode=RenderMode.ScreenSpaceOverlay;effect.Overlay.Canvas.worldCamera=null;
                controller.Hide();UnityEngine.Object.DestroyImmediate(owner);
            }
            var bombActor=actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true).Single(a=>a.name=="s1_4");
            foreach(int count in new[]{3,4}){
                configuration.SetValue(null,MatchConfiguration.Practice(count));
                var owner=new GameObject("Bomb validation controller",typeof(RectTransform),typeof(Canvas),typeof(PracticeSentimentController));SceneManager.MoveGameObjectToScene(owner,scene);owner.GetComponent<Canvas>().enabled=false;
                var controller=owner.GetComponent<PracticeSentimentController>();controller.Initialize(actorRoot);
                var baseline=Render(count+"-bomb-before");controller.Play(bombActor,Vector3.zero,true);
                var effect=bombActor.GetComponent<PracticeBombExplosion>();
                Check(effect&&!effect.Finished,"s1_4 selection starts local bomb presentation in "+count+" ball practice");
                SpriteRenderer Part(string name)=>effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);
                effect.Sample(.15f);var start=Part("Falling bomb").transform.position;Render(count+"-bomb-falling");
                Check(Part("Falling bomb").enabled&&!Part("Detonation flash").enabled&&!Part("Residual smoke 0").enabled,"Bomb drops before any explosion or smoke");
                Check(!Part("Falling bomb (2)").enabled,"Second bomb waits for its delayed drop");
                effect.Sample(.6f);Check(Vector3.Distance(start,Part("Falling bomb").transform.position)>.1f,"Bomb visibly approaches table");
                effect.Sample(PracticeBombExplosion.ImpactTime+.06f);Render(count+"-bomb-flash");
                Check(!Part("Falling bomb").enabled&&Part("Detonation flash").enabled&&Part("Shockwave").enabled,"Impact replaces bomb with flash and shockwave");
                var initialRing=Part("Shockwave").transform.localScale.x;
                effect.Sample(PracticeBombExplosion.ImpactTime+.30f);Render(count+"-bomb-fire");
                Check(Part("Shockwave").transform.localScale.x>initialRing&&Part("Fireball").enabled,"Shockwave expands while fireball burns");
                Check(effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.name.StartsWith("Casing shard")&&r.enabled)==18,"Eighteen metal casing fragments fly out");
                Check(Part("Falling bomb (2)").enabled&&!Part("Detonation flash (2)").enabled,"Second bomb falls after first detonation");
                effect.Sample(PracticeBombExplosion.ImpactTime+PracticeBombExplosion.BombInterval+.06f);Render(count+"-bomb-second");
                Check(!Part("Falling bomb (2)").enabled&&Part("Detonation flash (2)").enabled&&Part("Shockwave (2)").enabled,"Second bomb detonates independently");
                Check(Vector3.Distance(Part("Detonation flash").transform.position,Part("Detonation flash (2)").transform.position)>.2f,"Two bombs hit distinct table positions");
                effect.Sample(PracticeBombExplosion.ImpactTime+1.2f);Render(count+"-bomb-smoke");
                Check(!Part("Fireball").enabled&&!Part("Detonation flash").enabled&&Part("Residual smoke 0").enabled,"Only residual smoke remains after flames");
                var smokeAlpha=Part("Residual smoke 0").color.a;var smokeScale=Part("Residual smoke 0").transform.localScale.x;
                effect.Sample(PracticeBombExplosion.ImpactTime+1.6f);
                Check(Part("Residual smoke 0").color.a<smokeAlpha&&Part("Residual smoke 0").transform.localScale.x>smokeScale,"Smoke expands while steadily thinning out");
                effect.Sample(PracticeBombExplosion.Duration-.02f);
                Check(!effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.name.StartsWith("Residual smoke")&&r.enabled),"Both smoke clouds fully fade before sequence cleanup");
                effect.Sample(PracticeBombExplosion.Duration+.01f);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Explosion naturally finishes and hides all visuals");
                var restored=Render(count+"-bomb-restored");long difference=0;for(int i=0;i<baseline.Length;i++)difference+=Math.Abs(baseline[i].r-restored[i].r)+Math.Abs(baseline[i].g-restored[i].g)+Math.Abs(baseline[i].b-restored[i].b);
                Check(difference==0,"Explosion restores original table pixels exactly");
                controller.Play(bombActor,Vector3.zero,true);effect.Sample(.75f);controller.Hide();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Selection change cancels explosion immediately");
                controller.Play(bombActor,Vector3.zero,true);effect.Sample(.2f);Check(Part("Falling bomb").enabled&&!Part("Fireball").enabled,"Repeated play restarts with falling bomb");
                // Preview scenes do not dispatch MonoBehaviour lifecycle callbacks outside Play Mode.
                bombActor.gameObject.SetActive(false);typeof(PracticeBombExplosion).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Disable callback leaves no explosion behind");
                Check(cloth.localToWorldMatrix==originalMatrix&&bodies.Select((b,i)=>b.position==bodyPositions[i]&&b.rotation==bodyRotations[i]).All(same=>same),"Explosion preserves table and ball transforms");
                controller.Hide();UnityEngine.Object.DestroyImmediate(owner);
            }
            var stormActor=actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true).Single(a=>a.name=="s5_8");
            foreach(int count in new[]{3,4}){
                configuration.SetValue(null,MatchConfiguration.Practice(count));
                var owner=new GameObject("Storm validation controller",typeof(RectTransform),typeof(Canvas),typeof(PracticeSentimentController));SceneManager.MoveGameObjectToScene(owner,scene);owner.GetComponent<Canvas>().enabled=false;
                var controller=owner.GetComponent<PracticeSentimentController>();controller.Initialize(actorRoot);
                var baseline=Render(count+"-storm-before");controller.Play(stormActor,Vector3.zero,true);
                var effect=stormActor.GetComponent<PracticeTableStorm>();Check(effect&&!effect.Finished,"s5_8 selection starts storm in "+count+" ball practice");
                Check(effect.WeatherCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&effect.WeatherCanvas.sortingOrder>109,"Weather draws above the HUD and cue controls");
                var images=effect.WeatherCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                Check(images.All(r=>!r.raycastTarget)&&!effect.WeatherCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),"Fullscreen weather never intercepts UI input");
                // Camera.Render cannot capture overlay canvases; render the same images through the preview camera.
                effect.WeatherCanvas.renderMode=RenderMode.ScreenSpaceCamera;effect.WeatherCanvas.worldCamera=camera;effect.WeatherCanvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                SpriteRenderer Part(string name)=>effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);
                SpriteRenderer[] Parts(string prefix)=>effect.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith(prefix)).ToArray();
                UnityEngine.UI.Image ScreenPart(string name)=>images.Single(r=>r.name==name);
                UnityEngine.UI.Image[] ScreenParts(string prefix)=>images.Where(r=>r.name.StartsWith(prefix)).ToArray();
                effect.Sample(.3f);Render(count+"-storm-arriving");
                Check(ScreenPart("Storm cloud 0").enabled&&!ScreenParts("Rain streak").Any(r=>r.enabled)&&!Parts("Lightning ").Any(r=>r.enabled),"Clouds arrive before rain or lightning");
                var arriving=ScreenPart("Storm cloud 0").rectTransform.anchoredPosition;
                effect.Sample(1.15f);Render(count+"-storm-rain");
                Check(Vector2.Distance(arriving,ScreenPart("Storm cloud 0").rectTransform.anchoredPosition)>.1f,"Clouds move onto screen");
                Check(ScreenParts("Storm cloud").Length==5&&ScreenParts("Storm cloud").All(r=>r.rectTransform.anchoredPosition.y>0),"Only one upper row of clouds is created");
                var activeRain=ScreenParts("Rain streak").Where(r=>r.enabled).ToArray();
                Check(activeRain.Length>60&&Parts("Rain splash").Any(r=>r.enabled),"Heavy rain and water splashes cover table");
                var screenSize=((RectTransform)effect.WeatherCanvas.transform).rect.size;
                Check(activeRain.Max(r=>r.rectTransform.anchoredPosition.x)-activeRain.Min(r=>r.rectTransform.anchoredPosition.x)>screenSize.x*.8f&&activeRain.Max(r=>r.rectTransform.anchoredPosition.y)-activeRain.Min(r=>r.rectTransform.anchoredPosition.y)>screenSize.y*.8f,"Rain spans the full viewport width and height");
                Check(images.All(r=>!r.GetComponentInParent<UnityEngine.UI.Mask>()&&!r.GetComponentInParent<UnityEngine.UI.RectMask2D>()),"Weather is not clipped to the table or a HUD panel");
                var previous=ScreenPart("Rain streak 10").rectTransform.anchoredPosition;effect.Sample(1.17f);
                Check(Vector2.Distance(previous,ScreenPart("Rain streak 10").rectTransform.anchoredPosition)>.01f,"Rain streaks visibly fall");
                for(int strike=0;strike<3;strike++){
                    effect.Sample(new[]{1.38f,1.98f,2.68f}[strike]);Render(count+"-storm-lightning-"+strike);
                    Check(Part("Lightning "+strike).enabled&&Part("Lightning impact "+strike).enabled,"Each lightning strike and impact is visible: "+strike);
                }
                Check(Vector3.Distance(Part("Lightning impact 0").transform.position,Part("Lightning impact 1").transform.position)>.2f,"Lightning strikes distinct locations");
                effect.Sample(3.5f);float a=ScreenPart("Storm cloud 0").color.a;float rainAlpha=ScreenParts("Rain streak").Sum(r=>r.enabled?r.color.a:0);
                effect.Sample(4.15f);Render(count+"-storm-clearing");
                Check(ScreenPart("Storm cloud 0").color.a<a&&ScreenParts("Rain streak").Sum(r=>r.enabled?r.color.a:0)<rainAlpha,"Clouds and rain gradually fade");
                effect.Sample(PracticeTableStorm.Duration-.01f);
                Check(!ScreenParts("Storm cloud").Any(r=>r.enabled)&&!ScreenParts("Rain streak").Any(r=>r.enabled),"Weather has faded before final cleanup");
                effect.Sample(PracticeTableStorm.Duration+.01f);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Storm finishes naturally");
                var restored=Render(count+"-storm-restored");long difference=0;for(int i=0;i<baseline.Length;i++)difference+=Math.Abs(baseline[i].r-restored[i].r)+Math.Abs(baseline[i].g-restored[i].g)+Math.Abs(baseline[i].b-restored[i].b);
                Check(difference==0,"Storm restores original table pixels exactly");
                controller.Play(stormActor,Vector3.zero,true);effect.Sample(1.38f);controller.Hide();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Selection change cancels weather immediately");
                controller.Play(stormActor,Vector3.zero,true);effect.Sample(.3f);Check(!ScreenParts("Rain streak").Any(r=>r.enabled)&&!Parts("Lightning ").Any(r=>r.enabled),"Repeated storm restarts without leftover rain or lightning");
                stormActor.gameObject.SetActive(false);typeof(PracticeTableStorm).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Disable callback removes all storm visuals");
                Check(cloth.localToWorldMatrix==originalMatrix&&bodies.Select((b,i)=>b.position==bodyPositions[i]&&b.rotation==bodyRotations[i]).All(same=>same),"Storm preserves table and ball transforms");
                effect.WeatherCanvas.renderMode=RenderMode.ScreenSpaceOverlay;effect.WeatherCanvas.worldCamera=null;
                controller.Hide();UnityEngine.Object.DestroyImmediate(owner);
            }
            var skullActor=actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true).Single(a=>a.name=="s5_9");
            foreach(int count in new[]{3,4}){
                configuration.SetValue(null,MatchConfiguration.Practice(count));
                var owner=new GameObject("Skull validation controller",typeof(RectTransform),typeof(Canvas),typeof(PracticeSentimentController));SceneManager.MoveGameObjectToScene(owner,scene);owner.GetComponent<Canvas>().enabled=false;
                var controller=owner.GetComponent<PracticeSentimentController>();controller.Initialize(actorRoot);
                var cuePoint=cloth.TransformPoint(new Vector3(-.12f,0,-.1f));
                var baseline=Render(count+"-skull-before");controller.Play(skullActor,cuePoint,true);
                var effect=skullActor.GetComponent<PracticeSkullCurse>();Check(effect&&!effect.Finished,"s5_9 selection starts skull curse in "+count+" ball practice");
                effect.SetViewCamera(camera);
                Check(effect.OverlayCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&effect.OverlayCanvas.sortingOrder>109,"All skull effects render above balls, HUD and cue controls");
                Check(effect.OverlayCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).All(r=>!r.raycastTarget)&&!effect.OverlayCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),"Skull overlay does not intercept UI input");
                Check(!effect.VisualRoot.GetComponentInChildren<SpriteMask>(true),"Bones and particles are not clipped by the table boundary");
                effect.OverlayCanvas.renderMode=RenderMode.ScreenSpaceCamera;effect.OverlayCanvas.worldCamera=camera;effect.OverlayCanvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                UnityEngine.UI.Image Part(string name)=>effect.VisualRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(r=>r.name==name);
                UnityEngine.UI.Image[] Parts(string prefix)=>effect.VisualRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(r=>r.name.StartsWith(prefix)).ToArray();
                Check(Vector3.Distance(effect.CuePoint,cuePoint)<.02f,"Curse is anchored to requested cue point");
                effect.Sample(.4f);Render(count+"-skull-appearing");Check(Part("Curse skull").enabled&&!Parts("Falling bone").Any(r=>r.enabled),"Skull and mist appear before bones");
                effect.Sample(.8f);var raised=Part("Falling bone 0").transform.position;
                effect.Sample(1.0f);Render(count+"-skull-raining");Check(Vector3.Distance(raised,Part("Falling bone 0").transform.position)>.05f,"Bones visibly descend toward cloth");
                effect.Sample(1.6f);Check(Parts("Falling bone").Count(r=>r.enabled)==30&&Parts("Falling bone").Select(r=>r.sprite).Distinct().Count()==3,"Three waves contain thirty bones of three different shapes");
                Check(!Part("Final large bone").enabled,"Final large bone waits until after the shower");
                effect.Sample(PracticeSkullCurse.FinalImpact+.02f);Render(count+"-skull-final-impact");
                Check(Part("Final large bone").enabled&&Part("Bone impact 30").enabled,"Final large bone lands with an impact flash");
                Check(Part("Final large bone").rectTransform.sizeDelta.x>Parts("Falling bone").Max(r=>r.rectTransform.sizeDelta.x)*2,"Final bone is distinctly larger than shower bones");
                effect.Sample(3.2f);Render(count+"-skull-dissolving");Check(Parts("Dissolve mote").Count(r=>r.enabled)>20,"Purple motes accompany disappearing skull and bones");
                var skullAlpha=Part("Curse skull").color.a;effect.Sample(3.6f);Check(Part("Curse skull").color.a<skullAlpha,"Skull fades gradually rather than vanishing abruptly");
                effect.Sample(PracticeSkullCurse.Duration-.01f);Check(!effect.VisualRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(r=>r.enabled),"All curse sprites fade before final cleanup");
                effect.Sample(PracticeSkullCurse.Duration+.01f);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Curse naturally finishes");
                var restored=Render(count+"-skull-restored");long difference=0;for(int i=0;i<baseline.Length;i++)difference+=Math.Abs(baseline[i].r-restored[i].r)+Math.Abs(baseline[i].g-restored[i].g)+Math.Abs(baseline[i].b-restored[i].b);
                Check(difference==0,"Curse restores original table pixels exactly");
                controller.Play(skullActor,cuePoint,true);effect.Sample(1.6f);controller.Hide();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Selection change cancels curse immediately");
                controller.Play(skullActor,cuePoint,true);effect.Sample(.4f);Check(!Parts("Falling bone").Any(r=>r.enabled),"Repeated curse starts without leftover bones");
                skullActor.gameObject.SetActive(false);typeof(PracticeSkullCurse).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Disable callback clears skull curse");
                Check(cloth.localToWorldMatrix==originalMatrix&&bodies.Select((b,i)=>b.position==bodyPositions[i]&&b.rotation==bodyRotations[i]).All(same=>same),"Curse preserves table and ball transforms");
                effect.OverlayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;effect.OverlayCanvas.worldCamera=null;
                controller.Hide();UnityEngine.Object.DestroyImmediate(owner);
            }
            var tearActor=actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true).Single(a=>a.name=="s5_11");
            foreach(int count in new[]{3,4}){
                configuration.SetValue(null,MatchConfiguration.Practice(count));
                var owner=new GameObject("Tear fountain validation controller",typeof(RectTransform),typeof(Canvas),typeof(PracticeSentimentController));SceneManager.MoveGameObjectToScene(owner,scene);owner.GetComponent<Canvas>().enabled=false;
                var controller=owner.GetComponent<PracticeSentimentController>();controller.Initialize(actorRoot);
                var cue=cloth.TransformPoint(new Vector3(-.12f,0,-.1f));var baseline=Render(count+"-tears-before");controller.Play(tearActor,cue,true);
                var effect=tearActor.GetComponent<PracticeTearFountain>();Check(effect&&!effect.Finished,"s5_11 selector starts tear fountain in "+count+" ball practice");effect.SetViewCamera(camera);
                Check(Vector3.Distance(effect.CuePoint,cue)<.02f,"Fountain is centered around cue point");
                Check(effect.OverlayCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&effect.OverlayCanvas.sortingOrder==32767,"Tear fountain renders above HUD and cue controls");
                var images=effect.VisualRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true);Check(images.All(g=>!g.raycastTarget)&&!effect.OverlayCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),"Tears do not block UI input");
                effect.OverlayCanvas.renderMode=RenderMode.ScreenSpaceCamera;effect.OverlayCanvas.worldCamera=camera;effect.OverlayCanvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                UnityEngine.UI.Image Part(string name)=>images.Single(g=>g.name==name);
                effect.Sample(.4f);Render(count+"-tears-appearing");Check(Part("Crying face").enabled&&!Part("Tear jet 0").enabled,"Sad face appears before fountain");var initialFace=Part("Crying face").sprite;
                effect.Sample(1.15f);Render(count+"-tears-fountain");int visibleJets=images.Count(g=>g.name.StartsWith("Tear jet")&&g.enabled);Check(visibleJets>=2&&visibleJets<=4&&Part("Crying face").sprite!=initialFace,"Wailing face and two complete connected fountain sprites are visible");
                Check(Part("Tear jet 0").rectTransform.anchoredPosition.x<Part("Tear jet 2").rectTransform.anchoredPosition.x,"Two tear arcs separate to opposite sides");
                Check(images.Count(g=>g.name.StartsWith("Tear jet"))==4&&!Part("Tear splash 0").enabled,"Fountain and landing splash share sprites without separate disconnected images");
                effect.Sample(2.9f);Render(count+"-tears-large-drops");Check(Part("Final large tear 0").enabled&&Part("Final large tear 1").enabled,"Two final oversized tears fall after fountain");
                var start=Part("Final large tear 0").rectTransform.anchoredPosition;effect.Sample(3.05f);Check(Vector2.Distance(start,Part("Final large tear 0").rectTransform.anchoredPosition)>.05f,"Large tears visibly descend");
                effect.Sample(PracticeTearFountain.FinalImpact+.02f);Render(count+"-tears-final-splash");Check(!Part("Final large tear 0").enabled&&Part("Tear splash 0").enabled&&Part("Tear splash 1").enabled,"Final tears turn into broad splashes");
                float alpha=Part("Tear splash 0").color.a;var splashSize=Part("Tear splash 0").rectTransform.sizeDelta;effect.Sample(3.9f);Render(count+"-tears-clearing");Check(Part("Tear splash 0").color.a<alpha,"Remaining water and face gradually fade");
                Check(Vector2.Distance(splashSize,Part("Tear splash 0").rectTransform.sizeDelta)<.01f,"Final splash does not grow or zoom while fading");
                effect.Sample(PracticeTearFountain.Duration-.01f);Check(!images.Any(g=>g.enabled),"All tear artwork fades before cleanup");
                effect.Sample(PracticeTearFountain.Duration+.01f);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Tear fountain naturally finishes");
                var restored=Render(count+"-tears-restored");long difference=0;for(int i=0;i<baseline.Length;i++)difference+=Math.Abs(baseline[i].r-restored[i].r)+Math.Abs(baseline[i].g-restored[i].g)+Math.Abs(baseline[i].b-restored[i].b);Check(difference==0,"Tears restore original table pixels exactly");
                controller.Play(tearActor,cue,true);effect.Sample(1.2f);controller.Hide();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Selection change cancels tears immediately");
                controller.Play(tearActor,cue,true);effect.Sample(.4f);Check(!Part("Tear jet 0").enabled&&!Part("Final large tear 0").enabled,"Repeated tear animation starts cleanly");
                tearActor.gameObject.SetActive(false);typeof(PracticeTearFountain).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Disabled actor leaves no tears behind");
                Check(cloth.localToWorldMatrix==originalMatrix&&bodies.Select((b,i)=>b.position==bodyPositions[i]&&b.rotation==bodyRotations[i]).All(same=>same),"Tears preserve table and ball transforms");
                effect.OverlayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;effect.OverlayCanvas.worldCamera=null;controller.Hide();UnityEngine.Object.DestroyImmediate(owner);
            }
            var prideActor=actorRoot.GetComponentsInChildren<PracticeSentimentActor>(true).Single(a=>a.name=="s8_5");
            foreach(int count in new[]{3,4}){
                configuration.SetValue(null,MatchConfiguration.Practice(count));
                var owner=new GameObject("Proud halo validation controller",typeof(RectTransform),typeof(Canvas),typeof(PracticeSentimentController));SceneManager.MoveGameObjectToScene(owner,scene);owner.GetComponent<Canvas>().enabled=false;
                var controller=owner.GetComponent<PracticeSentimentController>();controller.Initialize(actorRoot);
                var cue=cloth.TransformPoint(new Vector3(-.12f,0,-.1f));var baseline=Render(count+"-pride-before");controller.Play(prideActor,cue,true);
                var effect=prideActor.GetComponent<PracticeProudHalo>();Check(effect&&!effect.Finished,"s8_5 selector starts proud halo in "+count+" ball practice");effect.SetViewCamera(camera);
                Check(Vector3.Distance(effect.CuePoint,cue)<.02f,"Pride effect is centered around cue point");
                Check(effect.OverlayCanvas.renderMode==RenderMode.ScreenSpaceOverlay&&effect.OverlayCanvas.sortingOrder==32767,"Pride effect overlays HUD and cue UI");
                var images=effect.VisualRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true);Check(images.All(g=>!g.raycastTarget)&&!effect.OverlayCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),"Pride overlay does not intercept clicks");
                effect.OverlayCanvas.renderMode=RenderMode.ScreenSpaceCamera;effect.OverlayCanvas.worldCamera=camera;effect.OverlayCanvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                UnityEngine.UI.Image Part(string name)=>images.Single(g=>g.name==name);
                effect.Sample(.25f);Render(count+"-pride-gathering");Check(images.Count(g=>g.name.StartsWith("Gold mote")&&g.enabled)>20&&!Part("Golden halo rays").enabled,"Gold motes gather before halo unfolds");
                effect.Sample(.9f);Render(count+"-pride-unfolding");Check(Part("Golden halo rays").enabled&&Part("Expanding gold ring 0").enabled&&!Part("Proud crown").enabled,"Rays and expanding ring precede crown");
                var ringSize=Part("Expanding gold ring 0").rectTransform.sizeDelta.x;
                effect.Sample(1.2f);float first=Part("Golden halo rays").color.a;Check(Part("Expanding gold ring 0").rectTransform.sizeDelta.x>ringSize,"Gold ring expands around cue ball");
                effect.Sample(1.6f);float between=Part("Golden halo rays").color.a;var rotation=Part("Golden halo rays").transform.localRotation;
                Render(count+"-pride-crown");Check(Part("Proud crown").enabled&&images.Count(g=>g.name.StartsWith("Pride star")&&g.enabled)>=2,"Floating crown and stars emphasize pride");
                effect.Sample(1.95f);Check(first>between+.15f&&Part("Golden halo rays").color.a>between+.15f,"Golden halo brightens twice");Check(Part("Golden halo rays").transform.localRotation!=rotation,"Halo rotates gently");
                effect.Sample(2.8f);float alpha=Part("Golden halo rays").color.a;effect.Sample(3.2f);Render(count+"-pride-clearing");Check(Part("Golden halo rays").color.a<alpha&&images.Any(g=>g.name.StartsWith("Gold mote")&&g.enabled),"Rays fade while gold motes rise");
                effect.Sample(PracticeProudHalo.Duration-.01f);Check(!images.Any(g=>g.enabled),"Every pride sprite fades before cleanup");
                effect.Sample(PracticeProudHalo.Duration+.01f);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Pride effect naturally finishes");
                var restored=Render(count+"-pride-restored");long difference=0;for(int i=0;i<baseline.Length;i++)difference+=Math.Abs(baseline[i].r-restored[i].r)+Math.Abs(baseline[i].g-restored[i].g)+Math.Abs(baseline[i].b-restored[i].b);Check(difference==0,"Pride restores original table pixels exactly");
                controller.Play(prideActor,cue,true);effect.Sample(1.5f);controller.Hide();Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Selection change cancels pride immediately");
                controller.Play(prideActor,cue,true);effect.Sample(.25f);Check(!Part("Proud crown").enabled&&!Part("Golden halo rays").enabled,"Repeated pride restarts without old rays or crown");
                prideActor.gameObject.SetActive(false);typeof(PracticeProudHalo).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);Check(effect.Finished&&!effect.VisualRoot.activeSelf,"Disable callback clears pride effect");
                Check(cloth.localToWorldMatrix==originalMatrix&&bodies.Select((b,i)=>b.position==bodyPositions[i]&&b.rotation==bodyRotations[i]).All(same=>same),"Pride preserves table and ball transforms");
                effect.OverlayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;effect.OverlayCanvas.worldCamera=null;controller.Hide();UnityEngine.Object.DestroyImmediate(owner);
            }
            report.passed=true;
        }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}finally{configuration.SetValue(null,savedConfig);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}File.WriteAllText("Logs/practice-demolition-validation.json",JsonUtility.ToJson(report,true));}
    }
}

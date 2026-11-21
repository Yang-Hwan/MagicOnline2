using System;
using System.IO;
using System.Reflection;
using Assets.Scripts.Sight.Surface.Pavilion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Preview-scene validation never replaces, saves or enters play mode in the user's scene.
[InitializeOnLoad]
public static class ElasticControlValidation
{
    const string Folder="Logs/ElasticControls";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] class Report { public bool passed; public int checks; public string error; }
    static ElasticControlValidation()
    {
        EditorApplication.update+=()=>{
            if(!File.Exists(Folder+"/run.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Folder+"/run.request");Run();
        };
    }
    [MenuItem("Tools/Gameplay/Elastic Controls/Validate And Render Approved Design")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        var report=new Report();var scene=EditorSceneManager.NewPreviewScene();
        RenderTexture target=null;Texture2D sheet=null;ElasticControlSettings settings=null;
        var previousTarget=RenderTexture.active;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);report.checks++;}
        try
        {
            settings=UnityEngine.Object.Instantiate(Resources.Load<ElasticControlSettings>("Gameplay/ElasticControls/Settings"));
            Check(settings && settings.useNewAppearance,"Approved appearance is enabled");
            var root=new GameObject("Elastic control preview");SceneManager.MoveGameObjectToScene(root,scene);
            var cameraRoot=new GameObject("Preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.scene=scene;camera.enabled=false;
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.08f,.085f);
            target=new RenderTexture(120,768,24);camera.targetTexture=target;
            var space=new GameObject("Input space").transform;space.SetParent(root.transform,false);
            var power=new GameObject("Original power handle").transform;power.SetParent(space,false);
            var follow=new GameObject("Original follow handle").transform;follow.SetParent(space,false);
            var owner=root.AddComponent<ElasticControlPreviewHost>();
            bool opponentTurn=false;
            var view=ElasticShotControls.Install(owner,power,follow,null,null,()=>camera,()=>opponentTurn);
            object Get(string name)=>typeof(ElasticShotControls).GetField(name,Private).GetValue(view);
            RectTransform Rect(string name)=>(RectTransform)Get(name);
            typeof(ElasticShotControls).GetField("settings",Private).SetValue(view,settings);
            var canvas=Rect("canvasRoot").GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if(font)foreach(var label in view.GetComponentsInChildren<TMP_Text>(true)){label.font=font;label.fontSharedMaterial=font.material;}
            void Layout(float powerY,float followY)
            {
                power.localPosition=new Vector3(0,powerY,0);follow.localPosition=new Vector3(0,followY,0);
                typeof(ElasticShotControls).GetMethod("LateUpdate",Private).Invoke(view,null);
                Canvas.ForceUpdateCanvases();
                foreach(var graphic in view.GetComponentsInChildren<Graphic>(true)){
                    graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);
                }
                Canvas.ForceUpdateCanvases();
            }
            Rect Bounds(string name){var r=Rect(name);return new Rect(r.anchoredPosition,r.sizeDelta);}
            Vector4 MeshEnds(string name)
            {
                var graphic=Rect(name).GetComponent<ElasticControlGraphic>();
                using(var mesh=new VertexHelper()){
                    typeof(ElasticControlGraphic).GetMethod("OnPopulateMesh",Private|BindingFlags.DeclaredOnly).Invoke(graphic,new object[]{mesh});
                    var lower=default(UIVertex);var upper=default(UIVertex);
                    mesh.PopulateUIVertex(ref lower,32);mesh.PopulateUIVertex(ref upper,128);
                    float previous=-1;
                    for(int i=0;i<mesh.currentVertCount;i+=2){
                        var v=default(UIVertex);mesh.PopulateUIVertex(ref v,i);
                        Check(!float.IsNaN(v.uv0.y) && v.uv0.y>=previous-.00001f && v.uv0.y>=0 && v.uv0.y<=1,"Stretch UVs remain continuous and ordered");
                        previous=v.uv0.y;
                    }
                    return new Vector4(lower.position.y,graphic.rectTransform.rect.height-upper.position.y,lower.uv0.y,upper.uv0.y);
                }
            }
            bool Covers(string cover,string screen){var a=Bounds(cover);var b=Bounds(screen);return a.yMin<=b.yMin+.01f && a.yMax>=b.yMax-.01f;}
            Check(ElasticShotControls.GaugeText(0)=="0.0" && ElasticShotControls.GaugeText(1)=="10.0" && ElasticShotControls.GaugeText(.35f)=="3.5","Numeric range is 0.0 to 10.0");
            Check(ElasticShotControls.GaugeText(-1)=="0.0" && ElasticShotControls.GaugeText(2)=="10.0","Numeric values are clamped");
            Layout(0,0);
            var grip=Rect("powerPad").GetComponent<ElasticControlGraphic>();
            Check(grip.roundedSilhouette,"Power grip clips opaque source-image corners");
            using(var mesh=new VertexHelper()){
                typeof(ElasticControlGraphic).GetMethod("OnPopulateMesh",Private|BindingFlags.DeclaredOnly).Invoke(grip,new object[]{mesh});
                var lower=default(UIVertex);var upper=default(UIVertex);var middle=default(UIVertex);
                mesh.PopulateUIVertex(ref lower,0);mesh.PopulateUIVertex(ref upper,160);mesh.PopulateUIVertex(ref middle,80);
                float inset=Mathf.Min(grip.rectTransform.rect.width,grip.rectTransform.rect.height)*grip.cornerRadius;
                Check(Mathf.Abs(lower.position.x-inset)<.01f && Mathf.Abs(upper.position.x-inset)<.01f,"Upper and lower corner geometry excludes opaque backdrop");
                Check(Mathf.Abs(middle.position.x)<.01f,"Rounded clipping retains full grip width in the middle");
                Check(lower.uv0.x>grip.uvRect.xMin && upper.uv0.x>grip.uvRect.xMin && Mathf.Abs(middle.uv0.x-grip.uvRect.xMin)<.00001f,"Rounded corners preserve metal texture coordinates");
            }
            Check(Covers("followPad","followDisplay"),"Closed cap covers fixed follow display");
            var originalPower=Bounds("powerDisplay");var originalFollow=Bounds("followDisplay");
            Check(Rect("powerDisplay").GetSiblingIndex()<Rect("powerPad").GetSiblingIndex() && Rect("followDisplay").GetSiblingIndex()<Rect("powerPad").GetSiblingIndex(),"Moving grip renders in front of both fixed displays");
            Check(Rect("followDisplay").GetSiblingIndex()<Rect("followPad").GetSiblingIndex(),"Moving cap renders in front of follow display");
            Layout(-ElasticShotControls.PowerTravel,ElasticShotControls.LegacyFollowTravel);
            Check(Covers("powerPad","powerDisplay"),"Full power physically covers bottom display");
            Check(Bounds("followDisplay")==originalFollow && Bounds("powerDisplay")==originalPower,"Both displays remain fixed during dragging");
            Check(!Bounds("powerPad").Overlaps(Bounds("followDisplay")) && !Bounds("followPad").Overlaps(Bounds("followDisplay")),"Raised cap reveals follow display while power is pulled");
            Layout(ElasticShotControls.LegacyFollowTravel,ElasticShotControls.LegacyFollowTravel);
            Check(Covers("powerPad","followDisplay"),"Released grip covers follow display at full follow boundary");
            Layout(.15f,.15f);
            Check(Covers("powerPad","followDisplay"),"Released grip covers follow display at intermediate boundary");
            Check(Rect("panel").GetComponent<ElasticControlGraphic>().preserveEndCaps && Rect("stem").GetComponent<ElasticControlGraphic>().preserveEndCaps,"Body and shaft stretch only between protected ends");
            Layout(0,.31f);var bodyEnds=MeshEnds("panel");var stemEnds=MeshEnds("stem");
            var gripSize=Rect("powerPad").sizeDelta;
            settings.bodyHeight=520;settings.followTravel=125;Layout(0,.31f);
            Check(Mathf.Abs(Rect("panel").rect.height-520)<.1f && Rect("powerPad").sizeDelta==gripSize,"Longer body preserves grip size");
            Check(Bounds("powerDisplay")==originalPower && Mathf.Abs(Bounds("followDisplay").y-originalFollow.y-118)<.1f,"Longer body preserves bottom display and moves top display with the body edge");
            Check((MeshEnds("panel")-bodyEnds).sqrMagnitude<.0001f && (MeshEnds("stem")-stemEnds).sqrMagnitude<.0001f,"Resizing preserves exact end-region geometry and UVs on body and shaft");
            settings.bodyHeight=402;settings.followTravel=95;
            float[] powers={0,0,0,-.455f,-.91f,0,-.3185f,-.637f,.155f};
            float[] follows={0,.31f,0,0,0,.155f,.155f,.155f,.155f};
            sheet=new Texture2D(120*9,600,TextureFormat.RGB24,false);
            for(int i=0;i<9;i++){
                Layout(powers[i],follows[i]);camera.Render();RenderTexture.active=target;
                var image=new Texture2D(120,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,120,600),0,0);image.Apply();
                File.WriteAllBytes(Folder+"/state-"+(i+1)+".png",image.EncodeToPNG());
                sheet.SetPixels(i*120,0,120,600,image.GetPixels());UnityEngine.Object.DestroyImmediate(image);
            }
            sheet.Apply();File.WriteAllBytes(Folder+"/approved-controls.png",sheet.EncodeToPNG());
            Layout(-.455f,.31f);
            var savedPower=power.localPosition;var savedFollow=follow.localPosition;
            Check(!Rect("metalCover").gameObject.activeSelf && !ElasticShotControls.InputBlocked,"Local/single turn leaves normal controls available");
            opponentTurn=true;Layout(savedPower.y,savedFollow.y);
            Check(Rect("metalCover").gameObject.activeInHierarchy && !Rect("panel").gameObject.activeSelf,"Opponent turn shows full cover and hides all working controls");
            Check(ElasticShotControls.InputBlocked && !ElasticShotControls.TryClassify(new Vector2(54,200),out _),"Covered controls reject drag classification and input");
            Check(power.localPosition==savedPower && follow.localPosition==savedFollow,"Cover never changes authoritative shot transforms");
            Check(Rect("metalCover").GetComponentsInChildren<TMP_Text>(true).Length==0,"Plain cover contains no icon or opponent-turn label");
            Check(Rect("metalCover").GetComponent<ElasticControlGraphic>().texture && Rect("metalCover").GetComponent<ElasticControlGraphic>().coverSilhouette,"Plain cover uses approved metal texture with transparent curved ends");
            var corners=new Vector3[4];var coverCorners=new Vector3[4];Rect("metalCover").GetWorldCorners(coverCorners);
            foreach(var part in new[]{"panel","powerPad","followPad"}){
                Rect(part).GetWorldCorners(corners);
                Check(corners[0].y>=coverCorners[0].y && corners[2].y<=coverCorners[2].y && corners[0].x>=coverCorners[0].x && corners[2].x<=coverCorners[2].x,"Cover encloses "+part+" at maximum extension");
            }
            space.gameObject.SetActive(false);Layout(savedPower.y,savedFollow.y);
            Check(Rect("metalCover").gameObject.activeInHierarchy,"Cover remains in place when opponent source handles are hidden");
            camera.Render();RenderTexture.active=target;
            var coverImage=new Texture2D(120,600,TextureFormat.RGB24,false);coverImage.ReadPixels(new Rect(0,0,120,600),0,0);coverImage.Apply();
            File.WriteAllBytes(Folder+"/opponent-cover.png",coverImage.EncodeToPNG());UnityEngine.Object.DestroyImmediate(coverImage);
            space.gameObject.SetActive(true);opponentTurn=false;Layout(savedPower.y,savedFollow.y);
            Check(!Rect("metalCover").gameObject.activeSelf && Rect("panel").gameObject.activeSelf && !ElasticShotControls.InputBlocked,"Returning to local turn restores normal appearance and interaction");
            report.passed=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            RenderTexture.active=previousTarget;
            EditorSceneManager.ClosePreviewScene(scene);
            if(target)UnityEngine.Object.DestroyImmediate(target);
            if(sheet)UnityEngine.Object.DestroyImmediate(sheet);
            if(settings)UnityEngine.Object.DestroyImmediate(settings);
            File.WriteAllText(Folder+"/validation.json",JsonUtility.ToJson(report,true));
        }
    }
}

public sealed class ElasticControlPreviewHost : MonoBehaviour { }

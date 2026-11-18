using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ThreeCushionTechniqueValidation
{
    const string Root="Logs/Techniques/";
    static ThreeCushionTechniqueValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)return;
        if(!File.Exists(Root+"validate.request") && !File.Exists(Root+"build.request"))return;
        bool build=File.Exists(Root+"build.request");
        if(build)File.Delete(Root+"build.request");if(File.Exists(Root+"validate.request"))File.Delete(Root+"validate.request");
        try
        {
            Run();
            if(build)
            {
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    target=BuildTarget.StandaloneWindows64,locationPathName="Build/TechniqueUpdate/MagicOnline2.exe",options=BuildOptions.Development});
                if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);
                File.WriteAllText(Root+"build.json","{\"Ok\":true,\"Errors\":"+result.summary.totalErrors+"}");
            }
        }catch(Exception ex){File.WriteAllText(Root+"error.txt",ex.ToString());Debug.LogException(ex);}
    }
    [MenuItem("Tools/Magic Online/Validate Three Cushion Techniques")]
    public static void Run()
    {
        Directory.CreateDirectory(Root);int checks=0;
        foreach(string name in new[]{"Back","Brush","Titles","ExtraTitles"})
        {
            string path="Assets/Resources/Gameplay/Techniques/"+name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer){importer.maxTextureSize=4096;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();}
            Check(Resources.Load<Texture2D>("Gameplay/Techniques/"+name),"art loads: "+name);
        }
        Check(Resources.Load<Shader>("Gameplay/Techniques/TechniqueArtwork")?.isSupported==true,"artwork shader supported");
        var bounds=new Bounds(Vector3.zero,new Vector3(4,.02f,2));
        Vector3 P(float x,float z)=>new Vector3(x,0,z);
        ThreeCushionShotTrace Trace(Vector3 incoming,Vector3 firstPosition=default,Vector3 target=default,float radius=0)
        {
            var t=new ThreeCushionShotTrace();t.Begin(0,P(-.5f,-.5f),bounds);t.Hit(1,-1,firstPosition,incoming,P(0,1),0,target,radius);return t;
        }
        Vector3 Pos(int rail)=>rail==0?P(-2,0):rail==1?P(2,0):rail==2?P(0,-1):P(0,1);
        void Rail(ThreeCushionShotTrace t,int rail,Vector3 input=default,Vector3 output=default,float spin=0)=>t.Hit(-1,rail,Pos(rail),input,output,spin);
        void Finish(ThreeCushionShotTrace t,Vector3 end=default)=>t.Hit(2,-1,end,Vector3.forward,Vector3.zero,0);
        void Expect(ThreeCushionShotTrace t,ThreeCushionTechnique kind,Vector3 launch=default)
        {
            Check(t.Classify(true)==kind,"classifies "+kind+" (got "+t.Classify(true)+")");
            Check(t.Classify(false)==ThreeCushionTechnique.None,"miss suppressed "+kind);
            // Further contacts after the second object must never change the title.
            Rail(t,0);Check(t.Classify(true)==kind,"frozen at score "+kind);
            foreach(bool rotate in new[]{false,true})
            {
                Vector3 Map(Vector2 p)=>rotate?P(p.y,-p.x):P(-p.x,p.y);
                var replica=new ThreeCushionShotTrace();
                replica.Begin(0,Map(t.Path[0]),new Bounds(Vector3.zero,rotate?new Vector3(2,.02f,4):bounds.size),Map(ThreeCushionShotTrace.Plane(launch)));
                foreach(var contact in t.Contacts)
                {
                    int rail=contact.rail;
                    if(rail>=0)rail=rotate?new[]{3,2,0,1}[rail]:rail<2?1-rail:rail;
                    replica.Hit(contact.ball,rail,Map(contact.position),Map(contact.incoming),Map(contact.outgoing),
                        contact.spin*(rotate?1:-1),Map(contact.target),contact.radius);
                }
                Check(replica.Classify(true)==kind,"orientation invariant "+kind+" rotate="+rotate);
            }
        }
        var side=Trace(Vector3.forward);Rail(side,3);Rail(side,1);Rail(side,2);Finish(side);Expect(side,ThreeCushionTechnique.Side);
        var back=Trace(Vector3.right);Rail(back,3);Rail(back,1);Rail(back,2);Finish(back);Expect(back,ThreeCushionTechnique.Back);
        foreach(int end in new[]{0,1})
        foreach(int adjacent in new[]{2,3})
        foreach(bool extra in new[]{false,true})
        {
            var front=Trace(Vector3.right);
            Rail(front,end);Rail(front,adjacent);Rail(front,adjacent==2?3:2);
            if(extra){Rail(front,end,Vector3.forward,-Vector3.forward,2);Rail(front,end,Vector3.forward,Vector3.back,1);}
            Finish(front);Expect(front,ThreeCushionTechnique.Front);
        }
        foreach(int end in new[]{0,1})
        foreach(int adjacent in new[]{2,3})
        {
            foreach(float degrees in new[]{0f,19.9f,20.1f,45f})
            {
                var launch=P((end==0?-1:1)*Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad));
                var shot=new ThreeCushionShotTrace();shot.Begin(0,Vector3.zero,bounds,launch);
                shot.Hit(1,-1,P(.1f,.1f),Vector3.forward,Vector3.right,0);
                Rail(shot,end);Rail(shot,adjacent);Rail(shot,1-end);Finish(shot);
                if(degrees<20)Expect(shot,ThreeCushionTechnique.HorizontalHit,launch);
                else Check(shot.Classify(true)!=ThreeCushionTechnique.HorizontalHit,"launch outside horizontal range");
            }
        }
        var extraRail=new ThreeCushionShotTrace();extraRail.Begin(0,Vector3.zero,bounds,Vector3.left);
        extraRail.Hit(1,-1,Vector3.zero,Vector3.left,Vector3.left,0);
        foreach(int r in new[]{0,2,1,3})Rail(extraRail,r);Finish(extraRail);
        Check(extraRail.Classify(true)!=ThreeCushionTechnique.HorizontalHit,"fourth rail before score excludes horizontal hit");
        var oldFront=Trace(Vector3.right);Rail(oldFront,3);Rail(oldFront,1);Rail(oldFront,0);Finish(oldFront);
        Check(oldFront.Classify(true)!=ThreeCushionTechnique.Front,"long rail first excludes front");
        var reverse=Trace(Vector3.forward);Rail(reverse,3,P(1,1),P(-.3f,-1),-2);Rail(reverse,2);Rail(reverse,1);Finish(reverse);Expect(reverse,ThreeCushionTechnique.Reverse);
        var grand=Trace(Vector3.forward);foreach(int r in new[]{3,1,2,0})Rail(grand,r);Finish(grand);Expect(grand,ThreeCushionTechnique.Grand);
        foreach(int n in new[]{2,3})
        {
            var bank=new ThreeCushionShotTrace();bank.Begin(0,P(0,0),bounds);
            for(int i=0;i<n;i++)Rail(bank,new[]{3,1,2}[i]);bank.Hit(1,-1,P(.5f,.5f),Vector3.forward,Vector3.right,0);
            if(n==2)Rail(bank,0);Finish(bank);Expect(bank,n==2?ThreeCushionTechnique.TwoBank:ThreeCushionTechnique.ThreeBank);
        }
        var ret=Trace(Vector3.forward,P(-.5f,.5f));Rail(ret,3);Rail(ret,1);Rail(ret,3);Finish(ret,P(-.6f,.8f));Expect(ret,ThreeCushionTechnique.Return);
        foreach(int extraCount in new[]{0,1,2})
        {
            var returned=Trace(Vector3.forward,P(-1.5f,.5f));
            Rail(returned,1);Rail(returned,2);Rail(returned,1);
            for(int i=0;i<extraCount;i++)Rail(returned,3,Vector3.forward,Vector3.back,2);
            Finish(returned,P(1.7f,-.8f));Expect(returned,ThreeCushionTechnique.Return);
        }
        var bound=Trace(Vector3.forward);Rail(bound,3,Vector3.forward,-Vector3.forward,2);Rail(bound,3,Vector3.forward,-Vector3.forward,1);Rail(bound,1);Finish(bound);Expect(bound,ThreeCushionTechnique.Bounding);
        foreach(float angle in new[]{0f,4.99f,5f,5.01f})
        {
            float half=angle*.5f*Mathf.Deg2Rad;
            var shallow=Trace(Vector3.forward);Rail(shallow,0);
            Rail(shallow,3,Vector3.forward,P(Mathf.Cos(half),-Mathf.Sin(half)),1);
            Rail(shallow,3,P(Mathf.Cos(half),Mathf.Sin(half)),Vector3.back);
            Finish(shallow);
            if(angle>=5f) Expect(shallow,ThreeCushionTechnique.Bounding);
            else Check(shallow.Classify(true)!=ThreeCushionTechnique.Bounding,"below five degrees: "+angle);
        }
        var cut=Trace(Vector3.right,P(0,.9f),P(0,.94f),.03f);Rail(cut,3);Rail(cut,1);Rail(cut,2);Finish(cut);Expect(cut,ThreeCushionTechnique.Cut);
        // Slot 20 (2026-11-18): first contact reconstructed between replay frames 54/55.
        // Scale the 2.64 x 1.32 table uniformly to this fixture's 4 x 2 bounds.
        float replayScale=4f/2.64f;
        Vector3 ReplayPoint(float x,float z)=>P(x*replayScale,(z+.16f)*replayScale);
        var replayContact=ReplayPoint(1.0638f,-.1951f);
        var replayTarget=ReplayPoint(1.0805689f,-.14038675f);
        var replayRail=ReplayPoint(1.306f,-.202f);
        Check(Vector3.Distance(replayContact,replayRail)>.36f,"slot 20 excluded by former cue travel limit");
        foreach(bool thick in new[]{false,true})
        foreach(bool recordedLaunch in new[]{false,true})
        {
            var incoming=thick?replayTarget-replayContact:P(1.7473232f,.5971208f);
            var launch=recordedLaunch?P(2.5642264f,.8762813f):Vector3.zero;
            var shot=new ThreeCushionShotTrace();
            shot.Begin(0,ReplayPoint(.08514f,-.52958f),bounds,launch);
            shot.Hit(1,-1,replayContact,incoming,P(1,0),0,replayTarget,.0285f*replayScale);
            shot.Hit(-1,1,replayRail,P(1,0),P(-1,1),0);
            shot.Hit(-1,3,ReplayPoint(.2063f,.4688f),P(-1,1),P(-1,-1),0);
            shot.Hit(-1,0,ReplayPoint(-1.302f,-.0976f),P(-1,-1),P(1,-1),0);
            Finish(shot,ReplayPoint(-.45f,-.688f));
            Expect(shot,thick?(recordedLaunch?ThreeCushionTechnique.HorizontalHit:ThreeCushionTechnique.Side):ThreeCushionTechnique.Cut,launch);
        }
        var farCut=Trace(Vector3.up,P(1,.2f),P(1.05f,.2f),.03f);
        Rail(farCut,1);Rail(farCut,3);Rail(farCut,0);Finish(farCut);
        Expect(farCut,ThreeCushionTechnique.Side);
        var cross=Trace(Vector3.forward);foreach(int r in new[]{3,2,3})Rail(cross,r);Finish(cross);Expect(cross,ThreeCushionTechnique.Cross);
        var longitudinal=Trace(Vector3.forward);foreach(int r in new[]{1,0,1})Rail(longitudinal,r);Finish(longitudinal);Expect(longitudinal,ThreeCushionTechnique.Lengthwise);
        var dbl=Trace(Vector3.forward);foreach(int r in new[]{3,2,1})Rail(dbl,r);Finish(dbl);Expect(dbl,ThreeCushionTechnique.DoubleCushion);
        var objectFirst=Trace(Vector3.right,P(0,.98f),P(0,.93f),.03f);foreach(int r in new[]{3,1,2})Rail(objectFirst,r);Finish(objectFirst);Expect(objectFirst,ThreeCushionTechnique.Cut);
        var kick=new ThreeCushionShotTrace();kick.Begin(0,Vector3.zero,bounds);Rail(kick,3);kick.Hit(1,-1,P(.5f,.5f),Vector3.right,Vector3.forward,0);Rail(kick,1);Rail(kick,2);Finish(kick);Expect(kick,ThreeCushionTechnique.OneBank);
        ThreeCushionShotTrace AfterOneRail(Vector3 position,Vector3 target,Vector3 approach=default)
        {
            var trace=new ThreeCushionShotTrace();trace.Begin(0,Vector3.zero,bounds);Rail(trace,3);
            trace.Hit(1,-1,position,approach==Vector3.zero?Vector3.right:approach,Vector3.forward,0,target,.03f);
            Rail(trace,1);Rail(trace,2);Finish(trace);return trace;
        }
        Expect(AfterOneRail(P(.05f,.97f),P(.10f,.96f),P(1,-.3f)),ThreeCushionTechnique.Kick);
        Expect(AfterOneRail(P(.05f,.97f),P(.10f,.96f)),ThreeCushionTechnique.Ticky);
        Expect(AfterOneRail(P(.45f,.97f),P(.50f,.96f)),ThreeCushionTechnique.OneBank);
        Expect(AfterOneRail(P(.45f,.97f),P(.50f,.96f),P(1,-.3f)),ThreeCushionTechnique.Kick);
        // Radius .03: .12 clearance is exactly two diameters, regardless of distance along the rail.
        Expect(AfterOneRail(P(.45f,.87f),P(.50f,.88f)),ThreeCushionTechnique.Kick);
        Expect(AfterOneRail(P(.45f,.87f),P(.50f,.879f)),ThreeCushionTechnique.OneBank);
        Expect(AfterOneRail(P(.06f,.92f),P(.08f,.87f)),ThreeCushionTechnique.OneBank);
        var early=Trace(Vector3.forward);Rail(early,3);Finish(early);Rail(early,1);Rail(early,2);Check(early.Classify(true)==ThreeCushionTechnique.None,"early second ball never rescued by later cushions");
        var kiss=Trace(Vector3.forward);kiss.Hit(1,-1,P(0,.5f),Vector3.forward,Vector3.right,0);foreach(int r in new[]{3,1,2})Rail(kiss,r);Finish(kiss);Check(kiss.Classify(true)==ThreeCushionTechnique.None,"ambiguous kiss suppressed");
        side.Clear();Check(side.Classify(true)==ThreeCushionTechnique.None,"new shot clears old classification");
        Check(ThreeCushionShotTrace.Label(ThreeCushionTechnique.Cut)=="비껴치기","single display name for spelling variants");
        Render();
        File.WriteAllText(Root+"validation.json","{\"Ok\":true,\"Checks\":"+checks+",\"TechniqueKinds\":17,\"Renders\":20}");
        void Check(bool ok,string detail){if(!ok)throw new Exception(detail);checks++;}
        void Render()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var go=new GameObject("Technique render",typeof(RectTransform),typeof(Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
                var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1920,1080);rect.position=Vector3.zero;
                var cameraGO=new GameObject("Technique validation camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO,scene);
                var cam=cameraGO.GetComponent<Camera>();cam.scene=scene;cam.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);cam.enabled=false;
                cam.orthographic=true;cam.orthographicSize=540;cam.aspect=1920f/1080;cam.transform.position=new Vector3(0,0,-100);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.03f,.25f,.09f);canvas.worldCamera=cam;
                var view=ThreeCushionTechniqueMotion.Install(rect,cam);view.enabled=false;
                foreach(ThreeCushionTechnique kind in Enum.GetValues(typeof(ThreeCushionTechnique)))
                {
                    if(kind==ThreeCushionTechnique.None)continue;
                    Check(view.Play(kind),"title loads "+kind);view.AdvanceVisual(.6f);Shot(kind.ToString());
                    Check(!view.Title.raycastTarget&&!view.Brush.raycastTarget&&!view.GetComponent<CanvasGroup>().blocksRaycasts,"input passes through "+kind);
                }
                view.Play(ThreeCushionTechnique.Back);view.AdvanceVisual(.06f);Check(view.Title.color.a==0&&view.Brush.color.a>0,"brush leads lettering");Shot("brush-leading");
                view.AdvanceVisual(.3f);float width=view.Title.rectTransform.sizeDelta.x;Check(Mathf.Abs(width/(1920*.77f)-.58f)<.001f,"approved reduced lettering size");
                view.AdvanceVisual(1.799f);Check(view.Title.color.a==1,"1.5 second hold");
                view.AdvanceVisual(1.95f);Shot("exit");Check(view.Title.color.a<1&&view.Title.rectTransform.localScale.x>1,"exit grows and fades");
                view.AdvanceVisual(2.1f);Check(view.GetComponent<CanvasGroup>().alpha==0,"ends in 2.1 seconds");Shot("finished");
                void Shot(string name)
                {
                    Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,1080,24);cam.targetTexture=rt;cam.Render();
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                    File.WriteAllBytes(Root+name+".png",image.EncodeToPNG());RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
                }
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}

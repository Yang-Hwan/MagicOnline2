using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Sight.Surface.Consist;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Explicit local UI check with a read-only export of the real DB chart. Never enters matchmaking.
[InitializeOnLoad]
public static class ArenaCarouselValidation
{
    const string Root="Logs/ArenaCarousel/",Key="ArenaCarouselValidation";
    [Serializable] sealed class Rows{public SkillMatchData[] Items;}
    static double deadline,next;
    static PointerEventData pointer;
    static Vector2 origin,end;
    static int expected;
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static ArenaCarouselValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<next)return;
        try
        {
            int step=SessionState.GetInt(Key,0);
            if(step==0)
            {
                if(!File.Exists(Root+"run.request"))return;
                File.Delete(Root+"run.request");
                if(!EditorApplication.isPlaying)
                {
                    if(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                        throw new Exception("Unsaved scene prevents UI validation");
                    EditorSceneManager.OpenScene("Assets/Scenes/Consist.unity");
                    SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;return;
                }
                SessionState.SetInt(Key,1);step=1;
            }
            if(!EditorApplication.isPlaying)return;
            if(step==1)
            {
                CheckSpring();
                var rows=JsonUtility.FromJson<Rows>("{\"Items\":"+File.ReadAllText(Root+"db-halls.json")+"}");
                if(rows.Items.Length<3)throw new Exception("Need three actual DB arenas for carousel check");
                BackendChart.skillMatchData=new List<SkillMatchData>(rows.Items);
                deadline=EditorApplication.timeSinceStartup+90;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Consist");
                SessionState.SetInt(Key,2);next=EditorApplication.timeSinceStartup+2;return;
            }
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Arena UI validation timed out at "+step);
            var view=UnityEngine.Object.FindAnyObjectByType<BottomMenuView>();if(!view)return;
            if(step==2)
            {
                view.SelectPage(2);SessionState.SetInt(Key,3);next=EditorApplication.timeSinceStartup+.5;return;
            }
            var carousel=view.GetComponentInChildren<ArenaSnapCarousel>();if(!carousel)return;
            if(step==3)
            {
                if(carousel.Count!=BackendChart.skillMatchData.Count)throw new Exception("Arena count does not match DB");
                var rect=(RectTransform)carousel.transform;
                origin=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                end=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center+Vector2.left*ArenaSnapCarousel.Pitch*.68f));
                pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=origin};
                carousel.OnBeginDrag(pointer);SessionState.SetInt(Key,4);next=EditorApplication.timeSinceStartup+.12;
            }
            else if(step==4)
            {
                pointer.position=end;carousel.OnDrag(pointer);float position=carousel.Position;
                var counts=new Dictionary<int,int>();
                counts[BackendChart.skillMatchData[0].HallIdx]=321;
                var originalCounts=new Dictionary<int,int>((Dictionary<int,int>)typeof(BottomMenuView).GetField("roomCounts",Flags).GetValue(view));
                try
                {
                    typeof(BottomMenuView).GetMethod("RoomCountsChanged",Flags).Invoke(view,new object[]{counts});
                    typeof(BottomMenuView).GetMethod("UserDataChanged",Flags).Invoke(view,null);
                    if(!carousel.Dragging || carousel.Position!=position || view.GetComponentInChildren<ArenaSnapCarousel>()!=carousel)
                        throw new Exception("Live data refresh reset the drag");
                }
                finally{typeof(BottomMenuView).GetMethod("RoomCountsChanged",Flags).Invoke(view,new object[]{originalCounts});}
                carousel.OnEndDrag(pointer);expected=carousel.Target;
                if(expected!=1)throw new Exception("Left drag did not choose the next magnet");
                SessionState.SetInt(Key,5);
            }
            else if(step==5)
            {
                if(!carousel.Settled)return;CheckCentre(view,carousel);
                ScreenCapture.CaptureScreenshot(Root+"01-centred.png");
                SessionState.SetInt(Key,6);next=EditorApplication.timeSinceStartup+.4;
            }
            else if(step==6)
            {
                var side=view.GetComponentsInChildren<Button>().Single(b=>b.name=="ArenaAction3");
                side.onClick.Invoke();expected++;
                if(carousel.Target!=expected || view.CurrentPage!=2)throw new Exception("Side tap entered a match or failed to snap");
                SessionState.SetInt(Key,7);
            }
            else if(step==7)
            {
                if(!carousel.Settled)return;CheckCentre(view,carousel);
                ScreenCapture.CaptureScreenshot(Root+"02-side-tap.png");
                SessionState.SetInt(Key,8);next=EditorApplication.timeSinceStartup+.4;
            }
            else if(step==8)
            {
                view.SelectPage(0);view.SelectPage(2);
                var nextCarousel=view.GetComponentInChildren<ArenaSnapCarousel>();
                if(nextCarousel.Index(nextCarousel.Target)!=2)throw new Exception("Selection was lost on tab return");
                File.WriteAllText(Root+"result.json","{\"passed\":true,\"actualDbRows\":"+BackendChart.skillMatchData.Count+",\"dragSnap\":true,\"sideTap\":true,\"dataRefreshKeepsDrag\":true,\"tabReturn\":true}");
                SessionState.SetInt(Key,0);
            }
        }
        catch(Exception e){Directory.CreateDirectory(Root);File.WriteAllText(Root+"error.txt",e.ToString());SessionState.SetInt(Key,0);}
    }
    static void CheckCentre(BottomMenuView view,ArenaSnapCarousel carousel)
    {
        if(Mathf.Abs(carousel.Position-expected)>.001f)throw new Exception("Card stopped between magnets");
        var centre=view.GetComponentsInChildren<Button>().Single(b=>b.name=="ArenaAction2").transform.parent;
        var labels=centre.GetComponentsInChildren<TMP_Text>();
        string Value(string name)=>labels.Single(t=>t.name==name).text;
        var hall=BackendChart.skillMatchData[carousel.Index(expected)];
        if(Value("ArenaTitle")!=hall.HallName || Value("Value입장")!=$"{hall.PrizeCoin:N0} 코인" || Value("Value규칙")!=$"{hall.MatchBall}구" ||
            Value("Value목표")!=$"{hall.TargetHit}개" || Value("Value제한시간")!=$"{hall.MatchTotMin}분")throw new Exception("Card fields do not match database");
        if(labels.Single(t=>t.name=="ArenaActionLabel").text=="눌러서 중앙으로")throw new Exception("Centred card is not entry-ready");
        var finish=centre.Find("ArenaFinish").GetComponent<TMP_Text>();
        if(hall.FinishMission==0 && (!string.IsNullOrEmpty(finish.text) || finish.gameObject.activeSelf))
            throw new Exception("Nonexistent default finish is still displayed");
        var carouselRect=(RectTransform)carousel.transform;
        if(Mathf.Abs(carouselRect.rect.width-BottomMenuView.Width)>.1f)
            throw new Exception("Card lane still has a narrow rectangular clipping boundary");
        var wall=view.GetComponentsInChildren<RawImage>().Single(i=>i.name=="Foreground Broken Wall");
        var blocker=wall.GetComponent<ArenaWallHitBlocker>();var texture=(Texture2D)wall.texture;
        if(wall.material.shader.name!="UI/Arena Opaque Wall")throw new Exception("Wall opacity shader missing");
        bool openingBeyondOldBoundary=false;
        foreach(float x in new[]{240f,280,310,330,1340,1360,1380,1420})
            foreach(float y in new[]{250f,400,550,700})
            {
                float alpha=texture.GetPixelBilinear(x/1672,1-y/941).a;
                var r=wall.rectTransform.rect;
                var local=new Vector2(r.xMin+x/1672*r.width,r.yMax-(y-130)/705*r.height);
                var screen=RectTransformUtility.WorldToScreenPoint(null,wall.rectTransform.TransformPoint(local));
                if(blocker.IsRaycastLocationValid(screen,null)!=(alpha>ArenaWallHitBlocker.AlphaHitThreshold))throw new Exception("Wall click mask differs from its visible broken edge");
                if((x<315 || x>1357) && alpha<.1f)openingBeyondOldBoundary=true;
            }
        if(!openingBeyondOldBoundary)throw new Exception("No broken-wall gaps were tested outside the old straight boundary");
        if(view.GetComponentsInChildren<Button>().Any(b=>b.name=="NextArena" || b.name=="PreviousArena"))throw new Exception("Legacy arrows remain");
    }
    static void CheckSpring()
    {
        var go=new GameObject("Arena spring regression",typeof(RectTransform),typeof(ArenaSnapCarousel));
        try
        {
            var carousel=go.GetComponent<ArenaSnapCarousel>();
            foreach(int count in new[]{0,1,2,7})
            {
                carousel.Initialize(count,0);carousel.SnapTo(count==7?-1:9);
                for(int i=0;i<600;i++)carousel.Advance(1f/60);
                if(!carousel.Settled || float.IsNaN(carousel.Position))throw new Exception("Spring failed at arena count "+count);
                if(count==7 && carousel.Index(carousel.Target)!=6)throw new Exception("Carousel wrap index failed");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}

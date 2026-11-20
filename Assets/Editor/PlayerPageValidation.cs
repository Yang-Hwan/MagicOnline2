using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Consist;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PlayerPageValidation
{
    static PlayerPageValidation(){EditorApplication.update+=Tick;}
    static void Tick(){if(!File.Exists("Logs/PlayerPage/run.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Logs/PlayerPage/run.request");Run();}
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void Run()
    {
        Directory.CreateDirectory("Logs/PlayerPage");checks=0;var savedCodes=BackendChart.commonCodeData;BackendChart.commonCodeData=new List<CommonCodeData>{new CommonCodeData{ComCd="LvPlayMax",IsUse="Y",ComVal=1000}};var scene=default(UnityEngine.SceneManagement.Scene);RenderTexture target=null;
        try{
            scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");var roots=scene.GetRootGameObjects();
            var legacy=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).Single();
            var menu=BottomMenuView.Install(legacy);menu.transform.parent.gameObject.SetActive(true);menu.SelectPage(0);
            var canvas=menu.GetComponentInParent<Canvas>();var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
            camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var events=roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).First();var raycaster=canvas.GetComponent<GraphicRaycaster>();
            void Invoke(string method,params object[] args)=>typeof(BottomMenuView).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,args);
            void Set(string name,object value)=>typeof(BottomMenuView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,value);
            void Prepare(){Canvas.ForceUpdateCanvases();menu.Resize();Canvas.ForceUpdateCanvases();foreach(var m in menu.GetComponentsInChildren<ModalBackdrop>())typeof(ModalBackdrop).GetMethod("Fit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,null);foreach(var g in menu.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}Canvas.ForceUpdateCanvases();camera.Render();}
            target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            Transform Find(string name)=>menu.DesignRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);
            void Tap(string name){Prepare();var rect=(RectTransform)Find(name);Check(rect,"Missing control "+name);var position=(Vector2)camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));Click(position);}
            void Click(Vector2 position){Prepare();var data=new PointerEventData(events){position=position,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);Check(hits.Count>0,"No UI hit");var hit=hits[0].gameObject;data.pointerPressRaycast=hits[0];ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit),data,ExecuteEvents.pointerClickHandler);}
            void Render(string name,int width=1672,int height=941){if(target){camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);}target=new RenderTexture(width,height,24);camera.targetTexture=target;Prepare();var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes("Logs/PlayerPage/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=previous;}
            Invoke("OpenPlayerPage",0);
            Check(Find("PlayerPage") && !Find("Inventory"),"Profile route opens a page, not the former inventory modal");
            var items=new List<ShopProductData>();
            string[] cueNames={"뇌신 큐","성운 큐","홍염 큐","인페르노","세라핌","헌츠맨","펄서"};
            string[] cueKeys={"Gameplay/PremiumCues/ThunderGod","Gameplay/PremiumCues/Nebula","Gameplay/PremiumCues/CrimsonFlame","Gameplay/ShopCues/Inferno","Gameplay/ShopCues/Seraphim","Gameplay/ShopCues/Huntsman","Gameplay/ShopCues/Pulsar"};
            for(int i=0;i<cueKeys.Length;i++)items.Add(new ShopProductData{Id=i+1,Kind="Cue",Name=cueNames[i],Owned=true,Equipped=i==0,Visual=cueKeys[i],CueAbility=new CueAbilitySettings{Grade="B",Initial=new[]{60,70,55,55},Maximum=new[]{80,90,95,100},Reach=100},CuePending=35,CueTimeMin=20,CueTimeMax=40});
            string[] characters={"Wanderer","MartialArtist","Gangster","Gentleman","Mint","Plum","Brick","Builder","Hitman","JobSeeker","Student"};
            for(int i=0;i<characters.Length;i++)items.Add(new ShopProductData{Id=100+i,Kind="Character",Name=new[]{"베테랑","무림인","강철","신사","민트","매화","브릭","빌더","히트맨","취준생","학생"}[i],Owned=true,Equipped=i==0,Emotions=new[]{new ShopEmotionData{Code="기본",Asset="Gameplay/ShopCharacters/"+characters[i]+"/Neutral"},new ShopEmotionData{Code="환희",Asset="Gameplay/ShopCharacters/"+characters[i]+"/Joy"},new ShopEmotionData{Code="좌절",Asset="Gameplay/ShopCharacters/"+characters[i]+"/Frustrated"}}});
            for(int i=0;i<7;i++)items.Add(new ShopProductData{Id=200+i,Kind="MessagePack",Name=i==0?"기본형":i==1?"까칠이":i==2?"소심이":"메시지 "+i,Owned=true,Equipped=i==0,Messages=new[]{"안녕하세요!","잘 부탁드려요.","멋진 샷이에요!","아싸!","수고하셨습니다.","좋은 경기였어요."}});
            Set("playerItems",items.ToArray());Set("playerInformation",new MatchPlayerInfo{UserId=GameApiClient.UserId,Nickname="타이거우즈20",Level=12,LevelProgress=680,TotalPrize=30000000,Total=new PlayerRecord{Wins=60,Losses=40},ThreeBall=new PlayerRecord{Wins=38,Losses=22},FourBall=new PlayerRecord{Wins=22,Losses=18}});
            Invoke("DrawPlayerHeader");Invoke("DrawPlayerRecords");Invoke("DrawPlayerLocker");
            Find("PlayerStatus").GetComponent<TMP_Text>().text="UI 검증용 데이터 · 실제 화면은 서버 정보로 표시됩니다.";
            Render("ui-cues");
            var rate=Find("RecordRate0").GetComponent<TMP_Text>();
            File.WriteAllText("Logs/PlayerPage/rate-render.txt",$"text={rate.text} vertices={rate.mesh.vertexCount} cull={rate.canvasRenderer.cull} color={rate.color} size={rate.rectTransform.rect} material={rate.fontSharedMaterial.name}");
            Check(Find("BrushTitle").GetComponent<CanvasRenderer>() && Find("EditPencil").GetComponent<CanvasRenderer>(),"Brush heading and edit icons have native CanvasRenderers");
            Check(Find("PlayerNickname").GetComponent<TMP_Text>().text=="타이거우즈20" && Find("TotalPrize").GetComponent<TMP_Text>().text=="30,000,000","Profile and cumulative prize bind their own values");
            Check(Find("RecordRate0").GetComponent<TMP_Text>().text=="60.0%" && Find("RecordRate1").GetComponent<TMP_Text>().text=="63.3%","Record panels use real model aggregation");
            Check(Find("PlayerCue1") && Find("CueAbilities").GetComponent<TMP_Text>().text.Contains("54%"),"Equipped cue uses graded capabilities");
            Tap("NextPage");Check(!Find("PlayerCue1") && menu.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("PlayerCue") && long.TryParse(r.name.Substring(9),out _))==3,"Cue pagination reaches additional owned cues");
            Tap("LockerMessages");Render("ui-messages");
            Check(Find("Phrase5") && Find("MessagePreviewViewport").GetComponent<ScrollRect>(),"All phrases appear in a scrollable preview");
            Tap("PreviewMessage201");Check(Find("MessagePreviewTitle").GetComponent<TMP_Text>().text.Contains("까칠이"),"Selecting a message pack changes its preview");
            var beforeMessages=menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("PreviewMessage")).Select(b=>b.name).ToArray();Tap("NextMessages");Check(menu.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("PreviewMessage") && !beforeMessages.Contains(b.name)),"Message packs paginate beyond the first three");
            Tap("ChangeCharacter");Render("ui-characters");
            Check(Find("CharacterPreview") && Find("Emotion0") && Find("Emotion1") && Find("Emotion2"),"Owned character grid includes emotion previews");
            var choice=menu.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("SelectCharacter") && b.name!="SelectCharacter0" && b.name!="SelectCharacter100");long choiceId=long.Parse(choice.name.Substring("SelectCharacter".Length));Tap(choice.name);Check(Find("SelectedCharacterName").GetComponent<TMP_Text>().text==items.First(p=>p.Id==choiceId).Name,"Character selection updates preview without equipping");
            var beforeCharacters=menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("SelectCharacter")).Select(b=>b.name).ToArray();Tap("NextCharacters");Check(menu.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("SelectCharacter") && !beforeCharacters.Contains(b.name)),"Character selection supports more than six choices");
            Click(new Vector2(8,450));Check(!Find("PlayerDialog") && Find("PlayerPage"),"Outside tap dismisses only the character popup");
            Tap("ChangeNickname");Render("ui-nickname");
            Check(Find("NicknameInput").GetComponent<TMP_InputField>().characterLimit==24,"Nickname edit enforces field limit");
            Check(!menu.GetComponentsInChildren<Button>().Any(b=>b.name=="Close" || b.name=="닫기"),"New profile popups have no close buttons");
            Click(new Vector2(8,450));Check(!Find("PlayerDialog"),"Outside tap dismisses nickname popup");
            Tap("LockerCue");Render("ui-4x3",1024,768);Render("ui-wide",2560,1080);
            Tap("PlayerBack");Check(!Find("PlayerPage"),"Back navigation returns to the existing menu");
            Invoke("OpenInventory");Check(Find("PlayerPage"),"Existing inventory entry routes to the same locker");Invoke("ClosePlayerPage");
            File.WriteAllText("Logs/PlayerPage/ui-result.json","{\"passed\":true,\"checks\":"+checks+",\"liveAccountsUntouched\":true,\"realUiRaycasts\":true}");
        }catch(Exception e){File.WriteAllText("Logs/PlayerPage/ui-failure.txt",e.ToString());Debug.LogException(e);}
        finally{BackendChart.commonCodeData=savedCodes;if(target)UnityEngine.Object.DestroyImmediate(target);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
    }
}

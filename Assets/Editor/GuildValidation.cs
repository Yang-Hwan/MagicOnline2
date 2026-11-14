using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Sight.Surface.Consist;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GuildValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Magic Online/Validate Guild Screens")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode first");
        var scene=EditorSceneManager.NewPreviewScene();int checks=0,renders=0;
        try
        {
            Directory.CreateDirectory("Logs/Guilds");
            var cameraRoot=new GameObject("Guild preview camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.055f);
            var canvasRoot=new GameObject("Guild preview",typeof(RectTransform),typeof(Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasRoot,scene);
            var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            var host=new GameObject("Menu",typeof(RectTransform));host.transform.SetParent(canvasRoot.transform,false);host.SetActive(false);
            var view=host.AddComponent<BottomMenuView>();var design=host.GetComponent<RectTransform>();design.sizeDelta=new Vector2(BottomMenuView.Width,BottomMenuView.Height);
            var background=new GameObject("Home background",typeof(RectTransform),typeof(RawImage));background.transform.SetParent(design,false);
            var backgroundRect=background.GetComponent<RectTransform>();backgroundRect.anchorMin=Vector2.zero;backgroundRect.anchorMax=Vector2.one;backgroundRect.offsetMin=backgroundRect.offsetMax=Vector2.zero;
            background.GetComponent<RawImage>().texture=Resources.Load<Texture2D>("Menu/home");background.GetComponent<RawImage>().raycastTarget=false;
            var font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");var material=new Material(font.material);material.DisableKeyword("UNDERLAY_ON");material.DisableKeyword("UNDERLAY_INNER");material.SetFloat("_OutlineWidth",0);material.SetFloat("_FaceDilate",0);
            Set("font",font);Set("textMaterial",material);Set("design",design);host.SetActive(true);Call("OpenGuild");
            var info=new GuildInfo{Id=Guid.NewGuid().ToString(),Name="블루 다이아몬드",Tag="BLUE",Description="함께 배우고 즐겁게 경기하는 당구 길드",Notice="오늘 저녁 9시 친선경기! 편하게 참여해 주세요.",LeaderName="타이거우즈20",MyRole="Leader",MemberCount=18,OnlineCount=7,JoinMode="Approval",Revision=1};
            Set("guildState",new GuildState{Guild=info});
            var messages=(System.Collections.Generic.List<GuildMessage>)Get("guildMessages");
            messages.Add(new(){Id=1,Name="타이거우즈20",Role="Leader",Text="반갑습니다! 오늘도 즐거운 경기 하세요.",SentAt=DateTimeOffset.Now.ToUnixTimeMilliseconds()});
            messages.Add(new(){Id=2,Name="초크장인",Role="Member",Text="친선경기 함께하실 분 계신가요?",SentAt=DateTimeOffset.Now.ToUnixTimeMilliseconds()});
            Call("DrawGuild");Render("home");Check(Find("GuildEmblem").GetComponent<CanvasRenderer>(),"guild emblem renders as native UI");
            Check(Find("GuildMessage").GetComponent<TMP_InputField>().characterLimit==250,"chat input is interactive and bounded");
            Set("guildMembers",new GuildMembers{Rows=new[]{
                new GuildMember{Id="self",Name="타이거우즈20",Level=12,Role="Leader",Status="online",Portrait="Gameplay/BandanaAvatar/Neutral"},
                new GuildMember{Id="member1",Name="초크장인",Level=8,Role="Officer",Status="online",Portrait="Gameplay/BandanaAvatar/Neutral"},
                new GuildMember{Id="member2",Name="회전마스터",Level=9,Role="Member",Status="busy"},
                new GuildMember{Id="member3",Name="오늘도한큐",Level=5,Role="Member",Status="offline"}}});
            Set("guildOwner","self");Set("guildTab",1);Call("DrawGuild");Render("members");
            var rows=Find("GuildMemberRows");Check(rows.GetComponentsInChildren<Button>().Count(b=>b.name=="경기 신청" && b.interactable)==1,"only online other member has enabled challenge button");
            Set("guildTab",2);Set("guildState",new GuildState());
            Set("guildSearch",new GuildSearch{Rows=new[]{info,new GuildInfo{Id=Guid.NewGuid().ToString(),Tag="CUE",Name="한큐 승부사",Description="실력보다 매너! 누구나 환영합니다.",MemberCount=12,OnlineCount=4,JoinMode="Open"}}});
            Call("DrawGuild");Call("DrawGuildSearchRows");Render("find");
            Check(Find("GuildSearch").GetComponent<TMP_InputField>(),"guild search uses native input");
            Call("GuildCreateForm");Render("create");Check(Find("Tag").GetComponent<TMP_InputField>().characterLimit==6,"creation form accepts bounded tag");
            Find("DialogCard").Find("자유 가입").GetComponent<Button>().onClick.Invoke();
            Check(Find("DialogCard").Find("자유 가입Fill").GetComponent<Image>().color==new Color(1,.9f,0),"join mode selection changes visible state");
            Check(view.GetComponentsInChildren<TMP_Text>().All(t=>!t.richText),"user-facing guild text cannot inject rich text markup");
            Check(view.GetComponentsInChildren<TMP_InputField>().All(i=>!i.richText && !i.textComponent.richText),"IME composition cannot add visible underline tags to guild inputs");
            Call("GuildCloseDialog");Set("guildState",new GuildState{Guild=info});Set("guildTab",3);
            var own=new GuildLeagueRank{GuildId=info.Id,Name=info.Name,Tag=info.Tag,Tier="골드 II",Rank=12,Points=1840,Wins=42,Losses=21,Draws=2};
            var season=new GuildLeagueState{Season=2,StartsAt=DateTimeOffset.Now.AddDays(-13).ToUnixTimeMilliseconds(),EndsAt=DateTimeOffset.Now.AddDays(15).ToUnixTimeMilliseconds(),Week=2,DaysLeft=15,TodayGames=2,MyGames=18,MyPoints=240,MyRank=3,Own=own,
                Rankings=new[]{new GuildLeagueRank{GuildId="leader",Name="에이스 클럽",Tag="ACE",Tier="다이아 I",Points=3240,Rank=1,Wins=84,Losses=36},own},
                Contributions=new[]{new GuildLeagueContribution{UserId="other",Name="초크장인",Rank=1,Games=22,Points=310},new GuildLeagueContribution{UserId="self",Name="타이거우즈20",Rank=3,Games=18,Points=240}},
                Recent=new[]{new GuildLeagueResult{Opponent="한큐 승부사",Result="Win",Delta=24},new GuildLeagueResult{Opponent="레드 쿠션",Result="Lose",Delta=-12}},
                Rewards=new[]{new GuildLeagueReward{Season=1,Tier="골드 II",Coin=30000,Gem=100,Emblem="GuildSeason:1:골드 II"}}};
            Set("guildLeague",season);Set("leagueTab",0);Call("DrawGuild");Render("league-season");
            Check(Find("Points").GetComponent<TMP_Text>().text.Contains("1,840"),"season view displays server rating");
            Set("leagueTab",1);Call("DrawGuildLeague");Render("league-ranking");
            Set("leagueTab",2);Call("DrawGuildLeague");Render("league-rewards");
            Check(Find("보상 수령").GetComponent<Button>().interactable,"eligible past season exposes claim action");
            var rules=(MatchRulesContract)typeof(PunNetwork).GetMethod("GuildWarRules",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{new SkillMatchData{HallIdx=1,MatchBall=4,TargetHit=10,MatchTotMin=10,PrizeCoin=1000,MaxCoin=100000}});
            Check(rules.CoinPolicy==1 && rules.EntryCoin==0 && rules.MaxCoin==0,"guild war runtime uses free server settlement rules");
            File.WriteAllText("Logs/Guilds/result.json","{\"Ok\":true,\"Checks\":"+checks+",\"Renders\":"+renders+"}");Debug.Log("[GuildValidation] PASS "+checks+" checks, "+renders+" renders");
            void Set(string name,object value)=>typeof(BottomMenuView).GetField(name,Flags).SetValue(view,value);
            object Get(string name)=>typeof(BottomMenuView).GetField(name,Flags).GetValue(view);
            void Call(string name)=>typeof(BottomMenuView).GetMethod(name,Flags).Invoke(view,null);
            Transform Find(string name)=>view.GetComponentsInChildren<Transform>(true).First(t=>t.name==name && t.gameObject.activeInHierarchy);
            void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
            void Render(string name)
            {
                const int width=1672,height=941;camera.aspect=(float)width/height;camera.orthographicSize=height*.5f;canvasRoot.GetComponent<RectTransform>().sizeDelta=new Vector2(width,height);
                Canvas.ForceUpdateCanvases();foreach(var t in view.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate();
                foreach(var graphic in view.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}
                var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
                var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes("Logs/Guilds/"+name+".png",texture.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);renders++;
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}

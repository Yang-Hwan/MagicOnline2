using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Sight.Surface.Pavilion;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    // The inbox outlives both the results screen and Consist. The server owns unanswered requests.
    public sealed class FriendSocial : MonoBehaviour
    {
        [Serializable] public sealed class Person { public string Id, Name, Status, Relation; public int Avatar, Level; }
        [Serializable] public sealed class People { public Person[] Rows; }
        [Serializable] public sealed class Invite { public string Id, Other, Name, State; public int Hall; public bool Incoming; }
        [Serializable] sealed class Snapshot { public string From, Name; public Invite Invite; }
        [Serializable] sealed class Presence { public bool Available; }
        [Serializable] sealed class Choice { public bool Accept; }
        [Serializable] sealed class RequestReply { public string Message; }
        [Serializable] sealed class ChallengeInput { public string Other; public int Hall; }
        [Serializable] sealed class Ticket { public string Id; }
        public const string Api = "/api/me/friends";
        public static FriendSocial Instance { get; private set; }
        public static BottomMenuView Menu;
        public static RematchLoungeView Lounge;
        Canvas overlay;
        GameObject banner;
        TMP_FontAsset font;
        string account, lastJoined, shown, waiting;
        bool fetching, answering, lastAvailable;
        float next, toastUntil;
        public event Action FriendsChanged;
        bool InMenu => Menu && Menu.isActiveAndEnabled;
        bool Available => InMenu && PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom;
        bool CanNotify => InMenu || (Lounge && Lounge.isActiveAndEnabled);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Instance) new GameObject("Friend Notifications").AddComponent<FriendSocial>();
        }
        void Awake()
        {
            Instance=this;
            DontDestroyOnLoad(gameObject);
            var layer=new GameObject("Friend Banner Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            layer.transform.SetParent(transform,false);
            overlay=layer.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=31000;
            var scaler=layer.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1672,941);scaler.matchWidthOrHeight=.5f;
            font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
        }

        void Update()
        {
            if(account!=GameApiClient.UserId)
            {
                account=GameApiClient.UserId;lastJoined=waiting=null;next=toastUntil=0;Hide();
            }
            if(string.IsNullOrEmpty(account))return;
            bool available=Available;
            if(available!=lastAvailable){lastAvailable=available;next=0;}
            if(!CanNotify && banner)Hide();
            if(toastUntil>0 && Time.unscaledTime>=toastUntil){toastUntil=0;Hide();next=0;}
            if(!fetching && !answering && Time.unscaledTime>=next)Poll(available);
        }

        async void Poll(bool available)
        {
            fetching=true;
            string owner=account;
            next=Time.unscaledTime+(CanNotify?4:15);
            try
            {
                var state=await GameApiClient.Request<Snapshot>(Api+"/pulse","POST",new Presence{Available=available});
                if(!this || owner!=GameApiClient.UserId || !CanNotify || answering)return;
                var invite=state.Invite;
                if(!string.IsNullOrEmpty(invite?.Id) && invite.State=="accepted")
                {
                    if(lastJoined!=invite.Id && Available)
                    {
                        lastJoined=invite.Id;waiting=null;Hide();
                        Menu.EnterFriendly(invite);
                    }
                    return;
                }
                if(toastUntil>Time.unscaledTime)return;
                if(!string.IsNullOrEmpty(invite?.Id) && invite.State=="pending")
                {
                    if(invite.Incoming && Available)
                    {
                        var hall=BackendChart.skillMatchData?.FirstOrDefault(h=>h.HallIdx==invite.Hall);
                        Show(invite.Id,"친선전 요청",$"{invite.Name}님이 도전을 요청했습니다.\n{hall?.HallName} · {hall?.MatchBall}구 · 입장 무료 · 공식 기록 미반영",
                            "수락",()=>Respond(invite.Id,true,true),"거절",()=>Respond(invite.Id,true,false));
                    }
                    else if(!invite.Incoming && InMenu)
                    {
                        waiting=invite.Id;
                        Show(invite.Id,"친선전 요청",$"{invite.Name}님의 응답을 기다리고 있습니다.","요청 취소",()=>Respond(invite.Id,true,false));
                    }
                    return;
                }
                if(waiting!=null){waiting=null;Toast("도전 요청이 종료되었습니다.");return;}
                if(!string.IsNullOrEmpty(state.From))
                    Show(state.From,"친구 요청",$"{state.Name}님의 친구 요청을 수락하시겠습니까?",
                        "수락",()=>Respond(state.From,false,true),"거절",()=>Respond(state.From,false,false));
                else Hide();
            }
            catch(Exception e){if(owner==GameApiClient.UserId)Debug.LogWarning("[Friends] "+e.Message);}
            finally {fetching=false;}
        }

        public async Task SendChallenge(string other,int hall)
        {
            var ticket=await GameApiClient.Request<Ticket>(Api+"/challenge","POST",new ChallengeInput{Other=other,Hall=hall});
            waiting=ticket.Id;next=toastUntil=0;
        }

        public async void Add(string other,Button button,TMP_Text actionLabel=null)
        {
            if(!Guid.TryParse(other,out _))return;
            button.interactable=false;
            try
            {
                var result=await GameApiClient.Request<RequestReply>(Api+"/"+other+"/request","POST");
                if(actionLabel)actionLabel.text=result.Message;
                Toast(result.Message=="요청 중"?"친구 요청을 보냈습니다.":result.Message);
                next=0;
            }
            catch(Exception e){if(button)button.interactable=true;Toast(e.Message);}
        }

        public async Task<bool> CanAdd(string other)
        {
            var relation=await GameApiClient.Request<RequestReply>(Api+"/"+other);
            return string.IsNullOrEmpty(relation.Message);
        }

        async void Respond(string id,bool challenge,bool accept)
        {
            if(answering)return;
            answering=true;
            try
            {
                string path=challenge?"/challenge/"+id:"/"+id+"/answer";
                await GameApiClient.Request<object>(Api+path,"POST",new Choice{Accept=accept});
                Hide();waiting=null;
                if(!challenge && accept){FriendsChanged?.Invoke();Toast("친구가 되었습니다.");}
            }
            catch(Exception e){Toast(e.Message);}
            finally{answering=false;next=0;}
        }
        public void Toast(string message)
        {
            Show("toast:"+message,"알림",message,null,null);
            toastUntil=Time.unscaledTime+3;
        }
        void Hide(){if(banner){banner.SetActive(false);Destroy(banner);}shown=null;}
        void Show(string key,string heading,string message,string yes,Action onYes,string no=null,Action onNo=null)
        {
            if(shown==key && banner)return;
            Hide();shown=key;
            var root=Rect("Friend Notice",overlay.transform,0,0,1000,102);
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,1);root.anchoredPosition=new Vector2(0,-9);
            banner=root.gameObject;
            root.gameObject.AddComponent<RawImage>().texture=Resources.Load<Texture2D>("Menu/Friends/Notice");
            Label(root,heading,157,11,563,26,22,Color.cyan);
            Label(root,message,157,36,yes==null?795:565,54,26,Color.white);
            if(yes!=null)ButtonAt(root,yes,no==null?849:737,onYes,true);
            if(no!=null)ButtonAt(root,no,866,onNo,false);
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        TMP_Text Label(Transform p,string value,float x,float y,float w,float h,int size,Color color)
        {
            var t=Rect("Label",p,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font=font;t.text=value;t.richText=false;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMin=15;t.fontSizeMax=size;
            t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;return t;
        }
        void ButtonAt(Transform p,string value,float x,Action click,bool primary)
        {
            var rect=Rect(value,p,x,25,116,54);
            var bg=rect.gameObject.AddComponent<Image>();bg.color=primary?new Color(1,.85f,.08f):new Color(.19f,.22f,.29f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.onClick.AddListener(()=>click());
            Label(rect,value,0,0,116,54,25,primary?Color.black:Color.white).alignment=TextAlignmentOptions.Center;
        }
    }
}

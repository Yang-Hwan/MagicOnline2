using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Sight.Surface.Consist;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    // Consist remains loaded and subscribed to Photon. Only the local practice scene is disposable.
    public sealed class WaitingRoomPractice : MonoBehaviour
    {
        const string Property = "waitingPractice";
        static WaitingRoomPractice current;
        public static bool Active => current;
        public static bool StartBlocked => Active || (PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Any(
            p => p.CustomProperties.TryGetValue(Property, out var value) && value is bool busy && busy));
        public static bool CanEnter => !Active && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient &&
            PhotonNetwork.PlayerList.Length == 1 && !PhotonNetwork.LocalPlayer.IsInactive &&
            PracticeSceneFlow.OnlineSession == null;
        readonly List<Behaviour> hidden = new List<Behaviour>();
        Scene lobbyScene, practiceScene;
        string roomName;
        bool returnRequested, sawOpponent;
        float originalTimeScale;
        Canvas overlay;
        TMP_Text notice;
        public static void Enter(PnlLobby lobby)
        {
            if (!lobby || !CanEnter || !Application.CanStreamedLevelBeLoaded(PracticeSceneFlow.ScenePath)) return;
            var hall = BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == NetworkManager.hallIdx);
            if (hall == null || (hall.MatchBall != 3 && hall.MatchBall != 4)) return;
            var host = new GameObject("Waiting room practice session");
            SceneManager.MoveGameObjectToScene(host, lobby.gameObject.scene);
            current = host.AddComponent<WaitingRoomPractice>();
            current.lobbyScene = lobby.gameObject.scene;
            current.roomName = PhotonNetwork.CurrentRoom.Name;
            current.originalTimeScale = Time.timeScale;
            SetBusy(true);
            PracticeSceneFlow.ConfigureWaitingPractice(hall.MatchBall);
            current.StartCoroutine(current.Run());
        }
        static void SetBusy(bool value)
        {
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [Property] = value });
        }
        public static bool ReturnToLobby()
        {
            if (!current) return false;
            current.returnRequested = true;
            return true;
        }
        bool Connected => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == roomName;
        bool HasOpponent => Connected && PhotonNetwork.PlayerList.Any(p => !p.IsLocal && !p.IsInactive);
        void Update()
        {
            if (HasOpponent) sawOpponent = true;
            if (!Connected) returnRequested = true;
        }
        void HideLobby()
        {
            foreach (var root in lobbyScene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                    if (component.enabled && (component is Canvas || component is Camera || component is AudioListener || component is EventSystem))
                    { hidden.Add(component); component.enabled = false; }
        }
        void BuildNotice()
        {
            var go = new GameObject("Opponent arrival notice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, lobbyScene);
            go.transform.SetParent(transform,false);
            overlay = go.GetComponent<Canvas>();overlay.renderMode = RenderMode.ScreenSpaceOverlay;overlay.sortingOrder = 32760;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);
            var panel=new GameObject("Shade",typeof(RectTransform),typeof(Image));panel.transform.SetParent(go.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            panel.GetComponent<Image>().color=new Color(.015f,.025f,.04f,.78f);
            var label=new GameObject("Arrival",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(panel.transform,false);
            rect=label.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.08f,.3f);rect.anchorMax=new Vector2(.92f,.7f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            notice=label.GetComponent<TextMeshProUGUI>();notice.font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            notice.fontSize=64;notice.enableAutoSizing=true;notice.fontSizeMin=32;notice.fontSizeMax=64;notice.alignment=TextAlignmentOptions.Center;notice.color=new Color(1,.91f,.62f);notice.raycastTarget=false;
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == PracticeSceneFlow.ScenePath && mode == LoadSceneMode.Additive)
            {
                practiceScene = scene;
                SceneManager.SetActiveScene(scene);
            }
        }
        void FreezePractice()
        {
            Time.timeScale = 0; // Freeze shots and fixed-step routines during the real-time notice.
            Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl.canControl = false;
            foreach(var root in practiceScene.GetRootGameObjects())
                foreach(var component in root.GetComponentsInChildren<Behaviour>(true))
                    if(component is Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng ||
                       component is Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.InputOutput ||
                       component is Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl ||
                       component is Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch ||
                       component is GraphicRaycaster || component is EventSystem)
                        component.enabled = false;
        }
        IEnumerator Run()
        {
            HideLobby();
            SceneManager.sceneLoaded += OnSceneLoaded;
            var load=SceneManager.LoadSceneAsync(PracticeSceneFlow.ScenePath,LoadSceneMode.Additive);
            if(load!=null)yield return load;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            practiceScene=SceneManager.GetSceneByPath(PracticeSceneFlow.ScenePath);
            if(practiceScene.IsValid() && practiceScene.isLoaded)
            {
                SceneManager.SetActiveScene(practiceScene);
                while(!returnRequested && !sawOpponent)yield return null;
                // Freeze the local shot and all practice controls, without touching Photon or the lobby.
                FreezePractice();
                if(sawOpponent && Connected)
                {
                    BuildNotice();
                    float end=Time.realtimeSinceStartup+1.5f;
                    while(Time.realtimeSinceStartup<end && Connected)
                    {
                        notice.text=HasOpponent ? "상대가 입장했습니다!\n<size=36>대전을 준비합니다.</size>" : "상대가 나갔습니다.\n<size=36>대기방으로 돌아갑니다.</size>";
                        yield return null;
                    }
                }
                if(lobbyScene.IsValid() && lobbyScene.isLoaded)SceneManager.SetActiveScene(lobbyScene);
                var unload=SceneManager.UnloadSceneAsync(practiceScene);
                if(unload!=null)yield return unload;
            }
            if(overlay)Destroy(overlay.gameObject);
            Restore();
            Destroy(gameObject);
        }
        void Restore()
        {
            Time.timeScale=originalTimeScale;
            foreach(var component in hidden)if(component)component.enabled=true;
            hidden.Clear();
            PracticeSceneFlow.EndWaitingPractice();
            SetBusy(false);
            if(current==this)current=null;
        }
        void OnDestroy(){SceneManager.sceneLoaded -= OnSceneLoaded;if(current==this)Restore();}
    }
}

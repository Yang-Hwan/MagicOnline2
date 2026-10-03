using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Exert.BackSys;
using Cysharp.Threading.Tasks;

namespace Assets.Scripts.Sight.Surface.Hall
{
    public class PnlLobby : MonoBehaviour
    {

        [SerializeField] GesHall ges;
        [SerializeField] Button btnMain;
        [SerializeField] GameObject boxHall;
        [SerializeField] Transform content;
        [SerializeField] TextMeshProUGUI txtLobby;
        [SerializeField] TextMeshProUGUI txtAll;

        Dictionary<int, int> hallCnt = new Dictionary<int, int>();
        Dictionary<int, BoxHall> hallObj = new Dictionary<int, BoxHall>();


        private void Awake()
        {
            ges = GesHall.FindObjectOfType<GesHall>();
            content = transform.Find("Scroll View/Viewport/Content");
            txtLobby = transform.Find("TxtLobby").GetComponent<TextMeshProUGUI>();
            txtAll = transform.Find("TxtAll").GetComponent<TextMeshProUGUI>();
            btnMain = transform.Find("BtnMain").GetComponent<Button>();
            btnMain.onClick.AddListener(btnMain_Click);
            //Debug.Log("PnlLobby Awake");
        }


        private void OnEnable()
        {
            Debug.Log("PnlLobby OnEnable start start");
            //NetworkManager.network.OnNetworkAllPlayer += NetworkManager_OnNetworkAllPlayer;
            NetworkManager.network.OnNetworkLobbyPlayer += NetworkManager_OnNetworkLobbyPlayer;
            NetworkManager.network.OnNetworkRoomUpdate += NetworkManager_OnNetworkRoomUpdate;
            Debug.Log("PnlLobby OnEnable end end ");

        }


        private void Start()
        {
            int c = BackendChart.skillMatchData.Count;
            Debug.Log($"PnlLobby Start LST CNT : {c}");
            
            hallCnt.Clear();
            hallObj.Clear();
            BackendChart.skillMatchData.ForEach(r =>
            {
                bool isUse = false;
                if (r.PrizeCoin <= NetworkManager.user_coin && NetworkManager.user_coin <= r.MaxCoin)
                {
                    isUse = true;
                }
                int hallIdx = r.HallIdx;
                string title = r.HallName;
                int playerCnt = 0;
                long prize = r.PrizeCoin;
                string playType = r.MatchBall.ToString();
                GameObject box = Instantiate(boxHall);
                box.transform.SetParent(content);
                //Debug.Log($"box hall_idx : {hallIdx}");
                box.GetComponent<BoxHall>().SetHallInfo(this, hallIdx, isUse, title, playerCnt, prize, playType);
                hallCnt.Add(hallIdx, 0);
                hallObj.Add(hallIdx, box.GetComponent<BoxHall>());
            });


            NetworkManager.network.GetRoomsInfo();

        }




  

        private void OnDisable()
        {
            Debug.Log("PnlLobby OnDisable");
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetworkLobbyPlayer))
                existingOnNetworkLobbyPlayer.OnNetworkLobbyPlayer -= NetworkManager_OnNetworkLobbyPlayer;
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetworkRoomUpdate))
                existingOnNetworkRoomUpdate.OnNetworkRoomUpdate -= NetworkManager_OnNetworkRoomUpdate;
        }

        public void GotoWaitRoom(int hallIdx)
        {
            NetworkManager.hallIdx = hallIdx;

            Debug.Log($"GotoWaitRoom hallIdx : {hallIdx}");
            ges.Open(GesHall.Pnl.WaitRoom);
            NetworkManager.network.JoinRandomRoom(hallIdx);
            //WaitRoomOpen().Forget();
        }

        async UniTaskVoid WaitRoomOpen()
        {
            await UniTask.WaitForSeconds(1.5f);
            Debug.Log($"WaitRoomOpen ....... ");
            ges.Open(GesHall.Pnl.WaitRoom);
        }

        void NetworkManager_OnNetworkLobbyPlayer(int playerCnt)
        {
            DelayLobbyPlayer(playerCnt).Forget();
            // Debug.Log("NetworkManager_OnNetworkLobbyPlayer cnt : " + playerCnt);
            //txtLobby.text = playerCnt.ToString();
        }

        async UniTaskVoid DelayLobbyPlayer(int playerCnt)
        {
            await UniTask.WaitForSeconds(1.5f);
            txtLobby.text = playerCnt.ToString();

            //Debug.Log($"DelayRoom ....... ");
            //dic.Keys.ToList().ForEach(k =>
            //{
            //    Debug.Log($"NetworkManager_OnNetworkRoomUpdate key : {k}, val : {dic[k]}");
            //    hallObj[k].SetHallPlayerCnt(dic[k]);
            //});
        }

        async UniTaskVoid DelayRoom(Dictionary<int, int> dic)
        {
            await UniTask.WaitForSeconds(0.5f);
            Debug.Log($"DelayRoom ....... ");
            dic.Keys.ToList().ForEach(k =>
            {
                Debug.Log($"NetworkManager_OnNetworkRoomUpdate key : {k}, val : {dic[k]}");
                hallObj[k].SetHallPlayerCnt(dic[k]);
            });
        }

        void NetworkManager_OnNetworkRoomUpdate(Dictionary<int, int> dic)
        {
            DelayRoom(dic).Forget();
            //dic.Keys.ToList().ForEach(k =>
            //{
            //    Debug.Log($"NetworkManager_OnNetworkRoomUpdate key : {k}, val : {dic[k]}");
            //    hallObj[k].SetHallPlayerCnt(dic[k]);
            //});


            /*
            for (int i = 0; i < roomData.Length; i++)
            {
                int mc = roomData[i].LimitCnt;
                int pc = roomData[i].PlayerCnt;
                string nm = roomData[i].Name.ToString();
                int hx = roomData[i].HallIdx;
                RoomEditType ty = roomData[i].ChangeType;
                Debug.Log($"mc : {mc}, pc : {pc}, nm : {nm}, hx : {hx}, ty : {ty}");
                int cnt = hallCnt[hx];
                if (ty == RoomEditType.Add)
                {
                    cnt = cnt + pc;
                }
                else if (ty == RoomEditType.Edit)
                {

                }
                else if(ty == RoomEditType.Remove)
                {

                }
            }
            */

        }


        void btnMain_Click()
        {
            Debug.Log("btnMain_Click ");
            SceneMove.LoadScene(SceneNames.Consist);
        }

    }
}

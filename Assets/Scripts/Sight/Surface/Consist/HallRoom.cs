using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Often;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class HallRoom : MonoBehaviour
    {

        [SerializeField] Sprite card_off;
        [SerializeField] Sprite card_on;

        TextMeshProUGUI txtTitle;
        TextMeshProUGUI txtPlayerCnt;
        TextMeshProUGUI txtMatchCushion;
        TextMeshProUGUI txtEnterCoin;
        TextMeshProUGUI txtMatchBall;
        TextMeshProUGUI txtTargetHit;
        TextMeshProUGUI txtFinishMission;
        TextMeshProUGUI txtMatchTotMin;

        float card_y_off = -30f;
        float card_y_on = -65f;

        GameObject card;
        GameObject axis;
        Transform traInfo;
        Button btn;
        int num;
        int hallIdx;
        bool isOn;
        bool isLock;
        RoomSwipe __parent;
        private void Awake()
        {


            //Debug.Log();
        }

        private void Init(int i)
        {
           // Debug.Log($"Init I : {i} .. hallIdx : {hallIdx}.. { transform?.name}  ");
            //Debug.Log($" >> transform.GetChild(0) :  {transform.GetChild(0)?.name} ");
            //Debug.Log($" >>>> transform.GetChild(0).GetChild(1)  :  {transform.GetChild(0).GetChild(1)?.name} ");
            traInfo = transform.GetChild(0).GetChild(1);
            txtTitle = traInfo.Find("TxtTitle").GetComponent<TextMeshProUGUI>();
            txtPlayerCnt = traInfo.Find("TxtPlayerCnt").GetComponent<TextMeshProUGUI>();
            txtMatchCushion = traInfo.Find("TxtMatchCushion").GetComponent<TextMeshProUGUI>();
            txtEnterCoin = traInfo.Find("TxtEnterCoin").GetComponent<TextMeshProUGUI>();
            txtMatchBall = traInfo.Find("TxtMatchBall").GetComponent<TextMeshProUGUI>();
            txtTargetHit = traInfo.Find("TxtTargetHit").GetComponent<TextMeshProUGUI>();
            txtFinishMission = traInfo.Find("TxtFinishMission").GetComponent<TextMeshProUGUI>();
            txtMatchTotMin = traInfo.Find("TxtMatchTotMin").GetComponent<TextMeshProUGUI>();
            
            card = transform.GetChild(0).gameObject;
            axis = transform.GetChild(1).gameObject;
            btn = transform.GetChild(0).GetComponent<Button>();
            btn.onClick.AddListener(Btn_Click);
        }

        public void SetNum(int num, RoomSwipe parent)
        {
            this.__parent = parent;
            this.num = num;
            //Debug.Log($"111 HallRoom num : {num}, parent : {parent.name}");
            //txtNum.text = $"{num}";
            //Debug.Log($"card HallRoom num : {num}, childCount : {transform.childCount}");

            if (traInfo == null) Init(11);

            //Debug.Log($"axis HallRoom num : {num}, axis : {axis.name}");

            card.SetActive(false);
            axis.SetActive(false);

            //if (num == 0 || num > 11)
            //{
            //    card.SetActive(false);
            //    axis.SetActive(false);
            //}
            //else
            //{
            //    card.SetActive(true);
            //    axis.SetActive(true);
            //}


        }

        public void SetRoomInfo(int hallIdx, bool isLock, string title, int playerCnt, int cushionCnt, int targetHit, long coin, int matchBall, int finish, int totMin)
        {
            //Debug.Log($"SetRoomInfo hallIdx : {hallIdx},  title : {title},  targetHit : {targetHit},  targetHit : {targetHit} .. ");
            card.SetActive(true);
            axis.SetActive(true);

            string finishStr = string.Empty;
            FinishMission finishMission = (FinishMission)finish;
            switch (finishMission)
            {
                case FinishMission.None:
                    finishStr = "";
                    break;
                case FinishMission.Cushion3:
                    finishStr = "쿠션 마무리";
                    break;
                case FinishMission.Bank1:
                    finishStr = "뱅크 마무리";
                    break;
                case FinishMission.Bank2:
                    finishStr = "투뱅크 마무리";
                    break;
                
            }
            string cushionStr = string.Empty;
            switch (cushionCnt)
            {
                case 0:
                    cushionStr = "사구";
                    break;
                case 1:
                    cushionStr = "원쿠션";
                    break;
                case 2:
                    cushionStr = "투쿠션";
                    break;
                case 3:
                    cushionStr = "쓰리쿠션";
                    break;
            }
            this.hallIdx = hallIdx;
            this.isLock = isLock;
            txtTitle.text = title;
            txtPlayerCnt.text = $"접속: {playerCnt}명";
            txtEnterCoin.text = $"입장: {coin}원";
            txtMatchCushion.text = cushionStr;
            txtMatchBall.text = $"{matchBall}구";
            txtTargetHit.text = $"목표개수: {(matchBall == (int)MatchBall.FourBall ? 3 : targetHit)}개";
            txtFinishMission.text = $"{finishStr}";
            txtMatchTotMin.text = $"제한시간: {totMin}분";
        }

        public void SetHallPlayerCnt(int playerCnt)
        {
            txtPlayerCnt.text = $"접속중인 {playerCnt}";
        }

        public void SetCardOn(bool isOn)
        {
            
            if (!this.isOn && !isOn)
            {
                return;
            }
            if (traInfo == null)
            {
                //Debug.Log($"HallRoom.SetCardOn  if (traInfo == null) hallIdx : {hallIdx} ");
                //Debug.Log($"HallRoom.SetCardOn  .. hallIdx : {hallIdx}.. {(this==null?"xx":"oo")}  ");
                Init(22);
            }

            //txtOnOff.text = $"{isOn}";
            this.isOn = isOn;
            float card_y = isOn ? card_y_on : card_y_off;
            Sprite sprite = isOn ? card_on : card_off;
            card.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, card_y);
            card.GetComponent<Image>().sprite = sprite;
        }

        void Btn_Click()
        {
            if (!isOn)
            {
                if(num < __parent.currentPage + 1)
                {
                    __parent.btnArrLeft_Click();
                }
                else if (num > __parent.currentPage + 1)
                {
                    __parent.btnArrRight_Click();
                }
                //Debug.Log($"num : {num}, currentPage : {__parent.currentPage+1}");
                return;
            }
            //Debug.Log($"Btn_Click num : {num}, hallIdx : {hallIdx}");
            __parent.GotoWaitRoom(this.hallIdx);
        }

    }
}

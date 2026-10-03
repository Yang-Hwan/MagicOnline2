using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.Sight.Surface.Hall
{
    public class BoxHall : MonoBehaviour
    {


        [SerializeField] Image imgBox;
        [SerializeField] Button btnBox;
        [SerializeField] TextMeshProUGUI txtInfo;
        [SerializeField] TextMeshProUGUI txtPlayerCnt;
        [SerializeField] TextMeshProUGUI txtEnterCoin;
        [SerializeField] TextMeshProUGUI txtPlayType;
        [SerializeField] PnlLobby lobby;
        [SerializeField] int hallIdx;

        private void Awake()
        {
            imgBox = GetComponent<Image>();
            btnBox = GetComponent<Button>();
            txtInfo = transform.Find("TxtTitle").GetComponent<TextMeshProUGUI>();
            txtPlayerCnt = transform.Find("TxtPlayerCnt").GetComponent<TextMeshProUGUI>();
            txtEnterCoin = transform.Find("TxtEnterCoin").GetComponent<TextMeshProUGUI>();
            txtPlayType = transform.Find("TxtPlayType").GetComponent<TextMeshProUGUI>();
            btnBox.onClick.AddListener(Box_Click);
        }


        public void SetHallInfo(PnlLobby lobby, int hallIdx, bool isUse, string title, int playerCnt, long coin, string playType)
        {
            this.hallIdx = hallIdx;
            imgBox.color = isUse ? Color.white : Color.gray;
            txtInfo.text = title;
            txtPlayerCnt.text = $"접속중인 {playerCnt}";
            txtEnterCoin.text = $"상금: {coin}";
            txtPlayType.text = $"{playType} 쿠션";
            this.lobby = lobby;
        }

        public void SetHallPlayerCnt(int playerCnt)
        {
            txtPlayerCnt.text = playerCnt.ToString();
        }

        void Box_Click()
        {
            //Debug.Log($"{txtInfo.text} 입장.. {this.hallIdx} ");
            lobby.GotoWaitRoom(this.hallIdx);
        }
    }
}

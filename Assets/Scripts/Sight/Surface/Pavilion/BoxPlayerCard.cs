using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Match;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class BoxPlayerCard : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI txtCoin;
        [SerializeField] TextMeshProUGUI txtNick;
        [SerializeField] TextMeshProUGUI txtWin;
        [SerializeField] TextMeshProUGUI txtMsg;

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            txtNick = transform.Find("TxtNickNm").GetComponent<TextMeshProUGUI>();
            txtCoin = transform.Find("TxtCoin").GetComponent<TextMeshProUGUI>();
            txtWin = transform.Find("TxtWin").GetComponent<TextMeshProUGUI>();
            txtMsg = transform.Find("TxtMsg").GetComponent<TextMeshProUGUI>();
        }


        public void SetPlayerInfo(PoolPlayer player)
        {
            if (!txtCoin)
            {
                Init();
            }

            string nick = player.name;
            string coin = player.coin.ToString();
            string win = player.winCnt.ToString();
            Debug.Log($"SetPlayerInfo name : {nick}, coin : {coin}, win : {win} ");
            string ccc = txtCoin?.text ?? "coin_xxx";
            //Debug.Log("ccc : " + ccc);
            txtCoin.text = coin;
            txtNick.text = nick;
            txtWin.text = win;
            txtMsg.text = "";
        }



        public void SetPlayerMsg(OneMore more)
        {
            string msg = string.Empty;
            switch (more)
            {
                case OneMore.Enable:
                    break;
                case OneMore.Req:
                    msg = "한 게임 더해요";
                    break;
                case OneMore.Disable:
                    msg = "응답할 수 없습니다";
                    break;
                default:
                    break;
            }
            txtMsg.text = msg;

        }

    }
}

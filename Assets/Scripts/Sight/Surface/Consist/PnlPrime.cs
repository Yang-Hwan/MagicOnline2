using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Sight.Surface.Arise;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Exert.BackSys;
using System.Linq;
using Assets.Scripts.Often;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class PnlPrime : MonoBehaviour
    {

        //MyInfo
        [SerializeField] Image imgAvatar;
        [SerializeField] TextMeshProUGUI txtNickNm;
        [SerializeField] TextMeshProUGUI txtLvVal;
        [SerializeField] Slider sliLvProc;
        [SerializeField] Button btnAttendence;
        [SerializeField] Button btnNickNmCh;

        //UserMain
        [SerializeField] TextMeshProUGUI txtJemVal;
        [SerializeField] TextMeshProUGUI txtCoinVal;
        [SerializeField] TextMeshProUGUI txtOutcome;
        [SerializeField] Button btnJemPlus;
        [SerializeField] Button btnCoinPlus;

        //MainMenu 
        [SerializeField] Button btnGear;
        [SerializeField] Button btnPost;
        [SerializeField] Button btnFriend;
        [SerializeField] Button btnShop;

        //MatchMenu
        [SerializeField] Button btnMatchEnter;


        //Shadow
        [SerializeField] GesPopConsist pops;


        private void Awake()
        {
            txtNickNm = transform.Find("PnlMyInfo/TxtNickNm").GetComponent<TextMeshProUGUI>();
            txtLvVal = transform.Find("PnlMyInfo/TxtLvVal").GetComponent<TextMeshProUGUI>();
            imgAvatar = transform.Find("PnlMyInfo/ImgAvatar").GetComponent<Image>();
            sliLvProc = transform.Find("PnlMyInfo/SliLvProc").GetComponent<Slider>();
            btnAttendence = transform.Find("PnlMyInfo/BtnAttendence").GetComponent<Button>();
            btnNickNmCh = transform.Find("PnlMyInfo/BtnNickNmCh").GetComponent<Button>();
            btnAttendence.onClick.AddListener(Attendence_Click);
            btnNickNmCh.onClick.AddListener(NickNmCh_Click);

            txtJemVal = transform.Find("PnlTopUserMain/PnlJem/TxtVal").GetComponent<TextMeshProUGUI>();
            txtCoinVal = transform.Find("PnlTopUserMain/PnlCoin/TxtVal").GetComponent<TextMeshProUGUI>();
            txtOutcome = transform.Find("PnlTopUserMain/PnlMatchOutcome/TxtOutcome").GetComponent<TextMeshProUGUI>();
            btnJemPlus = transform.Find("PnlTopUserMain/PnlJem/plus").GetComponent<Button>();
            btnCoinPlus = transform.Find("PnlTopUserMain/PnlCoin/plus").GetComponent<Button>();
            btnJemPlus.onClick.AddListener(JemPlus_Click);
            btnCoinPlus.onClick.AddListener(CoinPlus_Click);

            btnGear = transform.Find("PnlMainMenu/BtnMenu01").GetComponent<Button>();
            btnPost = transform.Find("PnlMainMenu/BtnMenu02").GetComponent<Button>();
            btnFriend = transform.Find("PnlMainMenu/BtnMenu03").GetComponent<Button>();
            btnShop = transform.Find("PnlMainMenu/BtnMenu04").GetComponent<Button>();
            btnGear.onClick.AddListener(Gear_Click);
            btnPost.onClick.AddListener(Post_Click);
            btnFriend.onClick.AddListener(Frien_Click);
            btnShop.onClick.AddListener(Shop_Click);

            btnMatchEnter = transform.Find("PnlMatchMenu/BtnMatchEnter").GetComponent<Button>();
            btnMatchEnter.onClick.AddListener(MatchEnter_Click);

            pops = transform.parent.parent.Find("GesPopConsist").GetComponent<GesPopConsist>();
        }



        private void Start()
        {
        }

        private void OnEnable()
        {
            BackendDuty bd = BackendDuty.FindObjectOfType<BackendDuty>();
            if (bd == null) return;
            BackendGame.Instance.onGameDataLoadEvent.AddListener(OutputUserMainData);
            OutputNickNm();
        }

        private void OnDisable()
        {
            BackendGame.Instance.onGameDataLoadEvent.RemoveListener(OutputUserMainData);
        }

        public void OutputUserMainData()
        {
            int lvVal = BackendGame.Instance.UserMainData.LvVel;
            int lvProc = BackendGame.Instance.UserMainData.LvProc;
            int lvPlayCnt = BackendChart.commonCodeData.Where(r => r.ComCd.Equals("LvPlayCnt")).Select(r => r.ComVal).FirstOrDefault();
            int avatarIdx = BackendGame.Instance.UserMainData.AvatarIdx;
            long coin = BackendGame.Instance.UserMainData.Coin;
            float proc = lvPlayCnt == 0 ? 0 : lvProc / (float)lvPlayCnt;
            txtLvVal.text = $"Lv.{lvVal}";
            sliLvProc.value = proc;
            Debug.Log($"coin : {coin} ");
        }

        public void OutputNickNm()
        {
            txtNickNm.text = UserInfo.DisplayName;
        }

        public void OutputTopUserMain()
        {
            int jem = BackendGame.Instance.UserMainData.Jem;
            long coin = BackendGame.Instance.UserMainData.Coin;
            int win = BackendGame.Instance.UserMainData.Win;
            int lose = BackendGame.Instance.UserMainData.Lose;
            int score = win + lose;
            float winRate = score == 0 ? 0 : win / (float)score * 100;

            //Debug.Log($"win : {win}");
            //Debug.Log($"score : {score}");
            //Debug.Log($"winRate : {winRate}");
            txtCoinVal.text = coin.ToString();
            txtJemVal.text = jem.ToString();
            txtOutcome.text = $" {win} / {score} ({winRate:F0}%)";

        }

        void MatchEnter_Click()
        {
            SceneMove.LoadScene(SceneNames.Hall);
        }

        void Attendence_Click()
        {
            pops.Open(GesPopConsist.Pop.Attendance);
        }

        void NickNmCh_Click()
        {
            pops.Open(GesPopConsist.Pop.NickCh);
        }



        void JemPlus_Click()
        {

        }

        void CoinPlus_Click()
        {

        }

        void Gear_Click()
        {
            Debug.Log("Gear_Click");
            UserInfo.LoadUserOther();
        }

        void Post_Click()
        {

        }

        void Frien_Click()
        {

        }


        void Shop_Click()
        {
            //replayManager.AddMatch();
            //SceneMove.LoadScene(SceneNames.Replay);

        }

    }
}

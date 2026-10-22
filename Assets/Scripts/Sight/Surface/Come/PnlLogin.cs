using System;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Sight.Surface.Arise;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Come
{
    public class PnlLogin : LoginBase
    {
        [SerializeField] NetworkDuty net;
        [SerializeField] GesMember gesMember;
        [SerializeField] Image imgId, imgPw;
        [SerializeField] TMP_InputField inpId, inpPw;
        [SerializeField] Button btnLogin, btnLoginTemp, btnLoginTemp2, btnGuest, btnEntry, btnHashKey;
        bool busy;
        protected override void Awake()
        {
            base.Awake(); net = FindObjectOfType<NetworkDuty>(); gesMember = transform.parent.GetComponent<GesMember>();
            imgId = transform.Find("InpId").GetComponent<Image>(); inpId = imgId.GetComponent<TMP_InputField>();
            imgPw = transform.Find("InpPw").GetComponent<Image>(); inpPw = imgPw.GetComponent<TMP_InputField>();
            inpPw.contentType = TMP_InputField.ContentType.Password;
            btnLogin = transform.Find("BtnLogin").GetComponent<Button>();
            btnLoginTemp = transform.Find("BtnLoginTemp").GetComponent<Button>(); btnLoginTemp2 = transform.Find("BtnLoginTemp2").GetComponent<Button>();
            btnGuest = transform.Find("BtnGuest").GetComponent<Button>(); btnEntry = transform.Find("BtnEntry").GetComponent<Button>(); btnHashKey = transform.Find("BtnHashKey").GetComponent<Button>();
            btnLogin.onClick.AddListener(() => Authenticate(false));
            // Existing entry button now registers the credentials entered above.
            btnEntry.onClick.AddListener(() => Authenticate(true));
            ConfigureTestLogin(btnLoginTemp, "로그인1", "user01");
            ConfigureTestLogin(btnLoginTemp2, "로그인2", "user02");
            btnGuest.gameObject.SetActive(false); btnHashKey.gameObject.SetActive(false);
        }
        void ConfigureTestLogin(Button button, string label, string loginId)
        {
            button.GetComponentInChildren<TMP_Text>(true).text = label;
            button.gameObject.SetActive(true);
            button.onClick.AddListener(() =>
            {
                if (busy) return;
                inpId.text = loginId;
                inpPw.text = "11111111";
                Authenticate(false);
            });
        }

        void SetLoginButtonsInteractable(bool interactable)
        {
            btnLogin.interactable = btnEntry.interactable = interactable;
            btnLoginTemp.interactable = btnLoginTemp2.interactable = interactable;
        }

        public void BtnGuest_Click() => SetMessage("아이디와 비밀번호로 가입 또는 로그인해 주세요.");
        async void Authenticate(bool register)
        {
            if (busy) return;
            ResetUI(imgId,imgPw);
            if (IsFieldDataEmpty(imgId,inpId.text,"아이디") || IsFieldDataEmpty(imgPw,inpPw.text,"비밀번호")) return;
            busy = true; SetLoginButtonsInteractable(false);
            SetMessage(register ? "가입 중입니다. 비밀번호는 8자 이상이어야 합니다." : "로그인 중입니다.");
            string step = register ? "회원 가입" : "로그인";
            try
            {
                await GameApiClient.Login(inpId.text,inpPw.text,register);
                step = "차트 불러오기";
                await BackendChart.LoadAllAsync();
                step = "출석 정보 불러오기";
                await BackendAttendance.Instance.LoadAsync();
                step = "프로필 불러오기";
                await BackendGame.Instance.LoadAsync();
                if (!this) return;
                inpPw.text = "";
                _ = BackendMatchHistory.Instance;
                BackendDuty.Instance.ConsistPage(0); net.NetworkSetup();
            }
            catch (Exception e)
            {
                if (this) SetMessage(FailureMessage(e, step));
                GameApiClient.Clear();
            }
            finally { busy = false; if(this) SetLoginButtonsInteractable(true); }
        }

        static string FailureMessage(Exception error, string step)
        {
            if (error is GameApiException api)
            {
                if (api.Message == "MatchProtocolMismatch") return api.UserMessage;
                if (api.Status == 0)
                    return "게임 서버에 연결할 수 없습니다. 서버가 실행 중인지 확인한 후 다시 시도해 주세요.";
                if (api.Message == "InvalidLogin")
                    return "아이디 또는 비밀번호가 맞지 않거나 계정이 잠겨 있습니다. 반복 실패했다면 5분 후 다시 시도해 주세요.";
                if (api.Message == "LoginIdTaken")
                    return "이미 가입된 아이디입니다. 로그인 버튼을 눌러 주세요.";
                if (api.Message == "InvalidCredentialsFormat")
                    return "아이디는 영문·숫자·밑줄 3~32자, 비밀번호는 8~128자로 입력해 주세요.";
                if (api.Status == 429)
                    return "요청이 많습니다. 잠시 후 다시 시도해 주세요.";
            }
            return step + " 실패: " + error.Message;
        }
    }
}

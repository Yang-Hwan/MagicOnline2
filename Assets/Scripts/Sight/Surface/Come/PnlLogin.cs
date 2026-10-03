using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BackEnd;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Sight.Surface.Arise;

namespace Assets.Scripts.Sight.Surface.Come
{
    public class PnlLogin : LoginBase 
    {

        [SerializeField] NetworkDuty net;
        [SerializeField] GesMember gesMember;
        [SerializeField] Image imgId;
        [SerializeField] Image imgPw;
        [SerializeField] TMP_InputField inpId;
        [SerializeField] TMP_InputField inpPw;
        [SerializeField] Button btnLogin;
        [SerializeField] Button btnLoginTemp;
        [SerializeField] Button btnLoginTemp2;
        [SerializeField] Button btnGuest;
        [SerializeField] Button btnEntry;
        [SerializeField] Button btnHashKey;

        protected override void Awake()
        {
            base.Awake();
            net = NetworkDuty.FindObjectOfType<NetworkDuty>();
            gesMember = transform.parent.GetComponent<GesMember>();
            imgId = transform.Find("InpId").GetComponent<Image>();
            inpId = transform.Find("InpId").GetComponent<TMP_InputField>();
            imgPw = transform.Find("InpPw").GetComponent<Image>();
            inpPw = transform.Find("InpPw").GetComponent<TMP_InputField>();
            btnLogin = transform.Find("BtnLogin").GetComponent<Button>();
            btnLoginTemp = transform.Find("BtnLoginTemp").GetComponent<Button>();
            btnLoginTemp2 = transform.Find("BtnLoginTemp2").GetComponent<Button>();
            btnGuest = transform.Find("BtnGuest").GetComponent<Button>();
            btnEntry = transform.Find("BtnEntry").GetComponent<Button>();
            btnHashKey = transform.Find("BtnHashKey").GetComponent<Button>();

            btnLogin.onClick.AddListener(btnLogin_Click);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            btnLoginTemp.gameObject.SetActive(true);
            btnLoginTemp2.gameObject.SetActive(true);
            btnLoginTemp.onClick.AddListener(btnLoginTemp_Click);
            btnLoginTemp2.onClick.AddListener(btnLoginTemp2_Click);
#else
            btnLoginTemp.gameObject.SetActive(false);
            btnLoginTemp2.gameObject.SetActive(false);
#endif
            btnGuest.onClick.AddListener(BtnGuest_Click);
            btnEntry.onClick.AddListener(btnEntry_Click);
            btnHashKey.onClick.AddListener(btnHashKey_Click);
        }



        void btnLogin_Click()
        {
            ResetUI(imgId, imgPw);
            if (IsFieldDataEmpty(imgId, inpId.text, "아이디")) return;
            if (IsFieldDataEmpty(imgPw, inpPw.text, "비밀번호")) return;

            btnLogin.interactable = false;

            StartCoroutine(nameof(LoginProcess));

            ResponseToLogin(inpId.text, inpPw.text);
        }

        void btnLoginTemp_Click()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoginTestAccount("user04");
#endif
        }
        void btnLoginTemp2_Click()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoginTestAccount("user02");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void LoginTestAccount(string id)
        {
            if (!btnLogin.interactable) return;
            inpId.text = id;
            inpPw.text = "1111";
            btnLogin_Click();
        }
#endif

        public void BtnGuest_Click()
        {
            Backend.BMember.GuestLogin(callback =>
            {
                if (callback.IsSuccess())
                {
                    BackendGame.Instance.UserMainDataIsExists();
                    //
                    GetLoadAllChart();

                    // 출석부 데이터 불러오기
                    BackendAttendance.Instance.GetAttendanceGive();

                    LoginAfter();
                }
                else
                {
                    Debug.LogWarning(callback.GetMessage());
                }
            });
        }

        void btnEntry_Click()
        {
            gesMember.Open(GesMember.Pnl.Entry);
        }


        void btnHashKey_Click()
        {
            string googlehash = Backend.Utils.GetGoogleHash();
            txtMsg.text = $"__{googlehash}__";

            TextEditor textEditor = new TextEditor();
            textEditor.text = googlehash;
            textEditor.OnFocus();
            textEditor.Copy();

            Debug.Log("구글 해시 키 : " + googlehash);
        }

        IEnumerator LoginProcess()
        {
            float time = 0;
            while (true)
            {
                time += Time.deltaTime;
                SetMessage($"로그인 중입니다. {time:F1}");
                yield return null;
            }
        }

        void GetLoadAllChart()
        {
            // 모든 차트 데이터 불러오기 ---------------- EDIT_TYPE : 1
            BackendChart.LoadAllChart();
        }



        void ResponseToLogin(string id, string pw)
        {
            Debug.Log("[Login] Login requested.");

            var bro = Backend.BMember.CustomLogin(id, pw);
            StopAllCoroutines();
            if (bro.IsSuccess())
            {

                SetMessage($"{inpId.text}님 환영합니다.");

                BackendGame.Instance.UserMainDataIsExists();

                GetLoadAllChart();

                // 출석부 데이터 불러오기
                BackendAttendance.Instance.GetAttendanceGive();

                LoginAfter();
            }
            else
            {
                btnLogin.interactable = true;
                string msg = string.Empty;
                switch (int.Parse(bro.GetStatusCode()))
                {
                    case 401:
                        msg = bro.GetMessage().Contains("customId") ? "존재하지 않는 아이디입니다." : "잘못된 비밀번호 입니다.";
                        break;
                    case 403:
                        msg = bro.GetMessage().Contains("user") ? "차단당한 유저입니다." : "차단당한 디바이스입니다.";
                        break;
                    case 410:
                        msg = "탈퇴가 진행중인 유저입니다.";
                        break;
                }
                Debug.LogError(msg);
                if (msg.Contains("비밀번호"))
                {
                    GuideForIncorrectlyEnteredData(imgPw, msg);
                }
                else
                {
                    GuideForIncorrectlyEnteredData(imgId, msg);
                }
            }



            //Backend.BMember.CustomLogin(id, pw, callback =>
            //{
            //    StopCoroutine(nameof(LoginProcess));

            //    if (callback.IsSuccess())
            //    {
            //        SetMessage($"{inpId.text}님 환영합니다.");

            //        BackendGame.Instance.UserMainDataIsExists();

            //        GetLoadAllChart();

            //        // 출석부 데이터 불러오기
            //        BackendAttendance.Instance.GetAttendanceGive();

            //        LoginAfter();

            //    }
            //    else
            //    {
            //        btnLogin.interactable = true;
            //        string msg = string.Empty;
            //        switch (int.Parse(callback.GetStatusCode()))
            //        {
            //            case 401:
            //                msg = callback.GetMessage().Contains("customId") ? "존재하지 않는 아이디입니다." : "잘못된 비밀번호 입니다.";
            //                break;
            //            case 403:
            //                msg = callback.GetMessage().Contains("user") ? "차단당한 유저입니다." : "차단당한 디바이스입니다.";
            //                break;
            //            case 410:
            //                msg = "탈퇴가 진행중인 유저입니다.";
            //                break;
            //        }

            //        if (msg.Contains("비밀번호"))
            //        {
            //            GuideForIncorrectlyEnteredData(imgPw, msg);
            //        }
            //        else
            //        {
            //            GuideForIncorrectlyEnteredData(imgId, msg);
            //        }
            //    }
            //});
        }


        void LoginAfter()
        {
            BackendDuty.Instance.ConsistPage(0);

            ////Utils.LoadScene(SceneNames.Consist);
            //Debug.Log("PnlLogin LoginAfter.....................");
            net.NetworkSetup();
            //Debug.Log($"UserInDate : { Backend.UserInDate }");
            //Debug.Log($"nickname : { UserInfo.userInfo.nickname }");
            //Debug.Log($"Coin : {BackendGame.Instance.UserMainData.Coin }");
            //Debug.Log($" ========================== ");
            //Debug.Log("PnlLogin GetUserInfoFromBackendSyn....................." + UserInfo.userInfo.nickname);

        }
    }
}

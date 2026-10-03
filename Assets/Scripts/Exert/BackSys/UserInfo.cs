using UnityEngine;
using BackEnd;
using UnityEngine.Events;
using LitJson;
using Assets.Scripts.Prack.BackSys;

namespace Assets.Scripts.Exert.BackSys
{
    public class UserInfo
    {

        public static UserInfoData userInfo;

        // 닉네임을 등록하지 않은 신규 계정도 화면에 이름을 표시한다.
        public static string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(userInfo?.nickname))
                    return userInfo.nickname;
                if (!string.IsNullOrWhiteSpace(userInfo?.gamerId))
                    return userInfo.gamerId;
                return "플레이어";
            }
        }

        static UserInfo()
        {
            userInfo = new UserInfoData();
        }


        public static void LoadUserInfo()
        {
            var bro = Backend.BMember.GetUserInfo();

            if (bro.IsSuccess())
            {
                try
                {
                    JsonData json = bro.GetReturnValuetoJSON()["row"];
                    userInfo.gamerId = json["gamerId"].ToString();
                    userInfo.countryCode = json["countryCode"]?.ToString();
                    userInfo.nickname = json["nickname"]?.ToString();
                    userInfo.inDate = json["inDate"].ToString();
                    userInfo.emailForFindPassword = json["emailForFindPassword"]?.ToString();
                    userInfo.subscriptionType = json["subscriptionType"].ToString();
                    userInfo.federationId = json["federationId"]?.ToString();

                    //onUserInfoEvent?.Invoke();

                    //Debug.Log("user : " + data.ToString() );
                }
                catch (System.Exception e)
                {
                    // 유저 정보를 기본 상태로 설정
                    userInfo.Reset();
                    // try-catch 에러 출력
                    Debug.LogError(e);
                }
            }
            else
            {
                // 유저 정보를 기본 상태로 설정
                // Tip. 일반적으로 오프라인 상태를 대비해 기본적인 정보를 저장해두고 오프라인일 때 불러와서 사용
                userInfo.Reset();
                Debug.LogError(bro.GetMessage());
            }

        }


        public static void LoadUserOther()
        {
            //Where where = new Where();
            //where.Equal("gamerId", uuid);
            //string[] select = { "gamerId", "nickname", "inDate" };
            //var bro = Backend.GameData.GetV2("UserMain", "2023-08-21T00:38:25.827Z", );
            var bro = Backend.Social.GetUserInfoByInDate("2023-08-21T00:29:51.780Z");

            if (bro.IsSuccess())
            {
                JsonData data = bro.GetReturnValuetoJSON()["row"];

                Debug.Log("bro   :" + bro.GetReturnValue());
                Debug.Log("data   :" + data.Count);
                //for (int i = 0; i < data.Count; i++)
                //{
                //    Debug.Log(data[i]);
                //    Debug.Log(data[i]?.ToString());
                //}
                string s1 = data["nickname"]?.ToString();
                string s2 = data["inDate"]?.ToString();
                Debug.Log("LoadUserOther s1 :" + s1 + ", s2 :" + s2);
            }
            else
            {
                // 유저 정보를 기본 상태로 설정
                // Tip. 일반적으로 오프라인 상태를 대비해 기본적인 정보를 저장해두고 오프라인일 때 불러와서 사용
                Debug.LogError(bro.GetMessage());
            }
        }



    }
}

using UnityEngine;
using BackEnd;
using UnityEngine.Events;
using LitJson;

namespace Assets.Scripts.Prack.BackSys
{
    public class BackendGame
    {

        [System.Serializable]
        public class GameDataLoadEvent : UnityEvent { }
        public GameDataLoadEvent onGameDataLoadEvent = new GameDataLoadEvent();
        static BackendGame instance = null;



        public static BackendGame Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new BackendGame();
                }
                return instance;
            }
        }

        private UserMainData userMainData = new UserMainData();
        public UserMainData UserMainData => userMainData;
        private string userMainDataRowInDate = string.Empty;

        /// <summary>
        /// 뒤끝 콘솔 테이블에 새로운 유저 정보 추가
        /// </summary>
        public void UserMainDataInsert()
        {
            // 유저 정보를 초기값으로 설정
            userMainData.Reset();

            Param param = new Param()
            {
                { "NickName", userMainData.NickName },
                { "LvVel", userMainData.LvVel },
                { "LvProc", userMainData.LvProc },
                { "Coin", userMainData.Coin },
                { "Jem", userMainData.Jem },
                { "Win", userMainData.Win },
                { "Lose", userMainData.Lose },
                { "AvatarIdx", userMainData.AvatarIdx },
            };

            Debug.Log("UserMainDataInsert >> " + userMainData.ToString());


            // 첫 번째 매개변수는 뒤끝 콘솔의 "게임 정보 관리" 탭에 생성한 테이블 이름
            Backend.GameData.Insert("UserMain", param, callback =>
            {
                // 게임 정보 추가에 성공했을 때
                if (callback.IsSuccess())
                {
                    userMainDataRowInDate = callback.GetInDate();
                    Debug.Log($"게임 정보 데이터 삽입에 성공했습니다. : {callback}");
                }
                // 실패했을 때
                else
                {
                    Debug.LogError($"게임 정보 데이터 삽입에 실패했습니다. : {callback}");
                }
            });
        }


        public void UserMainDataExistsInsert()
        {

            // 유저 정보를 초기값으로 설정
            userMainData.Reset();

            Param param = new Param()
            {
                { "NickName", "" },
                { "LvVel", userMainData.LvVel },
                { "LvProc", userMainData.LvProc },
                { "Coin", userMainData.Coin },
                { "Jem", userMainData.Jem },
                { "Win", userMainData.Win },
                { "Lose", userMainData.Lose },
                { "AvatarIdx", userMainData.AvatarIdx },
            };

            //Debug.Log("UserMainDataInsert >> " + userMainData.ToString());
            SendQueue.Enqueue(
                Backend.GameData.GetMyData, "UserMain", new Where(),
                callback =>
                {
                    if (callback.IsSuccess())
                    {
                        try
                        {
                            LitJson.JsonData userMainDataJson = callback.FlattenRows();
                            if (userMainDataJson.Count > 0)
                            {

                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError(e);
                        }
                    }
                });



            Backend.GameData.Insert("UserMain", param, callback =>
            {
                // 게임 정보 추가에 성공했을 때
                if (callback.IsSuccess())
                {
                    userMainDataRowInDate = callback.GetInDate();
                    Debug.Log($"게임 정보 데이터 삽입에 성공했습니다. : {callback}");
                }
                // 실패했을 때
                else
                {
                    Debug.LogError($"게임 정보 데이터 삽입에 실패했습니다. : {callback}");
                }
            });
        }

        /// <summary>
        /// 데이터가 없다면 추가.
        /// </summary>
        public void UserMainDataExists()
        {
            Backend.GameData.GetMyData("UserMain", new Where(), callback =>
            {
                if (callback.IsSuccess())
                {
                    //try
                    //{
                    LitJson.JsonData userMainDataJson = callback.FlattenRows();
                    if (userMainDataJson.Count <= 0)
                    {
                        UserMainDataExistsInsert();
                    }
                    //}
                    //catch (System.Exception e)
                    //{
                    //    Debug.LogError(e);
                    //}
                }
            }
            );
        }


        /// <summary>
        /// 자신의 정보 col(owner_inDate): val( Backend.UserInDate ) Debug.Log("UserMainOther : " + Backend.UserInDate.ToString()); 
        /// </summary>
        /// <param name="owner_inDate"></param>
        public UserMainData UserMainOther(string owner_inDate)
        {
            UserMainData user = new UserMainData();
            //Debug.Log("UserMainOther====================================== ");
            var bro_m = Backend.Social.GetUserInfoByInDate(owner_inDate);
            if (bro_m.IsSuccess())
            {
                JsonData data = bro_m.GetReturnValuetoJSON()["row"];
                if (data.Count != 0)
                {
                    string nickname = data["nickname"]?.ToString();
                    user.NickName = nickname;
                    //Debug.Log(nickname);
                }
            }
            Where where = new Where();
            where.Equal("owner_inDate", owner_inDate);
            string[] select = { "Coin", "AvatarIdx", "Lose", "Win", "LvVel" };
            var bro = Backend.GameData.Get("UserMain", where, select);
            //Debug.Log($". GetUserMain user : " + bro.GetReturnValue());
            if (bro.IsSuccess())
            {
                JsonData data = bro.GetReturnValuetoJSON()["rows"];
                //Debug.Log(">>>>> gamer_id : " + owner_inDate + ", data.Count : " + data.Count);
                for (int i = 0; i < data.Count; i++)
                {
                    if (i != 0) break;
                    int avatarIdx = int.Parse(data[i]["AvatarIdx"]["N"].ToString());
                    int coin = int.Parse(data[i]["Coin"]["N"].ToString());      // 10억이상인 경우 대비해 소스 수정필요.
                    int win = int.Parse(data[i]["Win"]["N"].ToString());
                    int lose = int.Parse(data[i]["Lose"]["N"].ToString());
                    int lvVel = int.Parse(data[i]["LvVel"]["N"].ToString());
                    user.AvatarIdx = avatarIdx;
                    user.Coin = coin;
                    user.Win = win;
                    user.Lose = lose;
                    user.LvVel = lvVel;

                    //Debug.Log($"coin : {coin}, avatarIdx {avatarIdx} , win {win} , lose {lose} , lvVel {lvVel} ");
                }
            }
            else
            {
                Debug.LogWarning($"GetUserMain user : " + bro);
            }

            return user;
        }

        public void UserMainDataIsExists()
        {
            var bro = Backend.GameData.GetMyData("UserMain", new Where());
            if (bro.IsSuccess())
            {
                if (bro.Rows().Count == 0)
                {
                    //Debug.Log("-- UserMainDataIsExists : " + bro.Rows().Count);
                    UserMainDataInsert();
                }
                else
                {
                    //Debug.Log("** UserMainDataIsExists : " + bro.Rows().Count);
                }
            }
        }

        public void UserMainDataLoad()
        {
            var bro = Backend.GameData.GetMyData("UserMain", new Where());
            // 게임 정보 불러오기에 성공했을 때
            if (bro.IsSuccess())
            {
                //Debug.Log($"게임 정보 데이터 불러오기에 성공했습니다. : {callback}");

                // JSON 데이터 파싱 성공
                try
                {
                    LitJson.JsonData userMainDataJson = bro.FlattenRows();
                    // 받아온 데이터의 개수가 0이면 데이터가 없는 것
                    if (userMainDataJson.Count <= 0)
                    {
                        //Debug.LogWarning("데이터가 존재하지 않습니다.");
                    }
                    else
                    {
                        // 불러온 게임정보의 고유값
                        userMainDataRowInDate = userMainDataJson[0]["inDate"].ToString();
                        // 불러온 게임 정보를 UserMainData 변수의 저장
                        userMainData.LvVel = int.Parse(userMainDataJson[0]["LvVel"].ToString());
                        userMainData.LvProc = int.Parse(userMainDataJson[0]["LvProc"].ToString());
                        userMainData.Coin = long.Parse(userMainDataJson[0]["Coin"].ToString());
                        userMainData.Jem = int.Parse(userMainDataJson[0]["Jem"].ToString());
                        userMainData.Win = int.Parse(userMainDataJson[0]["Win"].ToString());
                        userMainData.Lose = int.Parse(userMainDataJson[0]["Lose"].ToString());
                        userMainData.AvatarIdx = int.Parse(userMainDataJson[0]["AvatarIdx"].ToString());

                        onGameDataLoadEvent?.Invoke();
                    }
                }
                // JSON 데이터 파싱 실패
                catch (System.Exception e)
                {
                    // 유저 정보를 초기값으로 설정
                    userMainData.Reset();
                    // try-catch 에러 출력
                    Debug.LogError(e);
                }
            }

        }

        public void UserMainDataUpdate(UnityAction action = null)
        {
            if (userMainData == null)
            {
                Debug.LogError("서버에서 다운받거나 새로 삽입한 데이터가 존재하지 않습니다." +
                               "Insert 혹은 Load를 통해 데이터를 생성해주세요.");
                return;
            }

            Param param = new Param()
            {
                { "LvVel", userMainData.LvVel },
                { "LvProc", userMainData.LvProc },
                { "Coin", userMainData.Coin },
                { "Jem", userMainData.Jem },
                { "Win", userMainData.Win },
                { "Lose", userMainData.Lose },
                { "AvatarIdx", userMainData.AvatarIdx },
            };

            // 게임 정보의 고유값(gameDataRowInDate)이 없으면 에러 메시지 출력
            if (string.IsNullOrEmpty(userMainDataRowInDate))
            {
                Debug.LogError($"유저의 inDate 정보가 없어 게임 정보 데이터 수정에 실패했습니다.");
            }
            // 게임 정보의 고유값이 있으면 테이블에 저장되어 있는 값 중 inDate 컬럼의 값과
            // 소유하는 유저의 owner_inDate가 일치하는 row를 검색하여 수정하는 UpdateV2() 호출
            else
            {
                Debug.Log($"{userMainDataRowInDate}의 게임 정보 데이터 수정을 요청합니다.");
                Backend.GameData.UpdateV2("UserMain", userMainDataRowInDate, Backend.UserInDate, param, callback =>
                {
                    if (callback.IsSuccess())
                    {
                        Debug.Log($"게임 정보 데이터 수정에 성공했습니다. : {callback}");
                        action?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"게임 정보 데이터 수정에 실패했습니다. : {callback}");
                    }
                });
            }

        }
    }
}

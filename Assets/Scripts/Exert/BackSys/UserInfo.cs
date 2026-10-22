using Assets.Scripts.Prack.BackSys;
namespace Assets.Scripts.Exert.BackSys
{
    public class UserInfo
    {
        public static UserInfoData userInfo = new UserInfoData();
        public static string DisplayName => !string.IsNullOrWhiteSpace(userInfo.nickname) ? userInfo.nickname : GameApiClient.LoginId;
        public static void LoadUserInfo()
        {
            userInfo.inDate = GameApiClient.UserId; userInfo.gamerId = GameApiClient.LoginId;
            userInfo.nickname = BackendGame.Instance.UserMainData.NickName;
            userInfo.subscriptionType = "LocalApi";
        }
        public static void LoadUserOther() { }
    }
}

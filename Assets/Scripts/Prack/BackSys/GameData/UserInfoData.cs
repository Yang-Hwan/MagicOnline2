namespace Assets.Scripts.Prack.BackSys
{
    public class UserInfoData
    {

		public string gamerId;              // 유저의 gamerID
		public string countryCode;          // 국가코드. 설정 안했으면 null
		public string nickname;             // 닉네임. 설정 안했으면 null
		public string inDate;                   // 유저의 inDate
		public string emailForFindPassword; // 이메일주소. 설정 안했으면 null
		public string subscriptionType;     // 커스텀, 페더레이션 타입
		public string federationId;         // 구글, 애플, 페이스북 페더레이션 ID. 커스텀 계정은 null

		public void Reset()
		{
			gamerId = "Offline";
			countryCode = "Unknown";
			nickname = "Noname";
			inDate = string.Empty;
			emailForFindPassword = string.Empty;
			subscriptionType = string.Empty;
			federationId = string.Empty;
		}


		public override string ToString()
		{
			string result = string.Empty;
			result += $"gamerId : {gamerId}, ";
			result += $"nickname : {nickname}";

			return result;
		}
	}
}

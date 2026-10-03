using UnityEngine;
using BackEnd;
using Assets.Scripts.Exert.Network;
using Cysharp.Threading.Tasks;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Often;

namespace Assets.Scripts.Sight.Surface.Come
{
    public class NetworkDuty : MonoBehaviour
    {
        NetworkState state;
        NetworkEngine subscribedNetwork;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

        }

        private void OnEnable()
        {
            subscribedNetwork = NetworkManager.network;
            subscribedNetwork.OnNetwork += NetworkManager_network_OnNetwork;
            NetworkManager.OnMainPlayerLoaded += NetworkManager_OnMainPlayerLoaded;
        }

        private void OnDisable()
        {
            if (subscribedNetwork) subscribedNetwork.OnNetwork -= NetworkManager_network_OnNetwork;
            subscribedNetwork = null;
            NetworkManager.OnMainPlayerLoaded -= NetworkManager_OnMainPlayerLoaded;
        }




        void NetworkManager_network_OnNetwork(NetworkState state)
        {
            Debug.Log($"NetworkDuty NetworkManager_network_OnNetwork this.state : {this.state}, state : {state}");
            this.state = state;
            switch (this.state)
            {
                case NetworkState.Connected:
                    NetworkManager.LoadMainPlayer().Forget();
                    break;

            }
        }


        void NetworkManager_OnMainPlayerLoaded(PlayerProfile player)
        {
            string str = $"  ... Nick : {UserInfo.userInfo.nickname}, {BackendGame.Instance.UserMainData.ToString()}";
            Debug.Log($"NetworkDuty NetworkManager_OnMainPlayerLoaded OK OK OK OK OK OK NEXT SCENE {str} .. SceneNames.Consist : {SceneNames.Consist}");

            PoolPlayer.OnMainPlayerLoaded(0, UserInfo.DisplayName, BackendGame.Instance.UserMainData.Coin, Backend.UserInDate, player.image, player.imageURL, player.prize);
            SceneMove.LoadScene(SceneNames.Consist);
        }

        public void NetworkSetup()
        {
            UserInfo.LoadUserInfo();
            BackendGame.Instance.UserMainDataLoad();

            NetworkManager.uuid = Backend.UserInDate;
            NetworkManager.nickName = UserInfo.DisplayName;
            NetworkManager.user_coin = BackendGame.Instance.UserMainData.Coin;
            NetworkManager.network.Connect();
            NetworkManager.initialized = true;
            state = NetworkManager.network.state;
            Debug.Log("NetworkSetup userid : " + UserInfo.userInfo.gamerId + ", nick : " + UserInfo.userInfo.nickname +  ", coin  : " + NetworkManager.user_coin + ", state : " + state.ToString());
        }

    }
}

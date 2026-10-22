using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Events;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Exert.Match;

namespace Assets.Scripts.Prack.BackSys
{
    // Compatibility facade for existing scene references; all persistence now uses GameApi.
    public class BackendGame
    {
        public class GameDataLoadEvent : UnityEvent { }
        public GameDataLoadEvent onGameDataLoadEvent = new GameDataLoadEvent();
        static readonly BackendGame instance = new BackendGame();
        public static BackendGame Instance => instance;
        public UserMainData UserMainData { get; private set; } = new UserMainData();
        readonly Dictionary<string, UserMainData> players = new Dictionary<string, UserMainData>();
        public async Task LoadAsync()
        {
            string owner = GameApiClient.UserId;
            var data = await GameApiClient.Request<UserMainData>("/api/me");
            if (owner != GameApiClient.UserId) return;
            UserMainData = data; UserInfo.LoadUserInfo();
            NetworkManager.user_coin = data.Coin;
            SyncDisplayedCoin(data);
            onGameDataLoadEvent.Invoke();
        }
        public async Task<UserMainData> LoadOtherAsync(string id)
        {
            var data = await GameApiClient.Request<UserMainData>("/api/players/" + Uri.EscapeDataString(id));
            players[id] = data; SyncDisplayedCoin(data); return data;
        }
        static void SyncDisplayedCoin(UserMainData data)
        {
            if(NetworkManager.mainPlayer?.uuid == data.InDate) NetworkManager.mainPlayer.SetServerCoin(data.Coin);
            if(NetworkManager.opponentPlayer?.uuid == data.InDate) NetworkManager.opponentPlayer.SetServerCoin(data.Coin);
            if(PoolPlayer.players != null)
                foreach(var player in PoolPlayer.players)
                    if(player?.uuid == data.InDate) player.SetCoin(data.Coin);
        }
        public UserMainData UserMainOther(string id) => players.TryGetValue(id, out var user) ? user : new UserMainData { InDate = id, NickName = "플레이어" };
        public async void UserMainDataLoad() { try { await LoadAsync(); } catch (Exception e) { UnityEngine.Debug.LogWarning(e.Message); } }
        // Users/wallets are created together by the server's registration transaction.
        public void UserMainDataInsert() => UserMainDataLoad();
        public void UserMainDataExistsInsert() => UserMainDataLoad();
        public void UserMainDataExists() => UserMainDataLoad();
        public void UserMainDataIsExists() { }
        [Serializable] sealed class Edit { public string NickName; public int AvatarIdx; }
        public async void UserMainDataUpdate(UnityAction action = null)
        {
            try
            {
                await GameApiClient.Request<UserMainData>("/api/me/profile", "PATCH", new Edit { NickName = UserMainData.NickName, AvatarIdx = UserMainData.AvatarIdx });
                await LoadAsync(); action?.Invoke();
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning(e.Message); }
        }
    }
}

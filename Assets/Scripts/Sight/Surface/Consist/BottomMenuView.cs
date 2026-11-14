using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Sight.Surface.Arise;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    /// <summary>Approved 1672x941 art with native, live UI. Never displays mock wallet/ranking/price data.</summary>
    public sealed partial class BottomMenuView : MonoBehaviour
    {
        public const float Width = 1672, Height = 941;
        static readonly string[] ArtNames = { "home", "single", "multiplayer", "ranking", "shop" };
        static readonly string[] Labels = { "메인", "싱글플레이", "멀티플레이", "순위", "상점" };
        static readonly Color Yellow = new Color(1, .9f, 0);
        readonly Texture2D[] plates = new Texture2D[5];
        readonly List<TMP_Text> navLabels = new List<TMP_Text>();
        readonly Dictionary<int, int> roomCounts = new Dictionary<int, int>();
        RectTransform design, body, modal, progress, selection;
        TMP_FontAsset font;
        Material textMaterial;
        TMP_Text nickname, level, gems, coins;
        RawImage backplate, navIcon;
        RoomSwipe legacyRooms;
        NetworkEngine subscribedNetwork;
        bool initialized;
        int selectedHall, rankFilter, shopFilter;
        bool profileSaving;
        bool walletRefreshPending, bustCheckPending;
        RectTransform bustNotice;
        Coroutine bustNoticeAnimation;
        float nextWalletRefresh;
        public int CurrentPage { get; private set; } = -1;
        public RectTransform DesignRoot => design;

        public static BottomMenuView Install(MainSwipe legacy)
        {
            var host = legacy.transform;
            while (host && host.name != "PnlMainSwipe") host = host.parent;
            if (!host) return null;
            var existing = host.GetComponentInChildren<BottomMenuView>(true);
            if (existing) return existing;
            // Validate all required assets before disabling any legacy UI.
            var art = ArtNames.Select(n => Resources.Load<Texture2D>("Menu/" + n)).ToArray();
            var korean = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if (art.Any(t => !t) || !korean) { Debug.LogError("Bottom menu assets are missing; keeping legacy menu."); return null; }
            var rooms = host.GetComponentInChildren<RoomSwipe>(true);
            foreach (Transform child in host) child.gameObject.SetActive(false);
            var obj = new GameObject("BottomMenu", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(host, false);
            Stretch((RectTransform)obj.transform);
            obj.GetComponent<Image>().color = Color.black;
            var view = obj.AddComponent<BottomMenuView>();
            view.font = korean; view.legacyRooms = rooms;
            Array.Copy(art, view.plates, art.Length);
            view.Build();
            return view;
        }

        void Build()
        {
            // The legacy font material has a fully dilated black underlay. It closes
            // Korean counters when the face itself is black on the new yellow ribbons.
            textMaterial = new Material(font.material);
            textMaterial.DisableKeyword("UNDERLAY_ON"); textMaterial.DisableKeyword("UNDERLAY_INNER");
            textMaterial.SetFloat("_OutlineWidth", 0); textMaterial.SetFloat("_FaceDilate", 0);
            textMaterial.SetFloat("_UnderlayDilate", 0);
            design = Node("Design", transform, 0, 0, Width, Height);
            design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
            design.anchoredPosition = Vector2.zero;
            backplate = Art("Backplate", design, plates[0], 0, 0, Width, Height);
            // A shared header/footer avoids any generative drift between pages.
            Crop("SharedHeader", design, plates[0], new Rect(0, 0, Width, 127));
            Crop("SharedFooter", design, plates[0], new Rect(0, 837, Width, 104));
            nickname = Text("Nickname", design, "", 187, 15, 430, 52, 39, Color.white, TextAlignmentOptions.MidlineLeft);
            nickname.enableAutoSizing = true; nickname.fontSizeMin = 22; nickname.fontSizeMax = 39;
            Box("ProgressTrack", design, 185, 73, 373, 27, new Color(.05f, .05f, .05f));
            level = Text("Level", design, "", 188, 67, 80, 37, 22, Color.white, TextAlignmentOptions.MidlineLeft);
            Box("ProgressEmpty", design, 275, 80, 274, 14, new Color(.22f, .22f, .22f));
            progress = Box("ProgressFill", design, 275, 80, 0, 14, new Color(.36f, .84f, .12f)).rectTransform;
            // Replace the compact painted currency slots with wider native slots.
            // Keep the approved gem/coin/+ artwork using UVs from the shared header.
            Box("WalletBackground", design, 775, 8, 760, 86, new Color(.015f, .015f, .02f));
            HeaderIcon("GemIcon", new Rect(941, 16, 88, 70), 780, 20, 70, 62);
            HeaderIcon("CoinIcon", new Rect(1246, 16, 74, 70), 1112, 20, 64, 62);
            HeaderIcon("GemPlusIcon", new Rect(1167, 23, 55, 55), 1052, 29, 48, 48);
            HeaderIcon("CoinPlusIcon", new Rect(1167, 23, 55, 55), 1475, 29, 48, 48);
            gems = Text("Gems", design, "", 855, 24, 190, 58, 32, Color.white, TextAlignmentOptions.MidlineRight);
            coins = Text("Coins", design, "", 1183, 24, 282, 58, 32, Color.white, TextAlignmentOptions.MidlineRight);
            foreach (var t in new[] { gems, coins })
            {
                t.enableAutoSizing = true; t.fontSizeMin = 22; t.fontSizeMax = 32;
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Hit("Profile", design, 50, 10, 550, 105, () => OpenPopup(GesPopConsist.Pop.NickCh));
            Hit("GemStore", design, 1048, 16, 60, 75, () => { shopFilter = 1; SelectPage(4); });
            Hit("CoinStore", design, 1470, 16, 60, 75, () => SelectPage(4));
            Hit("Settings", design, 1540, 10, 104, 98, Settings);
            body = Node("PageContent", design, 0, 127, Width, 710);
            // Body uses full-screen pixel coordinates so artists and code share the same measurements.
            Place(body, 0, 0, Width, Height);
            body.SetSiblingIndex(1);
            navIcon = Crop("SelectedIcon", design, plates[0], new Rect(115, 850, 80, 72));
            navIcon.color = Yellow;
            selection = Box("SelectedTab", design, 54, 927, 266, 7, Yellow).rectTransform;
            for (int i = 0; i < 5; i++)
            {
                int page = i;
                float[] tx = { 195, 480, 810, 1170, 1490 };
                float[] tw = { 100, 176, 183, 110, 110 };
                navLabels.Add(Text("NavLabel" + i, design, Labels[i], tx[i], 860, tw[i], 55, 28, Color.white));
                Hit("Nav" + i, design, i * Width / 5, 838, Width / 5, 103, () => SelectPage(page));
            }
            initialized = true;
            RefreshUser(); Resize(); Subscribe();
            int initial = PracticeSceneFlow.ReturnToPracticeMenu ? 1 : (BackendDuty.Instance != null ? BackendDuty.Instance.mainP : 0);
            SelectPage(Mathf.Clamp(initial, 0, 4));
            if (Application.isPlaying) PracticeSceneFlow.ConsumeMenuReturn();
        }

        public void SelectPage(int page)
        {
            if (!initialized) return;
            bool enteringHome = page == 0 && CurrentPage != 0;
            bool enteringMultiplayer = page == 2 && CurrentPage != 2;
            CurrentPage = Mathf.Clamp(page, 0, 4);
            if (CurrentPage != 2 && bustNotice) HideBustNotice();
            CloseModal(); Clear(body);
            backplate.texture = plates[CurrentPage];
            for (int i = 0; i < navLabels.Count; i++) navLabels[i].color = i == CurrentPage ? Yellow : Color.white;
            float[] ix = { 115, 404, 728, 1093, 1417 };
            var crop = new Rect(ix[CurrentPage], 850, 80, 72);
            Place(navIcon.rectTransform, crop.x, crop.y, crop.width, crop.height);
            navIcon.uvRect = UV(crop);
            Place(selection, CurrentPage * Width / 5 + 22, 927, Width / 5 - 44, 7);
            if (CurrentPage == 0) Home();
            else if (CurrentPage == 1) Single();
            else if (CurrentPage == 2) Multiplayer();
            else if (CurrentPage == 3) Ranking();
            else Shop();
            RefreshUser();
            if (enteringHome) LoadMailbox();
            if (enteringMultiplayer) CheckBustOnMultiplayerEntry();
            RefreshWalletFromServer();
        }

        void HeaderIcon(string name, Rect source, float x, float y, float width, float height)
        {
            var icon = Crop(name, design, plates[0], source);
            Place(icon.rectTransform, x, y, width, height);
        }

        void Home()
        {
            string[] labels = { "우편함", "보유장비", "선물함", "출석부", "친구", "길드" };
            for (int i = 0; i < labels.Length; i++)
            {
                int action = i; float x = new[] { 705f, 1030, 1360 }[i % 3]; float y = i < 3 ? 160 : 450;
                Hit("HomeAction" + i, body, x, y, 280, 255, () => HomeAction(action));
                Text("ActionLabel" + i, body, labels[i], x + 16, y + 191, 248, 69, 40, Color.white);
            }
            BuildMailBadge();
        }

        void HomeAction(int action)
        {
            if (action == 0) { OpenMailbox(); return; }
            if (action == 1) { OpenInventory(); return; }
            if (action == 3) { OpenPopup(GesPopConsist.Pop.Attendance); return; }
            if (action == 4) { OpenFriends(); return; }
            if (action == 5)
            {
                OpenGuild();
                return;
            }
            string[] labels = { "우편함", "보유장비", "선물함", "출석부", "친구" };
            Dialog(labels[action], "서비스 준비 중입니다.");
        }

        void Single()
        {
            string[] labels = { "3구 연습", "4구 연습", "개별 리플레이", "시합 리플레이" };
            string[] subtitles = { "자유롭게 배치하고 연습", "기본기부터 차근차근", "저장한 연습 다시 보기", "멀티플레이 경기 다시 보기" };
            var modes = new[] { PracticeSceneFlow.Mode.ThreeBall, PracticeSceneFlow.Mode.FourBall, PracticeSceneFlow.Mode.Replay, PracticeSceneFlow.Mode.MatchReplay };
            for (int i = 0; i < 4; i++)
            {
                var mode = modes[i]; float x = i % 2 == 0 ? 420 : 920; float y = i < 2 ? 275 : 540;
                Hit("Practice" + i, body, x, y, 430, 255, () => PracticeSceneFlow.Enter(mode));
                Text("PracticeLabel" + i, body, labels[i], x + 20, y + (i < 2 ? 130 : 150), 390, 55, 43, Color.black);
                Text("PracticeDescription" + i, body, subtitles[i], x + 10, y + (i < 2 ? 193 : 201), 410, 42, 25, Color.white);
            }
        }

        public static bool CanEnter(SkillMatchData hall, long balance) => hall != null && balance >= hall.PrizeCoin && balance <= hall.MaxCoin;

        void Multiplayer()
        {
            var halls = BackendChart.skillMatchData;
            selectedHall = Mathf.Clamp(selectedHall, 0, Mathf.Max(0, halls.Count - 1));
            for (int slot = 0; slot < 3; slot++)
            {
                int index = selectedHall + slot - 1;
                var hall = index >= 0 && index < halls.Count ? halls[index] : null;
                float x = new[] { 334f, 695, 1066 }[slot];
                if (hall == null)
                {
                    Box("EmptyArena" + slot, body, x, 382, 278, 350, new Color(.015f, .07f, .12f, .95f));
                    Text("NoArena" + slot, body, halls.Count == 0 && slot == 1 ? "경기장 정보를\n불러오지 못했습니다" : "—", x + 5, 470, 268, 100, 27, Color.white);
                    continue;
                }
                Text("ArenaTitle" + slot, body, hall.HallName, x + 3, 382, 274, 60, 35, Color.white, autoSize: true);
                string count = roomCounts.TryGetValue(hall.HallIdx, out int n) ? n + "명" : "확인 중";
                string[] finish = { "", "쿠션 마무리", "뱅크 마무리", "투뱅크 마무리" };
                string description = $"접속  {count}\n입장  {hall.PrizeCoin:N0} 코인\n규칙  {hall.MatchBall}구\n목표  {(hall.TargetHit)}개\n제한시간  {hall.MatchTotMin}분\n{finish[Mathf.Clamp(hall.FinishMission, 0, 3)]}";
                var info = Text("ArenaInfo" + slot, body, description, x + 18, 450, 244, 213, 27, Color.white, TextAlignmentOptions.TopLeft, true);
                info.lineSpacing = 10;
                bool allowed = CanEnter(hall, BackendGame.Instance.UserMainData.Coin);
                string label = slot == 1 ? (allowed ? "입장하기" : "입장 조건 확인") : "선택";
                int chosen = index; var chosenHall = hall;
                Hit("ArenaAction" + slot, body, x, 667, 278, 64, () =>
                {
                    if (chosen != selectedHall) { selectedHall = chosen; SelectPage(2); }
                    else EnterArena(chosenHall);
                });
                Text("ArenaActionLabel" + slot, body, label, x + 5, 672, 268, 52, 33, slot == 1 ? Color.black : Color.white);
            }
            Hit("PreviousArena", body, 220, 425, 78, 110, () => { selectedHall = Mathf.Max(0, selectedHall - 1); SelectPage(2); });
            Hit("NextArena", body, 1382, 425, 80, 110, () => { selectedHall = Mathf.Min(Mathf.Max(0, halls.Count - 1), selectedHall + 1); SelectPage(2); });
            // Replace the mockup's three static dots with the real page index.
            Box("ArenaPaginationBackground", body, 775, 760, 130, 32, new Color(.04f, .04f, .04f));
            Text("ArenaPagination", body, halls.Count == 0 ? "0 / 0" : $"{selectedHall + 1} / {halls.Count}", 775, 759, 130, 33, 23, Yellow);
            Text("ArenaHint", body, "경기장 입장 후 상대를 기다립니다", 450, 795, 770, 36, 24, Color.white);
        }

        void EnterArena(SkillMatchData hall)
        {
            if (!CanEnter(hall, BackendGame.Instance.UserMainData.Coin))
            { Dialog("입장 조건", $"보유 코인이 {hall.PrizeCoin:N0} ~ {hall.MaxCoin:N0} 코인인 경우 입장할 수 있습니다."); return; }
            if (!legacyRooms || !NetworkManager.TryGetExistingNetwork(out var network))
            { Dialog("경기장", "서버 연결을 확인한 후 다시 시도해 주세요."); return; }
            // Keep the original matchmaking, waiting room, cancellation and match transition.
            legacyRooms.GotoWaitRoom(hall.HallIdx);
        }

        void Ranking()
        {
            string[] tabs = { "전체", "3구", "4구" };
            for (int i = 0; i < 3; i++)
            {
                int f = i;
                SolidButton(tabs[i], body, 452 + i * 253, 207, 245, 48, () => { rankFilter = f; SelectPage(3); }, rankFilter == i);
            }
            Text("ColumnRank", body, "순위", 390, 280, 150, 43, 28, Color.white);
            Text("ColumnName", body, "닉네임", 615, 280, 250, 43, 28, Color.white);
            Text("ColumnWins", body, "승리", 932, 280, 160, 43, 28, Color.white);
            Text("ColumnRate", body, "승률", 1130, 280, 170, 43, 28, Color.white);
            for (int i = 0; i < 5; i++)
            {
                Text("Rank" + i, body, (i + 1).ToString(), 442, 334 + 72 * i, 58, 54, 34, i < 3 ? Color.black : Color.white);
                Text("RankEmpty" + i, body, "—", 620, 334 + 72 * i, 650, 54, 28, new Color(.55f, .55f, .55f));
            }
            Box("RankStatusBackground", body, 593, 448, 690, 92, new Color(.03f, .03f, .03f, .97f));
            Text("RankStatus", body, "순위 서비스 준비 중", 600, 450, 670, 88, 33, Color.white);
            var data = BackendGame.Instance.UserMainData;
            int wins = rankFilter == 1 ? data.Ball_3_Win : rankFilter == 2 ? data.Ball_4_Win : data.Win + data.Ball_3_Win + data.Ball_4_Win;
            int losses = rankFilter == 1 ? data.Ball_3_Lose : rankFilter == 2 ? data.Ball_4_Lose : data.Lose + data.Ball_3_Lose + data.Ball_4_Lose;
            int draws = rankFilter == 1 ? data.Ball_3_Draw : rankFilter == 2 ? data.Ball_4_Draw : data.Ball_3_Draw + data.Ball_4_Draw;
            Text("MyRank", body, "내 기록", 352, 704, 120, 51, 29, Yellow);
            Text("MyName", body, UserInfo.DisplayName ?? "플레이어", 502, 704, 400, 51, 30, Color.white, autoSize: true);
            Text("MyWins", body, wins + "승", 929, 704, 165, 51, 31, Color.white);
            Text("MyRate", body, (wins + losses + draws == 0 ? 0 : 100f * wins / (wins + losses + draws)).ToString("F0") + "%", 1131, 704, 170, 51, 31, Yellow);
        }

        void OpenPopup(GesPopConsist.Pop popup)
        {
            // Legacy scene popup slots are empty. Use the existing API/cache with
            // native dialogs instead of opening an empty overlay or an absent slot.
            if (popup == GesPopConsist.Pop.Attendance) { Attendance(); return; }
            Profile();
        }

        [Serializable] sealed class ProfileEdit { public string NickName; public int AvatarIdx; }
        void Profile()
        {
            var panel = Dialog("프로필", "");
            Text("ProfileHint", panel, "닉네임 (최대 24자)", 80, 100, 680, 37, 25, Color.white);
            var background = Box("NicknameInput", panel, 100, 150, 640, 62, new Color(.15f, .17f, .21f));
            background.raycastTarget = true;
            var input = background.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = background;
            var viewport = Node("Viewport", background.transform, 14, 3, 612, 56);
            viewport.gameObject.AddComponent<RectMask2D>();
            var value = (TextMeshProUGUI)Text("Value", viewport, "", 0, 0, 612, 56, 30, Color.white, TextAlignmentOptions.MidlineLeft);
            input.textViewport = viewport; input.textComponent = value; input.fontAsset = font;
            input.characterLimit = 24; input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.text = UserInfo.DisplayName ?? ""; input.interactable = !profileSaving;
            var status = Text("ProfileStatus", panel, "", 60, 274, 720, 42, 23, Yellow);
            var save = Hit("SaveProfile", panel, 270, 227, 300, 48, () => SaveProfile(input, status));
            save.interactable = !profileSaving;
            Text("SaveLabel", panel, profileSaving ? "저장 중…" : "저장", 270, 227, 300, 48, 30, Yellow);
        }

        async void SaveProfile(TMP_InputField input, TMP_Text status)
        {
            if (profileSaving) return;
            string name = input.text.Trim();
            if (name.Length == 0 || name.Any(char.IsControl)) { status.text = "올바른 닉네임을 입력해 주세요."; return; }
            if (!Application.isPlaying || string.IsNullOrEmpty(GameApiClient.UserId)) { status.text = "로그인 후 이용할 수 있습니다."; return; }
            profileSaving = true; input.interactable = false; status.text = "저장 중…";
            try
            {
                await GameApiClient.Request<UserMainData>("/api/me/profile", "PATCH", new ProfileEdit { NickName = name, AvatarIdx = Mathf.Clamp(BackendGame.Instance.UserMainData.AvatarIdx, 1, 100) });
                await BackendGame.Instance.LoadAsync();
                if (this && status) { status.text = "저장했습니다."; RefreshUser(); }
            }
            catch (Exception) { if (this && status) status.text = "저장하지 못했습니다. 연결을 확인하고 다시 시도해 주세요."; }
            finally { profileSaving = false; if (input) input.interactable = true; }
        }

        void Settings()
        {
            var panel = Dialog("설정", "게임 소리를 켜거나 끌 수 있습니다.");
            SolidButton(AudioListener.volume > 0 ? "소리 끄기" : "소리 켜기", panel, 270, 230, 300, 60, () =>
            { AudioListener.volume = AudioListener.volume > 0 ? 0 : 1; Settings(); });
        }

        RectTransform Dialog(string title, string message)
        {
            CloseModal();
            modal = Node("Modal", design, 0, 0, Width, Height);
            Box("Shade", modal, 0, 0, Width, Height, new Color(0, 0, 0, .82f)).raycastTarget = true;
            var panel = Node("Panel", modal, 416, 260, 840, 410);
            Box("Border", panel, 0, 0, 840, 410, Yellow);
            Box("PanelBackground", panel, 4, 4, 832, 402, new Color(.035f, .04f, .055f));
            Text("Title", panel, title, 40, 30, 760, 62, 40, Yellow);
            Text("Message", panel, message, 55, 110, 730, 105, 28, Color.white, autoSize: true);
            SolidButton("닫기", panel, 270, 325, 300, 57, CloseModal);
            return panel;
        }

        void CloseModal() { CloseGuildState(); if (modal) { modal.gameObject.SetActive(false); Dispose(modal.gameObject); modal = null; } }
        void RefreshUser()
        {
            if (!initialized) return;
            var data = BackendGame.Instance.UserMainData;
            nickname.text = string.IsNullOrWhiteSpace(UserInfo.DisplayName) ? "플레이어" : UserInfo.DisplayName;
            level.text = "Lv." + data.LvVel;
            gems.text = data.Jem.ToString("N0", CultureInfo.InvariantCulture);
            coins.text = data.Coin.ToString("N0", CultureInfo.InvariantCulture);
            int perLevel = BackendChart.commonCodeData.FirstOrDefault(c => c.ComCd == "LvPlayMax" && c.IsUse == "Y")?.ComVal ?? 0;
            progress.sizeDelta = new Vector2(274 * (perLevel > 0 ? Mathf.Clamp01((float)data.LvProc / perLevel) : 0), 14);
        }
        void UserDataChanged() { RefreshUser(); if (CurrentPage == 2 || CurrentPage == 3) SelectPage(CurrentPage); }
        void RoomCountsChanged(Dictionary<int, int> counts)
        {
            roomCounts.Clear(); foreach (var pair in counts) roomCounts[pair.Key] = pair.Value;
            if (CurrentPage == 2) SelectPage(2);
        }
        void Subscribe()
        {
            if (!initialized || !Application.isPlaying) return;
            BackendGame.Instance.onGameDataLoadEvent.RemoveListener(UserDataChanged);
            BackendGame.Instance.onGameDataLoadEvent.AddListener(UserDataChanged);
            if (subscribedNetwork == null && !string.IsNullOrEmpty(GameApiClient.UserId))
            {
                var network = NetworkManager.network;
                subscribedNetwork = network; network.OnNetworkRoomUpdate += RoomCountsChanged; network.GetRoomsInfo();
            }
        }
        void OnEnable() { FriendSocial.Menu=this; if(FriendSocial.Instance){FriendSocial.Instance.FriendsChanged+=RefreshFriends;FriendSocial.Instance.GuildChanged+=RefreshGuild;} if (initialized) { RefreshUser(); Subscribe(); nextWalletRefresh = 0; if (CurrentPage == 2) CheckBustOnMultiplayerEntry(); RefreshWalletFromServer(); } }
        void OnDisable()
        {
            if(FriendSocial.Menu==this)FriendSocial.Menu=null;
            if(FriendSocial.Instance)FriendSocial.Instance.FriendsChanged-=RefreshFriends;
            if(FriendSocial.Instance)FriendSocial.Instance.GuildChanged-=RefreshGuild;
            if(guildRoot)CloseModal();
            BackendGame.Instance.onGameDataLoadEvent.RemoveListener(UserDataChanged);
            if (subscribedNetwork != null) subscribedNetwork.OnNetworkRoomUpdate -= RoomCountsChanged;
            subscribedNetwork = null;
            HideBustNotice();
        }
        void HideBustNotice()
        {
            if (bustNoticeAnimation != null) StopCoroutine(bustNoticeAnimation);
            bustNoticeAnimation = null;
            if (bustNotice) { Dispose(bustNotice.gameObject); bustNotice = null; }
        }
        void LateUpdate()
        {
            TickGuild();
            TickMailbox();
            Resize();
            RefreshWalletFromServer();
            if (!initialized || CurrentPage != 2 || bustNotice) return;
            var notice = BackendGame.Instance.PendingBustNotice;
            if (notice != null) ShowBustNotice(notice.Remaining);
        }
        void OnApplicationFocus(bool focused)
        { if (focused && initialized && isActiveAndEnabled) { nextWalletRefresh = 0; RefreshWalletFromServer(); } }
        async void RefreshWalletFromServer()
        {
            if (!Application.isPlaying || !initialized || !isActiveAndEnabled || walletRefreshPending || bustCheckPending ||
                string.IsNullOrEmpty(GameApiClient.UserId) || Time.realtimeSinceStartup < nextWalletRefresh) return;
            walletRefreshPending = true;
            nextWalletRefresh = Time.realtimeSinceStartup + 30;
            try
            {
                // Periodic refresh only reads balances; support is checked on multiplayer entry.
                await BackendGame.Instance.LoadAsync();
            }
            catch (Exception) { /* Keep the last server balance on transient network failures. */ }
            finally { walletRefreshPending = false; }
        }
        async void CheckBustOnMultiplayerEntry()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || bustCheckPending || string.IsNullOrEmpty(GameApiClient.UserId)) return;
            bustCheckPending = true;
            string owner = GameApiClient.UserId;
            try
            {
                // Let an existing balance read finish before applying the support response.
                while (walletRefreshPending) await Task.Delay(20);
                if (!this || !isActiveAndEnabled || CurrentPage != 2 || owner != GameApiClient.UserId) return;
                await BackendGame.Instance.CheckBustSupportAsync();
            }
            catch (Exception e) { Debug.LogWarning("파산 지원 확인 실패: " + e.Message); }
            finally { bustCheckPending = false; }
        }
        void ShowBustNotice(long remaining)
        {
            if (bustNoticeAnimation != null) StopCoroutine(bustNoticeAnimation);
            if (bustNotice) Dispose(bustNotice.gameObject);
            const float width = 720, height = 114;
            bustNotice = Node("BustSupportNotice", design, (Width - width) / 2, -height - 12, width, height);
            // Opaque backing guarantees the text area never reveals the arena behind it.
            Box("NoticeOpaqueFill", bustNotice, 50, 16, 620, 86, Color.black);
            var artwork = Resources.Load<Texture2D>("Menu/bust-notice");
            if (artwork)
            {
                var plate = Art("NoticeArtwork", bustNotice, artwork, 0, 0, width, height);
                // Crop transparent generation margins with UVs, preserving the source asset.
                plate.uvRect = new Rect(38f / 2172, 1 - 510f / 724, 2112f / 2172, 335f / 724);
            }
            else Box("NoticeFallback", bustNotice, 0, 0, width, height, Color.black);
            var message = Text("NoticeMessage", bustNotice, "파산지원 코인이 도착했습니다.", 164, 21, 490, 40, 29, new Color(1, .98f, .93f), TextAlignmentOptions.Center, true);
            message.fontStyle = FontStyles.Normal;
            message.textWrappingMode = TextWrappingModes.NoWrap;
            var uses = Text("NoticeRemaining", bustNotice, $"사용가능 횟수({remaining}회)", 285, 69, 181, 26, 19, Color.black, TextAlignmentOptions.Center, true);
            uses.fontStyle = FontStyles.Normal;
            uses.fontSizeMin = 12;
            uses.textWrappingMode = TextWrappingModes.NoWrap;
            bustNotice.SetAsLastSibling();
            if (Application.isPlaying) bustNoticeAnimation = StartCoroutine(AnimateBustNotice());
            else bustNotice.anchoredPosition = new Vector2((Width - width) / 2, -118);
        }
        IEnumerator AnimateBustNotice()
        {
            // Unscaled time keeps the banner moving even if gameplay time is paused.
            var notice = BackendGame.Instance.PendingBustNotice;
            const float x = (Width - 720) / 2;
            for (float t = 0; t < .4f; t += Time.unscaledDeltaTime)
            {
                bustNotice.anchoredPosition = new Vector2(x, Mathf.Lerp(126, -118, Mathf.SmoothStep(0, 1, t / .4f)));
                yield return null;
            }
            bustNotice.anchoredPosition = new Vector2(x, -118);
            yield return new WaitForSecondsRealtime(3f);
            BackendGame.Instance.AcknowledgeBustNotice(notice);
            for (float t = 0; t < .4f; t += Time.unscaledDeltaTime)
            {
                bustNotice.anchoredPosition = new Vector2(x, Mathf.Lerp(-118, 126, Mathf.SmoothStep(0, 1, t / .4f)));
                yield return null;
            }
            Dispose(bustNotice.gameObject); bustNotice = null; bustNoticeAnimation = null;
        }
        void OnDestroy()
        {
            ReleaseShopTextures();
            if (textMaterial) { if (Application.isPlaying) Destroy(textMaterial); else DestroyImmediate(textMaterial); }
        }
        public void Resize()
        {
            if (!design) return;
            var parent = (RectTransform)transform;
            float scale = Mathf.Min(parent.rect.width / Width, parent.rect.height / Height);
            design.localScale = Vector3.one * Mathf.Max(.01f, scale);
        }

        static RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; Place(rect, x, y, w, h); return rect;
        }
        static void Place(RectTransform r, float x, float y, float w, float h)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); r.localScale = Vector3.one; }
        static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one; }
        static Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        { var image = Node(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        static RawImage Art(string name, Transform parent, Texture texture, float x, float y, float w, float h)
        { var image = Node(name, parent, x, y, w, h).gameObject.AddComponent<RawImage>(); image.texture = texture; image.raycastTarget = false; return image; }
        static Rect UV(Rect r) => new Rect(r.x / Width, 1 - (r.y + r.height) / Height, r.width / Width, r.height / Height);
        static RawImage Crop(string name, Transform parent, Texture texture, Rect crop)
        { var image = Art(name, parent, texture, crop.x, crop.y, crop.width, crop.height); image.uvRect = UV(crop); return image; }
        TMP_Text Text(string name, Transform parent, string value, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool autoSize = false)
        {
            var text = Node(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = textMaterial;
            text.text = value; text.fontSize = size; text.fontStyle = FontStyles.Bold;
            text.color = color; text.alignment = align; text.raycastTarget = false; text.richText = false;
            text.enableAutoSizing = autoSize; text.fontSizeMin = 18; text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        Button Hit(string name, Transform parent, float x, float y, float w, float h, UnityAction action)
        {
            var image = Box(name, parent, x, y, w, h, Color.white); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.clear; colors.highlightedColor = new Color(1, 1, 1, .1f);
            colors.selectedColor = new Color(1, 1, 1, .12f); colors.pressedColor = new Color(0, 0, 0, .3f);
            colors.disabledColor = new Color(0, 0, 0, .18f); colors.fadeDuration = 0; button.colors = colors;
            image.canvasRenderer.SetColor(Color.clear);
            button.onClick.AddListener(action); return button;
        }
        void SolidButton(string label, Transform parent, float x, float y, float w, float h, UnityAction action, bool selected = false)
        {
            Box(label + "Border", parent, x, y, w, h, selected ? Yellow : new Color(.7f, .7f, .7f));
            Box(label + "Fill", parent, x + 2, y + 2, w - 4, h - 4, selected ? Yellow : new Color(.06f, .06f, .07f));
            Hit(label, parent, x, y, w, h, action);
            Text(label + "Label", parent, label, x + 6, y, w - 12, h, 29, selected ? Color.black : Color.white);
        }
        static void Clear(Transform parent)
        { for (int i = parent.childCount - 1; i >= 0; i--) { var child = parent.GetChild(i).gameObject; child.SetActive(false); Dispose(child); } }
        static void Dispose(GameObject obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    }
}

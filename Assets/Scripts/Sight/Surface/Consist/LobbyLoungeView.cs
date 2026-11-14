using System;
using System.Globalization;
using System.Linq;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Pavilion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    // Presentation only. PnlLobby continues to own matchmaking, cancellation and countdown.
    public sealed class LobbyLoungeView : MonoBehaviour
    {
        const float Width = 1672, Height = 941;
        RectTransform design;
        TMP_FontAsset font;
        Material material;
        TMP_Text sourceTitle, sourceStatus, sourceSelf, sourceOther;
        TMP_Text title, status, hint, selfName, otherName, level, selfState, otherState;
        TMP_Text ball, fee, target, duration;
        Button cancel, sourceCancel;
        WaitingLights waitingLights;
        Image selfPortrait, otherPortrait;
        EquipmentPortrait selfEquipment, otherEquipment;
        Sprite selfFallback, otherFallback;
        string otherAsset;
        int portraitRequest;
        string portraitOwner;
        GameObject otherPortraitCard;

        public static LobbyLoungeView Install(Transform host, TMP_Text title, TMP_Text status, TMP_Text self, TMP_Text other, Button back)
        {
            var existing = host.GetComponentInChildren<LobbyLoungeView>(true);
            if (existing) return existing;
            var art = Resources.Load<Texture2D>("Menu/waiting-lounge");
            var font = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if (!art || !font) { Debug.LogError("Waiting lounge assets missing; retaining legacy lobby."); return null; }
            foreach (Transform child in host) child.gameObject.SetActive(false);
            var root = new GameObject("LobbyLounge", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(host, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = Color.black;
            var view = root.AddComponent<LobbyLoungeView>();
            view.font = font;
            view.sourceTitle = title; view.sourceStatus = status; view.sourceSelf = self; view.sourceOther = other; view.sourceCancel = back;
            view.Build(art);
            return view;
        }

        void Build(Texture2D art)
        {
            material = new Material(font.material);
            material.DisableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER");
            material.SetFloat("_OutlineWidth", 0); material.SetFloat("_FaceDilate", 0); material.SetFloat("_UnderlayDilate", 0);
            design = Node("Design", transform, 0, 0, Width, Height);
            design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
            design.anchoredPosition = Vector2.zero;
            var background = Node("LoungeBackground", design, 0, 0, Width, Height).gameObject.AddComponent<RawImage>();
            background.texture = art; background.raycastTarget = false;
            // Replace only the printed faces inside the lounge artwork's rectangular borders.
            var selfCard = Panel("SelfPortraitCard", 513, 376, 175, 175, new Color(.32f,.25f,.12f));
            var otherCard = Panel("OtherPortraitCard", 992, 376, 176, 185, new Color(.15f,.23f,.31f));
            selfPortrait = Portrait(selfCard.rectTransform);
            selfEquipment = selfPortrait.gameObject.AddComponent<EquipmentPortrait>();
            otherPortrait = Portrait(otherCard.rectTransform);
            otherEquipment = otherPortrait.gameObject.AddComponent<EquipmentPortrait>();
            otherPortraitCard = otherCard.gameObject;
            waitingLights = Node("WaitingLights", design, 771, 708, 132, 40).gameObject.AddComponent<WaitingLights>();
            title = Label("ArenaTitle", 645, 66, 390, 104, 65, Color.white);
            ball = Label("Ball", 343, 228, 201, 36, 29, Color.white);
            fee = Label("EntryCoin", 566, 228, 254, 36, 27, Color.white);
            target = Label("Target", 844, 228, 197, 36, 27, Color.white);
            duration = Label("Duration", 1067, 228, 258, 36, 27, Color.white);
            selfName = Label("SelfName", 490, 557, 228, 51, 34, Color.white);
            level = Label("SelfLevel", 510, 610, 188, 28, 24, Color.white);
            selfState = Label("SelfState", 495, 648, 208, 37, 26, Color.white);
            otherName = Label("OtherName", 965, 572, 233, 47, 33, Color.white);
            otherState = Label("OtherState", 977, 633, 208, 38, 25, new Color(.7f, .8f, .9f));
            // Solid text backing, wider than the painted bands for long status/error messages.
            Panel("StatusBacking", 550, 750, 575, 45, new Color(.025f, .03f, .035f));
            Panel("HintBacking", 545, 799, 585, 34, new Color(.025f, .03f, .035f));
            status = Label("Status", 558, 750, 559, 45, 29, Color.white);
            hint = Label("Hint", 552, 799, 571, 34, 21, new Color(.62f, .78f, .88f));
            var hit = Panel("CancelWaiting", 663, 847, 345, 62, Color.clear);
            hit.raycastTarget = true;
            cancel = hit.gameObject.AddComponent<Button>(); cancel.targetGraphic = hit;
            var colors = cancel.colors; colors.normalColor = Color.clear; colors.highlightedColor = new Color(1, 1, 1, .1f);
            colors.pressedColor = new Color(1, 1, 1, .2f); colors.selectedColor = Color.clear; cancel.colors = colors;
            cancel.onClick.AddListener(() => { if (sourceCancel && sourceCancel.interactable) sourceCancel.onClick.Invoke(); });
            Label("CancelLabel", 670, 848, 330, 61, 33, new Color(1, .91f, 0)).text = "대기 취소";
            Refresh();
        }

        void OnEnable() { if (selfPortrait) ReadSelfCharacter(); }
        void OnDisable() { portraitRequest++; }

        async void ReadSelfCharacter()
        {
            int request = ++portraitRequest;
            portraitOwner = GameApiClient.UserId;
            ApplySelfCharacter(null);
            if (!Application.isPlaying || string.IsNullOrEmpty(portraitOwner)) return;
            try
            {
                var inventory = await GameApiClient.Request<ShopCatalogData>("/api/me/inventory");
                if (!this || !isActiveAndEnabled || request != portraitRequest || portraitOwner != GameApiClient.UserId) return;
                ApplySelfCharacter(inventory?.Products?.FirstOrDefault(p => p.Kind == "Character" && p.Equipped));
            }
            catch (Exception)
            {
                // Keep the built-in portrait if equipment or its image cannot be loaded.
                if (this && isActiveAndEnabled && request == portraitRequest)
                    Debug.LogWarning("대기실 장착 캐릭터를 불러오지 못했습니다.");
            }
        }

        void ApplySelfCharacter(ShopProductData character)
        {
            var emotions = character?.Emotions?.Select(e => new MatchEquipmentEmotion { Code = e.Code, Asset = e.Asset }).ToArray()
                ?? Array.Empty<MatchEquipmentEmotion>();
            if (!emotions.Any(e => e.Code == "기본" && !string.IsNullOrWhiteSpace(e.Asset)) && !string.IsNullOrWhiteSpace(character?.Thumbnail))
                emotions = new[] { new MatchEquipmentEmotion { Code = "기본", Asset = character.Thumbnail } };
            selfEquipment.SetProfile(new MatchEquipmentProfile { CharacterId = character?.Id ?? 0, Emotions = emotions });
            selfEquipment.Show("기본", selfFallback);
            if (Application.isPlaying && NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork room)
                room.SetLoungePortrait(emotions.FirstOrDefault(e => e.Code == "기본")?.Asset);
        }

        void ApplyOtherPortrait(string asset, Sprite fallback)
        {
            otherPortrait.sprite = fallback;
            if (otherAsset == asset && otherFallback == fallback) return;
            otherAsset = asset; otherFallback = fallback;
            otherEquipment.SetProfile(new MatchEquipmentProfile {
                Emotions = new[] { new MatchEquipmentEmotion { Code = "기본", Asset = asset ?? "" } }
            });
            otherEquipment.Show("기본", fallback);
        }

        public void Refresh(SkillMatchData previewHall = null)
        {
            if (!design) return;
            var parent = (RectTransform)transform;
            float scale = Mathf.Max(.01f, Mathf.Min(parent.rect.width / Width, parent.rect.height / Height));
            design.localScale = Vector3.one * scale;
            var hall = previewHall ?? BackendChart.skillMatchData?.FirstOrDefault(h => h.HallIdx == NetworkManager.hallIdx);
            title.text = hall?.HallName ?? sourceTitle.text;
            ball.text = hall == null ? "—" : hall.MatchBall + "구";
            fee.text = NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork friend && (friend.IsFriendly || friend.IsGuildWar) ? (friend.IsGuildWar?"공식 길드전 · 입장 무료":"친선 · 입장 무료") : hall == null ? "—" : "입장 " + hall.PrizeCoin.ToString("N0", CultureInfo.InvariantCulture) + " 코인";
            target.text = hall == null ? "—" : "목표 " + hall.TargetHit + "점";
            duration.text = hall == null ? "—" : "제한시간 " + hall.MatchTotMin + "분";
            selfName.text = sourceSelf.text;
            level.text = "Lv." + BackendGame.Instance.UserMainData.LvVel;
            bool opponent = !string.IsNullOrWhiteSpace(sourceOther.text);
            int localSeat = net is PunNetwork room ? room.LoungeLocalSeat : 0;
            var fallback = MatchAvatarReactions.NeutralForSeat(localSeat);
            selfPortrait.sprite = fallback;
            if (selfFallback != fallback) { selfFallback = fallback; selfEquipment.Show("기본", fallback); }
            if (Application.isPlaying && portraitOwner != GameApiClient.UserId) ReadSelfCharacter();
            string opponentAsset = opponent && net is PunNetwork opponentRoom ? opponentRoom.LoungeOpponentPortrait : "";
            ApplyOtherPortrait(opponentAsset, MatchAvatarReactions.NeutralForSeat(1 - localSeat));
            if (otherPortraitCard.activeSelf != opponent) otherPortraitCard.SetActive(opponent);
            otherName.text = opponent ? sourceOther.text : "상대 대기 중";
            otherState.text = opponent ? "입장 완료" : "빈 자리";
            var state = sourceStatus.text ?? "";
            bool error = state.Contains("실패") || state.Contains("끊김");
            waitingLights.SetWaiting(!error && (state == "상대 대기" || state == "입장 중" || state == "상대 확인 중" || state == "새 경기장 준비 중"));
            selfState.text = error ? "연결 확인" : state == "입장 중" ? "입장 중" : "입장 완료";
            if (int.TryParse(state, out int seconds))
            {
                status.text = seconds > 0 ? seconds + "초 후 경기가 시작됩니다." : "경기를 시작합니다.";
                hint.text = "잠시만 기다려 주세요.";
            }
            else
            {
                var lines = state.Split('\n');
                status.text = state == "상대 대기" ? "상대가 들어오기를 기다리고 있습니다." : lines[0];
                hint.text = lines.Length > 1 ? string.Join(" ", lines.Skip(1)) : "상대가 준비되면 카운트다운 후 경기가 시작됩니다.";
            }
            status.color = error ? new Color(1, .55f, .4f) : Color.white;
            cancel.interactable = sourceCancel.interactable;
        }
        void LateUpdate() { Refresh(); }
        static Image Portrait(RectTransform card)
        {
            var image = Node("NeutralAvatar", card, 0, 0, card.sizeDelta.x, card.sizeDelta.y).gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
        void OnDestroy() { if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); } }
        TMP_Text Label(string name, float x, float y, float w, float h, float size, Color color)
        {
            var text = Node(name, design, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = material; text.fontStyle = FontStyles.Normal;
            text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = 16;
            text.alignment = TextAlignmentOptions.Center; text.color = color; text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            return text;
        }
        Image Panel(string name, float x, float y, float w, float h, Color color)
        { var image = Node(name, design, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        static RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
        {
            var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
    }
}

using System;
using Assets.Scripts.Sight.Surface.Consist;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Dedicated result presentation; actions remain owned by PnlOutcome.
    public sealed class RematchLoungeView : MonoBehaviour
    {
        public sealed class State
        {
            public string Arena, Result, Score, SelfName, OtherName, SelfInfo, OtherInfo;
            public string SelfVote, OtherVote, Status, Settlement;
            public int LocalAvatarSeat;
            public long? PrizeCoin;
            public bool CanRematch, CanSave, CanLeave = true, Waiting, Friendly;
        }
        RectTransform design;
        TMP_FontAsset font;
        Material textMaterial;
        TMP_Text prizeStyle, prizeAmount;
        const double PrizeCountDuration = 2.0;
        long? animatedPrize;
        double prizeCountStarted;
        const int PrizeCoinCount = 24;
        RawImage[] flyingCoins;
        Vector3[] coinBursts;
        Vector3 coinOrigin, coinDestination;
        double coinFlightStarted;
        bool prizeFlightPlayed, prizeFlightActive;
        TMP_Text arena, score, selfName, otherName, selfInfo, otherInfo, selfVote, otherVote, status, settlement;
        Button rematch, save, leave;
        Button addFriend;
        TMP_Text prizeCaption;
        string friendTarget;
        RawImage background, selfWinner, otherWinner;
        Image selfAvatar, otherAvatar;
        RawImage[] selfWinnerSparkles, otherWinnerSparkles;
        double winnerShineStarted;
        LoungeActionGraphic selfStatus, otherStatus;
        Texture2D victoryBackground, defeatBackground;
        Func<State> read;
        public static RematchLoungeView Install(Transform host, Func<State> read, Action again, Action archive, Action exit)
        {
            var existing = host.GetComponentInChildren<RematchLoungeView>(true);
            if (existing) return existing;
            var art = Resources.Load<Texture2D>("Menu/rematch-victory-16x9");
            var font = Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            if (!art || !font) return null;
            foreach (Transform child in host) child.gameObject.SetActive(false);
            var root = new GameObject("RematchLounge", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(host, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; root.GetComponent<Image>().color = Color.black;
            var view = root.AddComponent<RematchLoungeView>(); view.font = font; view.read = read;
            view.prizeStyle = host.Find("TxtPrizeCoin")?.GetComponent<TMP_Text>();
            view.Build(art, again, archive, exit); return view;
        }
        void Build(Texture2D art, Action again, Action archive, Action exit)
        {
            textMaterial = new Material(font.material);
            textMaterial.DisableKeyword("UNDERLAY_ON"); textMaterial.DisableKeyword("UNDERLAY_INNER");
            textMaterial.SetFloat("_OutlineWidth", 0); textMaterial.SetFloat("_FaceDilate", 0); textMaterial.SetFloat("_UnderlayDilate", 0);
            design = Node("Design", transform, 0, 0, 1672, 941);
            design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f); design.anchoredPosition = Vector2.zero;
            background = Node("Background", transform, 0, 0, 1672, 941).gameObject.AddComponent<RawImage>();
            background.rectTransform.anchorMin=Vector2.zero; background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            background.transform.SetAsFirstSibling();
            victoryBackground=art; defeatBackground=Resources.Load<Texture2D>("Menu/rematch-defeat-16x9");
            background.texture = art; background.raycastTarget = false;
            arena = Label("Arena", 650, 18, 372, 52, 32, Color.white);
            var prizeArt = Node("PrizeGraffiti", design, 506, 66, 660, 300).gameObject.AddComponent<RawImage>();
            prizeArt.texture = Resources.Load<Texture2D>("Menu/rematch-prize-graffiti");
            prizeArt.raycastTarget = false;
            if (prizeArt.texture)
                prizeArt.rectTransform.sizeDelta = new Vector2(660, 660f * prizeArt.texture.height / prizeArt.texture.width);
            prizeCaption=Label("PrizeCaption", 610, 140, 452, 42, 30, Color.white);
            prizeAmount = Label("PrizeAmount", 602, 192, 520, 124, 80, new Color(1,.69173265f,0));
            if (prizeStyle) {
                prizeAmount.font = prizeStyle.font;
                prizeAmount.fontSharedMaterial = prizeStyle.fontSharedMaterial;
                prizeAmount.fontStyle = prizeStyle.fontStyle;
                prizeAmount.fontWeight = prizeStyle.fontWeight;
                prizeAmount.color = prizeStyle.color;
                prizeAmount.characterSpacing = prizeStyle.characterSpacing;
                prizeAmount.rectTransform.localScale = prizeStyle.rectTransform.localScale;
                prizeAmount.fontSizeMax = prizeStyle.fontSize;
            } else prizeAmount.rectTransform.localScale = new Vector3(.9f,1,1);
            // White tint preserves the gradient instead of multiplying it by the old orange color.
            prizeAmount.color = Color.white;
            prizeAmount.colorGradientPreset = null;
            prizeAmount.enableVertexGradient = true;
            var prizeTop = new Color32(255, 241, 161, 255);
            var prizeBottom = new Color32(255, 186, 40, 255);
            prizeAmount.colorGradient = new VertexGradient(prizeTop, prizeTop, prizeBottom, prizeBottom);
            prizeAmount.fontSizeMin = 18;
            selfAvatar = Portrait("SelfAvatar", 317, new Color(1,.86f,.08f));
            otherAvatar = Portrait("OtherAvatar", 1055, new Color(.15f,.8f,1));
            selfWinner = WinnerBadge("SelfWinner", 367);
            otherWinner = WinnerBadge("OtherWinner", 1105);
            selfWinnerSparkles = WinnerSparkles(selfWinner);
            otherWinnerSparkles = WinnerSparkles(otherWinner);
            StyledPanel("SelfNamePlate", 307, 566, 320, 68, new Color(1,.86f,.08f), LoungeActionGraphic.PlateKind.Name);
            StyledPanel("OtherNamePlate", 1045, 566, 320, 68, new Color(.6f,.8f,1), LoungeActionGraphic.PlateKind.Name);
            selfName = Label("SelfName", 337, 576, 260, 46, 28, Color.black);
            otherName = Label("OtherName", 1075, 576, 260, 46, 28, Color.black);
            selfName.fontStyle = otherName.fontStyle = FontStyles.Bold;
            selfStatus = StyledPanel("SelfStatus", 322, 636, 290, 58, Color.black, LoungeActionGraphic.PlateKind.Status);
            otherStatus = StyledPanel("OtherStatus", 1060, 636, 290, 58, Color.black, LoungeActionGraphic.PlateKind.Status);
            selfVote = Label("SelfVote", 337, 644, 260, 42, 23, Color.white);
            otherVote = Label("OtherVote", 1075, 644, 260, 42, 23, Color.white);
            score = Label("Score", 640, 409, 392, 147, 114, new Color(1,.89f,.12f));
            score.fontStyle = FontStyles.Bold | FontStyles.Italic;
            Label("SeriesCaption", 650, 563, 372, 34, 25, Color.white).text = "누적 전적";
            StyledPanel("HighRunBacking", 642, 606, 191, 116, Color.white, LoungeActionGraphic.PlateKind.Stat);
            StyledPanel("DurationBacking", 839, 606, 191, 116, Color.white, LoungeActionGraphic.PlateKind.Stat);
            Label("HighRunCaption", 650, 615, 175, 34, 22, Color.white).text = "최고 연타";
            Label("DurationCaption", 847, 615, 175, 34, 22, Color.white).text = "플레이 시간";
            selfInfo = Label("HighRun", 650, 653, 175, 56, 36, Color.yellow);
            otherInfo = Label("Duration", 847, 653, 175, 56, 36, Color.yellow);
            status = Label("Status", 400, 747, 872, 48, 29, Color.white);
            settlement = Label("Settlement", 400, 799, 872, 29, 18, new Color(.7f,.8f,.9f));
            rematch = ActionButton("Rematch", "재대결", 420, new Color(1,.9f,0), again);
            save = ActionButton("SaveReplay", "시합 저장", 708, new Color(.05f,.9f,1), archive);
            leave = ActionButton("Leave", "나가기", 996, new Color(.07f,.08f,.1f), exit);
            var add=Node("AddFriend",design,1301,274,64,64).gameObject.AddComponent<RawImage>();
            add.texture=Resources.Load<Texture2D>("Menu/Friends/Add");
            addFriend=add.gameObject.AddComponent<Button>();addFriend.targetGraphic=add;
            addFriend.onClick.AddListener(()=> {
                if(friendTarget!=null && FriendSocial.Instance)FriendSocial.Instance.Add(friendTarget,addFriend);
            });
            Refresh();
        }
        public void Refresh()
        {
            if (!design || read == null) return;
            var rect = (RectTransform)transform;
            design.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(rect.rect.width / 1672, rect.rect.height / 941));
            var s = read(); if (s == null) return;
            if (Application.isPlaying && FriendSocial.Instance)
            {
                string target=Assets.Scripts.Exert.Match.PoolPlayer.otherPlayer?.uuid;
                if(target!=friendTarget)CheckFriend(target);
            }
            bool won = s.Result != null && s.Result.Contains("승리");
            bool lost = s.Result != null && s.Result.Contains("패배");
            background.texture = lost && defeatBackground ? defeatBackground : victoryBackground;
            background.uvRect = CoverUv(background.texture, rect.rect.size);
            RefreshPrize(s.PrizeCoin);
            prizeCaption.text=s.Friendly?"친선 경기":"최종 획득 상금";
            if(s.Friendly)prizeAmount.text="입장 무료";
            arena.text = s.Arena; score.text = s.Score;
            score.color = Color.white;
            // Result is local-player-relative, independent of cumulative series wins.
            selfWinner.gameObject.SetActive(won && !lost && selfWinner.texture);
            otherWinner.gameObject.SetActive(lost && !won && otherWinner.texture);
            AnimateWinnerBadge(selfWinner, selfWinnerSparkles);
            AnimateWinnerBadge(otherWinner, otherWinnerSparkles);
            selfAvatar.sprite = MatchAvatarReactions.NeutralForSeat(s.LocalAvatarSeat);
            otherAvatar.sprite = MatchAvatarReactions.NeutralForSeat(1 - s.LocalAvatarSeat);
            var selfEquipment = selfAvatar.GetComponent<EquipmentPortrait>() ?? selfAvatar.gameObject.AddComponent<EquipmentPortrait>();
            var otherEquipment = otherAvatar.GetComponent<EquipmentPortrait>() ?? otherAvatar.gameObject.AddComponent<EquipmentPortrait>();
            selfEquipment.SetProfile(Assets.Scripts.Exert.Network.MatchEquipmentRuntime.ForSeat(s.LocalAvatarSeat));
            otherEquipment.SetProfile(Assets.Scripts.Exert.Network.MatchEquipmentRuntime.ForSeat(1 - s.LocalAvatarSeat));
            selfEquipment.Show("기본", selfAvatar.sprite); otherEquipment.Show("기본", otherAvatar.sprite);
            if (Application.isPlaying && isActiveAndEnabled && s.PrizeCoin > 0 && won != lost &&
                !prizeFlightPlayed && Time.unscaledTimeAsDouble - prizeCountStarted >= PrizeCountDuration)
                StartPrizeFlight(won ? selfAvatar : otherAvatar, Time.unscaledTimeAsDouble);
            TickPrizeFlight(Time.unscaledTimeAsDouble);
            selfName.text = s.SelfName; otherName.text = s.OtherName; selfInfo.text = s.SelfInfo; otherInfo.text = s.OtherInfo;
            selfVote.text = s.SelfVote; otherVote.text = s.OtherVote; status.text = s.Status; settlement.text = s.Settlement;
            rematch.interactable = s.CanRematch; save.interactable = s.CanSave; leave.interactable = s.CanLeave;
            StyleStatus(selfStatus, selfVote); StyleStatus(otherStatus, otherVote);
        }
        async void CheckFriend(string target)
        {
            friendTarget=target;
            addFriend.gameObject.SetActive(false);
            if (!Guid.TryParse(target,out _)) return;
            bool canAdd;
            try { canAdd=await FriendSocial.Instance.CanAdd(target); }
            catch(Exception) { canAdd=true; }
            if(this && friendTarget==target && addFriend)
            {
                addFriend.gameObject.SetActive(canAdd);
                addFriend.interactable=canAdd;
            }
        }
        void OnEnable()
        {
            // Each visit starts a fresh count, including consecutive matches with the same prize.
            FriendSocial.Lounge=this;
            friendTarget=null;
            animatedPrize = null;
            winnerShineStarted = Time.unscaledTimeAsDouble;
            ResetPrizeFlight();
        }
        void OnDisable() { if(FriendSocial.Lounge==this)FriendSocial.Lounge=null; ResetPrizeFlight(); }
        void ResetPrizeFlight()
        {
            prizeFlightPlayed = prizeFlightActive = false;
            if (flyingCoins != null)
                foreach (var coin in flyingCoins) if (coin) coin.gameObject.SetActive(false);
        }
        void StartPrizeFlight(Image winner, double now)
        {
            prizeFlightPlayed = true;
            var texture = Resources.Load<Texture2D>("Menu/rematch-prize-coin");
            if (!texture || !winner) return;
            if (flyingCoins == null) {
                flyingCoins = new RawImage[PrizeCoinCount];
                coinBursts = new Vector3[PrizeCoinCount];
                for (int i = 0; i < PrizeCoinCount; i++) {
                    var coin = Node("PrizeFlyingCoin" + i, design, 0, 0, 34, 34).gameObject.AddComponent<RawImage>();
                    coin.rectTransform.pivot = new Vector2(.5f,.5f);
                    coin.texture = texture; coin.raycastTarget = false;
                    coin.gameObject.SetActive(false); flyingCoins[i] = coin;
                }
            }
            coinOrigin = design.InverseTransformPoint(prizeAmount.rectTransform.TransformPoint(prizeAmount.rectTransform.rect.center));
            coinDestination = design.InverseTransformPoint(winner.rectTransform.TransformPoint(winner.rectTransform.rect.center));
            for (int i = 0; i < PrizeCoinCount; i++) {
                float angle = i * 2.399963f;
                coinBursts[i] = coinOrigin + new Vector3(Mathf.Cos(angle) * (60 + i % 5 * 12), Mathf.Sin(angle) * 55 + 30, 0);
                flyingCoins[i].transform.SetAsLastSibling();
            }
            coinFlightStarted = now;
            prizeFlightActive = true;
        }
        void TickPrizeFlight(double now)
        {
            if (!prizeFlightActive || flyingCoins == null) return;
            bool pending = false;
            for (int i = 0; i < PrizeCoinCount; i++) {
                float age = (float)(now - coinFlightStarted) - i * .035f;
                float duration = .85f + i % 4 * .07f;
                var coin = flyingCoins[i];
                if (age < 0) { pending = true; coin.gameObject.SetActive(false); continue; }
                if (age >= duration) { coin.gameObject.SetActive(false); continue; }
                pending = true; coin.gameObject.SetActive(true);
                float travel = Mathf.Clamp01((age - .18f) / (duration - .18f));
                Vector3 position;
                if (age < .18f) position = Vector3.Lerp(coinOrigin, coinBursts[i], 1 - Mathf.Pow(1 - age / .18f, 2));
                else {
                    float t = travel * travel;
                    var control = (coinBursts[i] + coinDestination) * .5f + Vector3.up * (70 + i % 5 * 15);
                    position = (1-t)*(1-t)*coinBursts[i] + 2*(1-t)*t*control + t*t*coinDestination;
                }
                coin.rectTransform.localPosition = position;
                float size = Mathf.Lerp(1, .2f, travel * travel);
                coin.rectTransform.localScale = new Vector3(size * (.55f + .45f * Mathf.Abs(Mathf.Cos(age * 12 + i))), size, 1);
                coin.rectTransform.localRotation = Quaternion.Euler(0,0,age * 240 + i * 27);
                coin.color = new Color(1,1,1,1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,travel)));
            }
            prizeFlightActive = pending;
        }
        void RefreshPrize(long? amount)
        {
            if (!amount.HasValue) {
                animatedPrize = null;
                ResetPrizeFlight();
                prizeAmount.text = "—";
                return;
            }
            double now = Time.unscaledTimeAsDouble;
            if (animatedPrize != amount) {
                animatedPrize = amount;
                ResetPrizeFlight();
                prizeCountStarted = now;
            }
            long displayed = amount.Value;
            if (Application.isPlaying && amount.Value > 0) {
                double progress = Math.Max(0, Math.Min(1, (now - prizeCountStarted) / PrizeCountDuration));
                // Fast at first, then gently settle. Assign the exact long value at completion.
                if (progress < 1)
                    displayed = (long)(amount.Value * (decimal)(1 - (1 - progress) * (1 - progress)));
            }
            prizeAmount.text = displayed.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
        RawImage WinnerBadge(string name, float x)
        {
            var image = Node(name, design, x, 155, 200, 100).gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("Menu/rematch-winner-badge");
            image.raycastTarget = false;
            if (image.texture)
                image.rectTransform.sizeDelta = new Vector2(200, 200f * image.texture.height / image.texture.width);
            // Pulse about the badge's center without shifting its original layout position.
            var size = image.rectTransform.sizeDelta;
            image.rectTransform.pivot = new Vector2(.5f,.5f);
            image.rectTransform.anchoredPosition += new Vector2(size.x*.5f,-size.y*.5f);
            image.gameObject.SetActive(false);
            return image;
        }
        RawImage[] WinnerSparkles(RawImage badge)
        {
            var texture = Resources.Load<Texture2D>("Menu/rematch-winner-sparkle");
            var points = new[]{new Vector2(.20f,.50f),new Vector2(.48f,.91f),new Vector2(.85f,.61f),new Vector2(.69f,.18f)};
            var stars = new RawImage[points.Length];
            for (int i=0;i<stars.Length;i++) {
                var star=Node("WinnerSparkle"+i,badge.transform,0,0,38,38).gameObject.AddComponent<RawImage>();
                star.texture=texture;star.raycastTarget=false;
                star.rectTransform.anchorMin=star.rectTransform.anchorMax=points[i];
                star.rectTransform.pivot=new Vector2(.5f,.5f);
                star.rectTransform.anchoredPosition=Vector2.zero;
                star.color=Color.clear;stars[i]=star;
            }
            return stars;
        }
        void AnimateWinnerBadge(RawImage badge, RawImage[] stars)
        {
            if (!badge.gameObject.activeSelf || stars == null) return;
            float elapsed=(float)((Time.unscaledTimeAsDouble-winnerShineStarted)%18.0);
            float pulse=.5f+.5f*Mathf.Sin(elapsed*Mathf.PI*2/1.8f);
            badge.color=new Color(1,1,1,Mathf.Lerp(.84f,1,pulse));
            badge.rectTransform.localScale=Vector3.one*Mathf.Lerp(1,1.045f,pulse);
            for(int i=0;i<stars.Length;i++) {
                float phase=Mathf.Repeat(elapsed-i*.39f,1.8f);
                float flash=phase<.7f?Mathf.Pow(Mathf.Sin(phase/.7f*Mathf.PI),2):0;
                stars[i].color=new Color(1,.96f,.72f,flash);
                stars[i].rectTransform.localScale=Vector3.one*Mathf.Lerp(.35f,1.15f,flash);
                stars[i].rectTransform.localRotation=Quaternion.Euler(0,0,i*18+phase*20);
            }
        }
        Image Portrait(string name, float x, Color accent)
        {
            return RematchAvatarShape.CreatePortrait(Node(name+"Frame", design, x-20, 240, 340, 380), accent);
        }
        static void StyleStatus(LoungeActionGraphic panel,TMP_Text label)
        {
            bool requested=label.text.Contains("요청"), ready=label.text.Contains("준비 완료");
            var accent=ready?new Color(.1f,.88f,.93f):requested?new Color(1,.88f,.08f):new Color(.45f,.8f,1);
            if(panel.Accent != accent) { panel.Accent=accent; panel.SetVerticesDirty(); }
            label.color=requested?new Color(1,.88f,.08f):ready?new Color(.1f,.88f,.93f):Color.white;
        }
        void LateUpdate() { Refresh(); }
        void OnDestroy() { if (textMaterial) { if (Application.isPlaying) Destroy(textMaterial); else DestroyImmediate(textMaterial); } }
        Button ActionButton(string name, string label, float x, Color accent, Action clicked)
        {
            var image = Node(name, design, x, 844, 256, 74).gameObject.AddComponent<LoungeActionGraphic>();
            image.Accent = accent; image.Filled = true; image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.65f, .65f, .65f);
            colors.disabledColor = new Color(.55f, .55f, .55f); colors.fadeDuration = .12f; button.colors = colors;
            button.onClick.AddListener(() => { if (button.interactable) clicked?.Invoke(); });
            var text = Label(name + "Label", 0, 0, 256, 74, 29, name == "Leave" ? Color.white : Color.black);
            text.transform.SetParent(image.transform,false);
            text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=new Vector2(20,8);text.rectTransform.offsetMax=new Vector2(-20,-8);
            text.fontStyle = FontStyles.Bold;
            text.text = label;
            return button;
        }
        TMP_Text Label(string name, float x, float y, float w, float h, float size, Color color)
        {
            var t = Node(name, design, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSharedMaterial = textMaterial; t.fontStyle = FontStyles.Normal; t.color = color;
            t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = size;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.richText = false; t.raycastTarget = false;
            return t;
        }
        public static Rect CoverUv(Texture texture, Vector2 viewport)
        {
            if (!texture || viewport.x <= 0 || viewport.y <= 0) return new Rect(0,0,1,1);
            float source=(float)texture.width/texture.height, target=viewport.x/viewport.y;
            if(source>target) { float w=target/source; return new Rect((1-w)/2,0,w,1); }
            float h=source/target; return new Rect(0,(1-h)/2,1,h);
        }
        LoungeActionGraphic StyledPanel(string name,float x,float y,float w,float h,Color accent,LoungeActionGraphic.PlateKind kind)
        {
            var image=Node(name,design,x,y,w,h).gameObject.AddComponent<LoungeActionGraphic>();
            image.Filled=true;image.Kind=kind;image.Accent=accent;image.raycastTarget=false;return image;
        }
        Image Panel(string name, float x, float y, float w, float h, Color color)
        { var image = Node(name, design, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        static RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
    }
}

using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface
{

    public class PnlMatch : MonoBehaviour
    {
        TextMeshProUGUI onlineStatus;
        public void SetOnlineStatus(string message)
        {
            if (!onlineStatus)
            {
                var label = new GameObject("OnlineStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(transform, false);
                onlineStatus = label.GetComponent<TextMeshProUGUI>();
                onlineStatus.font = txtNameSelf.font;
                onlineStatus.fontSize = 30;
                onlineStatus.enableAutoSizing = true;
                onlineStatus.fontSizeMin = 20;
                onlineStatus.fontSizeMax = 30;
                onlineStatus.alignment = TextAlignmentOptions.Center;
                onlineStatus.raycastTarget = false;
                var rect = onlineStatus.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, 65);
                rect.sizeDelta = new Vector2(1100, 55);
            }
            onlineStatus.text = message;
        }

        public void ApplyOnlineResult(Assets.Scripts.Often.MatchResultSession result, int turn)
        {
            var config = PoolCoach.Instance.Configuration;
            int self = config.LocalSeat;
            int previousSelf = hitCntSelf, previousOther = hitCntOther;
            txtNameSelf.text = PoolPlayer.players[self].name;
            txtNameOther.text = PoolPlayer.players[1 - self].name;
            hitCntSelf = self == 0 ? result.Score0 : result.Score1;
            hitCntOther = self == 0 ? result.Score1 : result.Score0;
            txtHitCntSelf.text = hitCntSelf.ToString(); txtHitCntOther.text = hitCntOther.ToString();
            txtCoinSelf.text = txtCoinOther.text = txtMatchCoinSelf.text = txtMatchCoinOther.text = "—";
            for (int i = 0; i < Mathf.Min(hitTarget, Mathf.Min(eggBoxSelf.childCount, eggBoxOther.childCount)); i++)
            {
                UpdateEggPosition(eggBoxSelf, i, previousSelf, hitCntSelf, true);
                UpdateEggPosition(eggBoxOther, i, previousOther, hitCntOther, false);
            }
            if (result.Finished)
                matchFinish.OnlineResultView(result.Winner == -2 ? 0 : result.Winner == self ? 1 : -1,
                    hitCntSelf, hitCntOther);
        }

        public void UpdateOnlineClock(Assets.Scripts.Often.MatchResultSession result, int turn, bool aiming, double now)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(result.MatchDeadline - now)));
            txtTime.text = result.Finished ? "종료" : $"{seconds / 60:00}:{seconds % 60:00}";
            float fraction = aiming && !result.Finished ? Mathf.Clamp01((float)(result.TurnDeadline - now) /
                PoolCoach.Instance.Configuration.TurnSeconds) : 0;
            bool own = turn == PoolCoach.Instance.Configuration.LocalSeat;
            rectInningBarSelf.sizeDelta = new Vector2(own ? inningWidthMax * fraction : 0, inningHeightMax);
            rectInningBarOther.sizeDelta = new Vector2(own ? 0 : inningWidthMax * fraction, inningHeightMax);
        }

        int undoPresentationVersion;
        TextMeshProUGUI txtTime;
        int displayedSeconds = -1;
        Assets.Scripts.Sight.Surface.Pavilion.PopMatchFinish matchFinish;
        Coroutine finishReturnRoutine;
        public void RefreshAfterPracticeUndo()
        {
            CancelPracticeFinish();
            undoPresentationVersion++;
            EggBoxReset();
            hitCntSelf = PoolPlayer.mainPlayer.hitCnt;
            hitCntOther = PoolPlayer.otherPlayer.hitCnt;
            txtHitCntSelf.text = hitCntSelf.ToString();
            txtHitCntOther.text = hitCntOther.ToString();
            txtMatchCoinSelf.text = Utility.CoinNumToStr(PoolPlayer.mainPlayer.matchCoin);
            txtMatchCoinOther.text = Utility.CoinNumToStr(PoolPlayer.otherPlayer.matchCoin);
            txtCoinSelf.text = Utility.CoinNumToStr(PoolPlayer.mainPlayer.coin);
            txtCoinOther.text = Utility.CoinNumToStr(PoolPlayer.otherPlayer.coin);
            for (int i = 0; i < hitTarget; i++)
            {
                if (i >= hitTarget - hitCntSelf) eggBoxSelf.GetChild(i).GetComponent<RectTransform>().anchoredPosition = new Vector2(230 - (hitTarget - 1 - i) * 15, 0);
                if (i >= hitTarget - hitCntOther) eggBoxOther.GetChild(i).GetComponent<RectTransform>().anchoredPosition = new Vector2((hitTarget - 1 - i) * 15, 0);
            }
            rectWordCoinSelf.localScale = Vector3.zero;
            rectWordCoinOther.localScale = Vector3.zero;
            foreach (var message in txtMsgs) message.text = "";
            msgCnt = 0;
            RefreshPracticeScoreboard();
        }

        [SerializeField] TextMeshProUGUI txtCoinSelf;
        [SerializeField] TextMeshProUGUI txtMatchCoinSelf;
        [SerializeField] TextMeshProUGUI txtNameSelf;
        [SerializeField] TextMeshProUGUI txtHitCntSelf;
        [SerializeField] Transform eggBoxSelf;
        [SerializeField] RectTransform rectInningBarSelf;
        [SerializeField] RectTransform rectWordCoinSelf;

        [SerializeField] TextMeshProUGUI txtCoinOther;
        [SerializeField] TextMeshProUGUI txtMatchCoinOther;
        [SerializeField] TextMeshProUGUI txtNameOther;
        [SerializeField] TextMeshProUGUI txtHitCntOther;
        [SerializeField] Transform eggBoxOther;
        [SerializeField] RectTransform rectInningBarOther;
        [SerializeField] RectTransform rectWordCoinOther;

        [SerializeField] Button btnMatchEnd;
        [SerializeField] TextMeshProUGUI[] txtMsgs;
        [SerializeField] RectTransform rectMsgInfo;
        PhysicsMng physicsManager;
        ShotCtrl shotController;

        [SerializeField] SpriteRenderer inningTimerBar;
        [SerializeField] AniCtrl aniCtrl;
        [SerializeField] WordBalloonCtrl wordBalloonCtrl;

        int hitTarget = 0;
        int hitCntSelf = 0;
        int hitCntOther = 0;
        float inningWidthMax = 450f;
        float inningHeightMax = 40f;

        int msgCnt = 0;
        bool isViewMsg = true;
        private void Awake()
        {
            physicsManager = FindObjectOfType<PhysicsMng>();
            shotController = FindObjectOfType<ShotCtrl>();
            txtTime = transform.Find("TopCenter/TxtTime").GetComponent<TextMeshProUGUI>();
            matchFinish = transform.Find("Pops/MatchFinish").GetComponent<Assets.Scripts.Sight.Surface.Pavilion.PopMatchFinish>();
    

            txtCoinSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtCoin").GetComponent<TextMeshProUGUI>();
            txtMatchCoinSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtMatchCoin").GetComponent<TextMeshProUGUI>();
            txtNameSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtName").GetComponent<TextMeshProUGUI>();
            txtHitCntSelf = transform.Find("PlayerBoxs/PlayerSelf/HitCountBox/HitCnt/Txt").GetComponent<TextMeshProUGUI>();
            eggBoxSelf = transform.Find("PlayerBoxs/PlayerSelf/HitCountBox/EggBox");
            rectInningBarSelf = transform.Find("PlayerBoxs/PlayerSelf/TimeInning/Bar").GetComponent<RectTransform>();
            rectWordCoinSelf = transform.Find("PlayerBoxs/PlayerSelf/WordBalloonCoin").GetComponent<RectTransform>();
            txtCoinOther = transform.Find("PlayerBoxs/PlayerOther/TxtCoin").GetComponent<TextMeshProUGUI>();
            txtMatchCoinOther = transform.Find("PlayerBoxs/PlayerOther/TxtMatchCoin").GetComponent<TextMeshProUGUI>();
            txtNameOther = transform.Find("PlayerBoxs/PlayerOther/TxtName").GetComponent<TextMeshProUGUI>();
            txtHitCntOther = transform.Find("PlayerBoxs/PlayerOther/HitCountBox/HitCnt/Txt").GetComponent<TextMeshProUGUI>();
            eggBoxOther = transform.Find("PlayerBoxs/PlayerOther/HitCountBox/EggBox");
            rectInningBarOther = transform.Find("PlayerBoxs/PlayerOther/TimeInning/Bar").GetComponent<RectTransform>();
            rectWordCoinOther = transform.Find("PlayerBoxs/PlayerOther/WordBalloonCoin").GetComponent<RectTransform>();

            btnMatchEnd = transform.Find("BtnMatchEnd").GetComponent<Button >();
            btnMatchEnd.onClick.AddListener(btnMatchEnd_Click);

            rectMsgInfo = transform.Find("MsgInfo").GetComponent<RectTransform>();
            txtMsgs = new TextMeshProUGUI[transform.Find("MsgInfo/Box").childCount];
            for (int i = 0; i < transform.Find("MsgInfo/Box").childCount; i++)
                txtMsgs[i] = transform.Find("MsgInfo/Box").GetChild(i).GetComponent<TextMeshProUGUI>();


            inningTimerBar = GameObject.Find("Table/UI_TOP/InningTimeBar/TimeInningBar").GetComponent<SpriteRenderer>();

            aniCtrl = transform.Find("AchieveMsg").GetComponent<AniCtrl>();
            wordBalloonCtrl = transform.Find("WordBalloonMsg").GetComponent<WordBalloonCtrl>();

            BindRuntimeEvents();
            //physicsManager.OnBallAllStop += PhysicsManager_OnBallAllStop;
            //Debug.Log($"PnlMatch Awake -----------------");
        }

        //private void Start()
        //{
        //    Debug.Log($"PnlMatch Start -----------------");
        //}

        private void OnEnable()
        {
            //Debug.Log($"PnlMatch OnEnable -----------------");
            if (!BindRuntimeEvents()) return;
            MatchInit();

        }

        private void OnDisable()
        {
            StopEggAnimations();
            PoolCoach.Instance.OnMatchComplite -= PoolCoach_OnMatchComplite;
            CancelPracticeFinish();
        }

        void CancelPracticeFinish()
        {
            if (finishReturnRoutine != null) StopCoroutine(finishReturnRoutine);
            finishReturnRoutine = null;
            if (matchFinish) matchFinish.MatchResultClose();
        }

        void PoolCoach_OnMatchComplite()
        {
            if (!PoolCoach.Instance.isRulePractice || finishReturnRoutine != null) return;
            matchFinish.PracticeResultView(PoolPlayer.currentPlayer.name, PoolCoach.Instance.targetHit);
            finishReturnRoutine = StartCoroutine(ReturnAfterPracticeFinish());
        }

        System.Collections.IEnumerator ReturnAfterPracticeFinish()
        {
            yield return new WaitForSecondsRealtime(3f);
            finishReturnRoutine = null;
            Assets.Scripts.Often.PracticeSceneFlow.ReturnToMenu();
        }

        private void FixedUpdate()
        {
            PoolCoach.Instance.Update(Time.fixedDeltaTime);

        }

#if UNITY_EDITOR
        private void Update()
        {
            if (PoolCoach.Instance.Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                //Debug.Log("LeftArrow");
                //HitSelf().Forget();
                TestInningMove();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                //Debug.Log("RightArrow");
                HitOther().Forget();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                //Debug.Log("RightArrow");
                EggBoxReset();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                ToggleMsgInfo();
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                wordBalloonCtrl.MsgOpen(1, "나이스샷!");
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                wordBalloonCtrl.MsgClose();
            }

        }
#endif

        void TestInningMove()
        {
            TestInnintMoveAsync().Forget();
        }

        async UniTaskVoid TestInnintMoveAsync()
        {
            int version = undoPresentationVersion;
            inningTimerBar.size = new Vector2(4.5f, 0.4f);
            inningTimerBar.transform.localPosition = new Vector3(0.62f, 0, 0.92f);
            float t = 0f;
            float w = 4.5f;
            float x = 0.62f;
            
            while (t <= 1)
            {
                inningTimerBar.size = new Vector2(Mathf.Lerp(4.5f, 0, t), 0.4f);
                inningTimerBar.transform.localPosition = new Vector3(Mathf.Lerp(0.62f, 1.04f, t), 0, 0.92f);
                t += 0.01f;
                await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
            }

        }

        void MatchInit()
        {
            CancelPracticeFinish();
            PoolCoach.Instance.MatchReset();            // 시합정보 리셋
            PoolCoach_OnUpdateTime(0);
            EggBoxReset();                              // 점수초기화
            RefreshPracticeScoreboard();
            if (PoolCoach.Instance.Configuration.Execution == Assets.Scripts.Often.MatchExecutionMode.OnlineMatch) return;
            if (Assets.Scripts.Often.PracticeSceneFlow.SelectedMode == Assets.Scripts.Often.PracticeSceneFlow.Mode.Replay ||
                Assets.Scripts.Often.PracticeSceneFlow.SelectedMode == Assets.Scripts.Often.PracticeSceneFlow.Mode.MatchReplay)
                physicsManager.OpenReplayBrowser().Forget();
            else physicsManager.StartBallScatter();


        }

        void EggBoxReset()
        {
            StopEggAnimations();
            hitTarget = PoolCoach.Instance.targetHit;
            for (int i = 0; i < eggBoxSelf.childCount; i++)
            {
                if(i < hitTarget)
                {
                    eggBoxSelf.GetChild(i).gameObject.SetActive(true);
                    eggBoxOther.GetChild(i).gameObject.SetActive(true);
                    eggBoxSelf.GetChild(i).GetComponent<RectTransform>().anchoredPosition = new Vector2(i * 15, 0);
                    eggBoxOther.GetChild(i).GetComponent<RectTransform>().anchoredPosition = new Vector2(230 - i * 15, 0);
                }
                else
                {
                    eggBoxSelf.GetChild(i).gameObject.SetActive(false);
                    eggBoxOther.GetChild(i).gameObject.SetActive(false);
                }
            }
            hitCntSelf = 0;
            hitCntOther = 0;
            txtHitCntSelf.text = $"{hitCntSelf}";
            txtHitCntOther.text = $"{hitCntOther}";
        }

        async UniTask WordCoinPlusSelf()
        {
            int version = undoPresentationVersion;
            //Debug.Log($"WordCoinPlusSelf a : {rectWordCoinSelf.localScale}");
            rectWordCoinSelf.localScale = Vector3.zero;
            float a = 0;
            while(a < 0.3f)
            {
                a += Time.deltaTime; 
                Vector3 v = Vector3.one * a * 2f;
                rectWordCoinSelf.localScale = v;
                await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
                //Debug.Log($"WordCoinPlusSelf a : {a}");
            }
            await UniTask.Delay(2000, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
            rectWordCoinSelf.localScale = Vector3.zero;

        }

        async UniTask WordCoinPlusOther()
        {
            int version = undoPresentationVersion;
            //Debug.Log($"WordCoinPlusSelf a : {rectWordCoinSelf.localScale}");
            rectWordCoinOther.localScale = Vector3.zero;
            float a = 0;
            while (a < 0.3f)
            {
                a += Time.deltaTime;
                Vector3 v = Vector3.one * a * 2f;
                v.x *= -1;
                rectWordCoinOther.localScale = v;
                await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
                //Debug.Log($"WordCoinPlusSelf a : {a}");
            }
            await UniTask.Delay(2000, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
            rectWordCoinOther.localScale = Vector3.zero;

        }

        async UniTaskVoid HitSelf()
        {
            int version = undoPresentationVersion;
            await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;

            if (hitCntSelf < hitTarget)
            {
                int choice = hitTarget - hitCntSelf;
                RectTransform egg = eggBoxSelf.GetChild(choice - 1).GetComponent<RectTransform>();

                float x_ing = egg.anchoredPosition.x;
                float x_end = 230 - hitCntSelf * 15;
                //Debug.Log($"00 hitTarget : {hitTarget}, hitCntSelf : {hitCntSelf}, x_ing : {x_ing}, x_end : {x_end} ");
                while (x_ing < x_end)
                {
                    x_ing = egg.anchoredPosition.x + 10f;
                    if (x_ing > x_end) x_ing = x_end;
                    egg.anchoredPosition = new Vector2(x_ing, 0);
                    await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
                }
                hitCntSelf++;
                txtHitCntSelf.text = $"{hitCntSelf}";
                //Debug.Log($"11 hitTarget : {hitTarget}, hitCntSelf : {hitCntSelf}, x_start : {x_ing}, x_end : {x_end} ");
            }
            else
            {
                //Debug.Log("HitSelf xxxxxxxxxxxxxx");
            }
        }

        async UniTaskVoid HitOther()
        {
            int version = undoPresentationVersion;
            await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;

            if (hitCntOther < hitTarget)
            {
                int choice = hitTarget - hitCntOther;
                RectTransform egg = eggBoxOther.GetChild(choice - 1).GetComponent<RectTransform>();

                float x_ing = egg.anchoredPosition.x;
                float x_end = 0 + hitCntOther * 15;
                //Debug.Log($"22 hitTarget : {hitTarget}, hitCntOther : {hitCntOther}, x_ing : {x_ing}, x_end : {x_end} ");
                while (x_ing > x_end)
                {
                    x_ing = egg.anchoredPosition.x - 10f;
                    if (x_ing < x_end) x_ing = x_end;
                    egg.anchoredPosition = new Vector2(x_ing, 0);
                    await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
                if (version != undoPresentationVersion) return;
                }
                hitCntOther++;
                txtHitCntOther.text = $"{hitCntOther}";
                //Debug.Log($"33 hitTarget : {hitTarget}, hitCntOther : {hitCntOther}, x_ing : {x_ing}, x_end : {x_end} ");
            }
            else
            {
                //Debug.Log("HitOther xxxxxxxxxxxxxx");
            }
        }

        void PoolCoach_OnSetPlayer(PoolPlayer player)
        {
            //Debug.Log($"name : {player.name} , coin : {player.coin}");

            if(player.playerId == 0)
            {
                txtNameSelf.text = player.name;
                txtCoinSelf.text = Utility.CoinNumToStr(player.coin);
                txtMatchCoinSelf.text = "0";
                txtHitCntSelf.text = "0";
            }   
            else if (player.playerId == 1)
            {
                txtNameOther.text = player.name;
                txtCoinOther.text = Utility.CoinNumToStr(player.coin);
                txtMatchCoinOther.text = "0";
                txtHitCntOther.text = "0";
            }
        }

        void PoolCoach_OnSetActivePlayer(int playerId)
        {
            RefreshPracticeScoreboard();
            //Debug.Log($"PoolCoach_OnSetActivePlayer : {playerId}");
            if(playerId == 0)
            {
                rectInningBarSelf.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);
                rectInningBarOther.sizeDelta = new Vector2(0, 0);
            }
            else
            {
                rectInningBarSelf.sizeDelta = new Vector2(0, 0);
                rectInningBarOther.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);
            }
        }

        void PoolCoach_OnUpdateTime(float timeProc)
        {
            int seconds = Mathf.CeilToInt(PoolCoach.Instance.maxPlayTime * (1f - Mathf.Clamp01(timeProc)));
            if (txtTime && seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                txtTime.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            float ing = Mathf.Lerp(inningWidthMax, 0, timeProc);
            RectTransform bar = PoolPlayer.turnId == 0 ? rectInningBarSelf : rectInningBarOther;
            bar.sizeDelta = new Vector2(ing, inningHeightMax);
        }

        void btnMatchEnd_Click()
        {
            Assets.Scripts.Often.PracticeSceneFlow.ReturnToMenu();
        }

        void PoolCoach_OnScoreChanged(int playerId, bool isHit, int coin, RunPath runPath)
        {
            if (PoolCoach.Instance.isRulePractice)
            {
                RefreshPracticeScoreboard();
                if (isHit) aniCtrl.AniAchieve(-1, runPath);
                return;
            }
            if (playerId == 0)
            {
                if (isHit)
                {
                    HitSelf().Forget(); // 점수
                    aniCtrl.AniAchieve(-1, runPath);
                }
            }
            else if (playerId == 1)
            {
                if (isHit)
                {
                    HitOther().Forget();
                    aniCtrl.AniAchieve(-1, runPath);
                }
            }
        }

        // 득점점수, 득점코인
        void PoolCoach_OnBallAllStop(int playerId, bool isHit, int coin)
        {
            //Debug.Log($"PoolCoach_OnBallAllStop playerId : {playerId}, isHit : {isHit}, coin : {coin}");

            if (PoolCoach.Instance.isRulePractice)
            {
                RefreshPracticeScoreboard();
                if (PoolLogic.gameState.gameIsComplete && txtTime)
                {
                    txtTime.text = "종료";
                    displayedSeconds = -1;
                }
                if (isHit)
                {
                    if (playerId == 0) WordCoinPlusSelf().Forget();
                    else WordCoinPlusOther().Forget();
                }
                return;
            }
            if(playerId == 0)
            {
                WordCoinPlusSelf().Forget();
                if (isHit)
                {
                    HitSelf().Forget();

                }
            }
            else if(playerId == 1)
            {
                WordCoinPlusOther().Forget();
                if (isHit)
                {
                    HitOther().Forget();
                }
            }
        }

        bool BindRuntimeEvents()
        {
            if (!physicsManager) physicsManager = FindAnyObjectByType<PhysicsMng>();
            if (!shotController) shotController = FindAnyObjectByType<ShotCtrl>();
            if (!physicsManager || !shotController)
            {
                Debug.LogError("Practice match could not find PhysicsMng or ShotCtrl.");
                return false;
            }

            var coach = PoolCoach.Instance;
            coach.Initialize(physicsManager, shotController);
            coach.OnSetPlayer -= PoolCoach_OnSetPlayer;
            coach.OnSetActivePlayer -= PoolCoach_OnSetActivePlayer;
            coach.OnUpdateTime -= PoolCoach_OnUpdateTime;
            coach.OnBallAllStop -= PoolCoach_OnBallAllStop;
            coach.OnScoreChanged -= PoolCoach_OnScoreChanged;
            coach.OnMatchComplite -= PoolCoach_OnMatchComplite;
            coach.OnPracticeShotResult -= PoolCoach_OnPracticeShotResult;
            coach.OnSetGameInfo -= PoolCoach_OnSetGameInfo;
            coach.OnStartTime -= PoolCoach_OnStartTime;
            coach.OnWordBalloon -= PoolCoach_OnWordBalloon;
            coach.OnSetPlayer += PoolCoach_OnSetPlayer;
            coach.OnSetActivePlayer += PoolCoach_OnSetActivePlayer;
            coach.OnUpdateTime += PoolCoach_OnUpdateTime;
            coach.OnBallAllStop += PoolCoach_OnBallAllStop;
            coach.OnScoreChanged += PoolCoach_OnScoreChanged;
            coach.OnMatchComplite += PoolCoach_OnMatchComplite;
            coach.OnPracticeShotResult += PoolCoach_OnPracticeShotResult;
            coach.OnSetGameInfo += PoolCoach_OnSetGameInfo;
            coach.OnStartTime += PoolCoach_OnStartTime;
            coach.OnWordBalloon += PoolCoach_OnWordBalloon;
            if (btnMatchEnd)
            {
                btnMatchEnd.onClick.RemoveListener(btnMatchEnd_Click);
                btnMatchEnd.onClick.AddListener(btnMatchEnd_Click);
            }
            return true;
        }

        void PoolCoach_OnPracticeShotResult(bool success, bool foul, int cushions)
        {
            RefreshPracticeScoreboard();
            string mode = PoolCoach.Instance.matchBall == MatchBall.ThreeBall ? "3구" : "4구";
            string result = success ? "성공" : foul ? "실패 · 파울" : "실패";
            string detail = PoolCoach.Instance.matchBall == MatchBall.ThreeBall ? $" · 쿠션 {cushions}회" : "";
            PoolCoach.Instance.SetMatchInfo($"{mode} {result}{detail}");
        }

        void StopEggAnimations()
        {
            if (eggBoxSelf)
                foreach (RectTransform egg in eggBoxSelf) egg.DOKill();
            if (eggBoxOther)
                foreach (RectTransform egg in eggBoxOther) egg.DOKill();
        }

        void UpdateEggPosition(Transform box, int index, int previousScore, int score, bool self)
        {
            bool wasScored = index >= hitTarget - previousScore;
            bool isScored = index >= hitTarget - score;
            // Score, shot-end and turn notifications can refresh the same score repeatedly.
            // Leave an in-flight animation alone until that egg's scored state changes.
            if (wasScored == isScored) return;
            var egg = (RectTransform)box.GetChild(index);
            float x = self
                ? (isScored ? 230 - (hitTarget - 1 - index) * 15 : index * 15)
                : (isScored ? (hitTarget - 1 - index) * 15 : 230 - index * 15);
            egg.DOKill();
            var destination = new Vector2(x, 0);
            if (isScored)
                egg.DOAnchorPos(destination, .5f).SetEase(Ease.InOutSine).SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            else egg.anchoredPosition = destination;
        }

        void RefreshPracticeScoreboard()
        {
            if (!PoolCoach.Instance.isRulePractice || PoolPlayer.mainPlayer == null) return;
            int previousSelf = hitCntSelf, previousOther = hitCntOther;
            txtNameSelf.text = PoolPlayer.mainPlayer.name;
            txtNameOther.text = PoolPlayer.otherPlayer.name;
            hitCntSelf = PoolPlayer.mainPlayer.hitCnt;
            hitCntOther = PoolPlayer.otherPlayer.hitCnt;
            txtHitCntSelf.text = hitCntSelf.ToString();
            txtHitCntOther.text = hitCntOther.ToString();
            txtCoinSelf.text = Utility.CoinNumToStr(PoolPlayer.mainPlayer.coin);
            txtCoinOther.text = Utility.CoinNumToStr(PoolPlayer.otherPlayer.coin);
            txtMatchCoinSelf.text = Utility.CoinNumToStr(PoolPlayer.mainPlayer.matchCoin);
            txtMatchCoinOther.text = Utility.CoinNumToStr(PoolPlayer.otherPlayer.matchCoin);
            for (int i = 0; i < hitTarget; i++)
            {
                UpdateEggPosition(eggBoxSelf, i, previousSelf, hitCntSelf, true);
                UpdateEggPosition(eggBoxOther, i, previousOther, hitCntOther, false);
            }
        }


        void PoolCoach_OnSetGameInfo(string msg)
        {
            
            if(msgCnt < txtMsgs.Length)
            {
                txtMsgs[msgCnt].text = $"{msgCnt}. {msg}";
            }
            else
            {
                for (int i = 1; i < txtMsgs.Length; i++)
                {
                    txtMsgs[i - 1].text = txtMsgs[i].text;
                }
                txtMsgs[5].text = $"{msgCnt}. {msg}";
            }
            msgCnt++;
        }

        void ToggleMsgInfo()
        {
            isViewMsg = !isViewMsg;
            float y = isViewMsg ? 0 : -500f;
            rectMsgInfo.anchoredPosition = new Vector2(0, y);
        }

        void PoolCoach_OnStartTime()
        {
            PoolCoach_OnUpdateTime(0);
            physicsManager.SetBallSign();
        }

        void PoolCoach_OnWordBalloon(int ballId, string msg)
        {
            wordBalloonCtrl.MsgOpen(ballId, msg);
        }
    }


}

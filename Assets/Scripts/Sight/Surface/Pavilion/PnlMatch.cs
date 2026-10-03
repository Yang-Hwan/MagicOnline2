using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Vital.Pavilion;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Assets.Scripts.Sight.Surface.Arise;
using System.IO;
using System.Text;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public class PnlMatch : MonoBehaviour
    {
        public static PnlMatch Instance { get; private set; }


        [SerializeField] GesPnl ges;
        [SerializeField] TextMeshProUGUI txtCoinSelf;
        [SerializeField] TextMeshProUGUI  txtMatchCoinSelf;
        [SerializeField] TextMeshProUGUI txtNameSelf;
        [SerializeField] TextMeshProUGUI txtHitCntSelf;
        [SerializeField] TextMeshProUGUI txtScoreSelf;
        [SerializeField] Transform eggBoxSelf;
        [SerializeField] RectTransform rectInningBarSelf;
        [SerializeField] RectTransform rectWordCoinSelf;
        [SerializeField] Image imgInningBarSelf;
        [SerializeField] RectTransform gamFinishSelf;
        [SerializeField] TextMeshProUGUI txtFinishSelf;

        [SerializeField] TextMeshProUGUI txtCoinOther;
        [SerializeField] TextMeshProUGUI txtMatchCoinOther;
        [SerializeField] TextMeshProUGUI txtNameOther;
        [SerializeField] TextMeshProUGUI txtHitCntOther;
        [SerializeField] TextMeshProUGUI txtScoreOther;
        [SerializeField] Transform eggBoxOther;
        [SerializeField] RectTransform rectInningBarOther;
        [SerializeField] RectTransform rectWordCoinOther;
        [SerializeField] Image imgInningBarOther;
        [SerializeField] RectTransform gamFinishOther;
        [SerializeField] TextMeshProUGUI txtFinishOther;

        [SerializeField] Button btnMatchEnd;
        [SerializeField] TextMeshProUGUI[] txtMsgs;
        [SerializeField] RectTransform rectMsgInfo;
        PhysicsMng physicsManager;
        ShotCtrl shotController;
        DrawStuffPos drawStuffPos;
        [SerializeField] ChkBox01 chkMyTurn;
        [SerializeField] ChkBox01 chkWait;
        [SerializeField] TextMeshProUGUI txtDigest;
        [SerializeField] TextMeshProUGUI txtStory;
        [SerializeField] TextMeshProUGUI txtCnt;
        
        [SerializeField] TextMeshProUGUI txtInning;
        [SerializeField] TextMeshProUGUI txtHitInfo;
        [SerializeField] TextMeshProUGUI txtTime;



        //[SerializeField] SpriteRenderer inningTimerBar;   TEST
        [SerializeField] ShotAchieveAni shotAchieveAni;
        [SerializeField] PopMatchFinish popMatchFinish;
        [SerializeField] PopNoticeMsg popNoticeMsg;
 
        int hitTarget = 0;
        int hitCntSelf = 0;
        int hitCntOther = 0;
        readonly MatchReplayArchive matchReplayArchive = new MatchReplayArchive();
        bool recordingMatchReplay;
        bool matchReplaySaved;
        public bool CanSaveMatchReplay => matchReplayArchive.Ready && !matchReplaySaved;
        float inningWidthMax = 450f;
        float inningHeightMax = 40f;

        int msgCnt = 0;
        bool isViewMsg = true;
        CancellationTokenSource cancelSendTime;
        CancellationTokenSource cancelMatchTime;

        public bool pre_isMyturn { get; private set; }

 

        private void Awake()
        {
            Instance = this;

            //DontDestroyOnLoad(this.gameObject);
            if (!NetworkManager.initialized)
            {
                enabled = false;
                Debug.LogWarning("PnlMatch Awake   !NetworkManager.initialized ==========");
                return;
            }

            physicsManager = FindObjectOfType<PhysicsMng>();
            shotController = FindObjectOfType<ShotCtrl>();
            drawStuffPos = FindObjectOfType<DrawStuffPos>();

            ges = transform.parent.GetComponent<GesPnl>();
            txtCoinSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtCoin").GetComponent<TextMeshProUGUI>();
            txtMatchCoinSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtMatchCoin").GetComponent<TextMeshProUGUI>();
            txtNameSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtName").GetComponent<TextMeshProUGUI>();
            txtHitCntSelf = transform.Find("PlayerBoxs/PlayerSelf/HitCountBox/HitCnt/Txt").GetComponent<TextMeshProUGUI>();
            txtScoreSelf = transform.Find("PlayerBoxs/PlayerSelf/TxtScore").GetComponent<TextMeshProUGUI>();
            eggBoxSelf = transform.Find("PlayerBoxs/PlayerSelf/HitCountBox/EggBox");
            rectInningBarSelf = transform.Find("PlayerBoxs/PlayerSelf/TimeInning/Bar").GetComponent<RectTransform>();
            rectWordCoinSelf = transform.Find("PlayerBoxs/PlayerSelf/WordBalloonCoin").GetComponent<RectTransform>();
            imgInningBarSelf = transform.Find("PlayerBoxs/PlayerSelf/TimeInning/Bar").GetComponent<Image>();
            gamFinishSelf = transform.Find("PlayerBoxs/PlayerSelf/HitCountBox/Finish").GetComponent<RectTransform>();
            txtFinishSelf = transform.Find("PlayerBoxs/PlayerSelf/HitCountBox/Finish/Text").GetComponent<TextMeshProUGUI>();

            txtCoinOther = transform.Find("PlayerBoxs/PlayerOther/TxtCoin").GetComponent<TextMeshProUGUI>();
            txtMatchCoinOther = transform.Find("PlayerBoxs/PlayerOther/TxtMatchCoin").GetComponent<TextMeshProUGUI>();
            txtNameOther = transform.Find("PlayerBoxs/PlayerOther/TxtName").GetComponent<TextMeshProUGUI>();
            txtHitCntOther = transform.Find("PlayerBoxs/PlayerOther/HitCountBox/HitCnt/Txt").GetComponent<TextMeshProUGUI>();
            txtScoreOther = transform.Find("PlayerBoxs/PlayerOther/TxtScore").GetComponent<TextMeshProUGUI>();
            eggBoxOther = transform.Find("PlayerBoxs/PlayerOther/HitCountBox/EggBox");
            rectInningBarOther = transform.Find("PlayerBoxs/PlayerOther/TimeInning/Bar").GetComponent<RectTransform>();
            rectWordCoinOther = transform.Find("PlayerBoxs/PlayerOther/WordBalloonCoin").GetComponent<RectTransform>();
            imgInningBarOther = transform.Find("PlayerBoxs/PlayerOther/TimeInning/Bar").GetComponent<Image>();
            gamFinishOther = transform.Find("PlayerBoxs/PlayerOther/HitCountBox/Finish").GetComponent<RectTransform>();
            txtFinishOther = transform.Find("PlayerBoxs/PlayerOther/HitCountBox/Finish/Text").GetComponent<TextMeshProUGUI>();

            btnMatchEnd = transform.Find("BtnMatchEnd").GetComponent<Button>();
            btnMatchEnd.onClick.AddListener(btnMatchEnd_Click);

            rectMsgInfo = transform.Find("MsgInfo").GetComponent<RectTransform>();
            txtMsgs = new TextMeshProUGUI[transform.Find("MsgInfo/Box").childCount];
            for (int i = 0; i < transform.Find("MsgInfo/Box").childCount; i++)
                txtMsgs[i] = transform.Find("MsgInfo/Box").GetChild(i).GetComponent<TextMeshProUGUI>();

            chkMyTurn = transform.Find("MsgInfo/ChkMyTurn").GetComponent<ChkBox01>();
            chkWait = transform.Find("MsgInfo/ChkWait").GetComponent<ChkBox01>();
            txtDigest = transform.Find("MsgInfo/BallStory/Txt1").GetComponent<TextMeshProUGUI>();
            txtStory = transform.Find("MsgInfo/BallStory/Txt2").GetComponent<TextMeshProUGUI>();
            txtCnt = transform.Find("MsgInfo/BallStory/Cnt").GetComponent<TextMeshProUGUI>();
            txtInning = transform.Find("MsgInfo/InnHit/TxtInning").GetComponent<TextMeshProUGUI>();
            txtHitInfo = transform.Find("MsgInfo/InnHit/TxtHitInfo").GetComponent<TextMeshProUGUI>();
            txtTime = transform.Find("TopCenter/TxtTime").GetComponent<TextMeshProUGUI>();

            chkMyTurn.SetInfo("내턴");
            chkWait.SetInfo("기다림");
            txtDigest.text = "";
            txtStory.text = "";
            txtCnt.text = "";
            //gamFinishSelf.SetActive(false);
            //gamFinishOther.SetActive(false);
            //inningTimerBar = GameObject.Find("Table/UI_TOP/InningTimeBar/TimeInningBar").GetComponent<SpriteRenderer>();

            shotAchieveAni = transform.Find("AchieveMsg").GetComponent<ShotAchieveAni>();
            popMatchFinish = transform.Find("Pops/MatchFinish").GetComponent<PopMatchFinish>();
            popNoticeMsg = transform.Find("Pops/NoticeMsg").GetComponent<PopNoticeMsg>();
            //aniCtrl = transform.Find("AchieveMsg").GetComponent<AniCtrl>();
            //wordBalloonCtrl = transform.Find("WordBalloonMsg").GetComponent<WordBalloonCtrl>();


            Debug.Log($"PnlMatch Awake  gamFinishSelf : {gamFinishSelf?.name}");
            Debug.Log($"PnlMatch Awake  gamFinishOther : {gamFinishOther?.name}");

            //string str = "";
            //if(rectInningBarSelf == null)
            //{
            //    str = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
            //}
            //else
            //{
            //    str = "oooooooooooooooooooooooooo";
            //}
            //Debug.Log($"PnlMatch.Awake. rectInningBarSelf : {str}");
        }


        private void OnEnable()
        {

            PoolCoach.Instance.OnSetPlayer += PoolCoach_OnSetPlayer;
            PoolCoach.Instance.OnSetActivePlayer += PoolCoach_OnSetActivePlayer;
            PoolCoach.Instance.OnEndShot += PoolCoach_OnEndShot;
            PoolCoach.Instance.OnUpdateTime += PoolCoach_OnUpdateTime;                  // 타격전 실시간시간
            PoolCoach.Instance.OnEndTime += PoolCoach_OnEndTime;
            PoolCoach.Instance.OnSetGameInfo += PoolCoach_OnSetGameInfo;
            PoolCoach.Instance.OnScoreChanged += PoolCoach_OnScoreChanged;
            PoolCoach.Instance.OnMatchComplite += PoolCoach_OnMatchComplite;
            PoolCoach.Instance.OnNoticeHitBall += PoolCoach_OnNoticeHitBall;
            PoolCoach.Instance.OnFinishAchieve += PoolCoach_OnFinishAchieve;
            PoolCoach.Instance.OnMatchTimeStart += PoolCoach_OnMatchTimeEndProcess;
            physicsManager.OnInitRandForce += PhysicsMng_OnInitRandForce;
            physicsManager.OnStartReplayShot += PhysicsManager_OnStartReplayShot;
            physicsManager.OnMatchReplayShot += PhysicsManager_OnMatchReplayShot;
            physicsManager.OnEndShot += PhysicsManager_OnMatchReplayShotEnd;
            physicsManager.OnBallMovStory += PhysicsManager_OnBallMovStory;

            NetworkManager.network.OnNetworkWaiting += NetworkManager_network_OnNetworkWaiting;
            NetworkManager.network.OnNetwork += NetworkManager_network_OnNetwork;

            WaitInit().Forget();
        }

        private void OnDisable()
        {
            PoolCoach.Instance.OnSetPlayer -= PoolCoach_OnSetPlayer;
            PoolCoach.Instance.OnSetActivePlayer -= PoolCoach_OnSetActivePlayer;
            PoolCoach.Instance.OnEndShot -= PoolCoach_OnEndShot;
            PoolCoach.Instance.OnUpdateTime -= PoolCoach_OnUpdateTime;                  // 타격전 실시간시간
            PoolCoach.Instance.OnEndTime -= PoolCoach_OnEndTime;
            PoolCoach.Instance.OnSetGameInfo -= PoolCoach_OnSetGameInfo;
            PoolCoach.Instance.OnScoreChanged -= PoolCoach_OnScoreChanged;
            PoolCoach.Instance.OnMatchComplite -= PoolCoach_OnMatchComplite;
            PoolCoach.Instance.OnNoticeHitBall -= PoolCoach_OnNoticeHitBall;
            PoolCoach.Instance.OnFinishAchieve -= PoolCoach_OnFinishAchieve;
            PoolCoach.Instance.OnMatchTimeStart -= PoolCoach_OnMatchTimeEndProcess;

            physicsManager.OnInitRandForce -= PhysicsMng_OnInitRandForce;
            physicsManager.OnStartReplayShot -= PhysicsManager_OnStartReplayShot;
            physicsManager.OnMatchReplayShot -= PhysicsManager_OnMatchReplayShot;
            physicsManager.OnEndShot -= PhysicsManager_OnMatchReplayShotEnd;
            physicsManager.OnBallMovStory -= PhysicsManager_OnBallMovStory;

            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetworkWaiting))
                existingOnNetworkWaiting.OnNetworkWaiting -= NetworkManager_network_OnNetworkWaiting;
            if (NetworkManager.TryGetExistingNetwork(out var existingOnNetwork))
                existingOnNetwork.OnNetwork -= NetworkManager_network_OnNetwork;

            Debug.Log($"PnlMatch OnDisable...");

            if (cancelSendTime != null)
            {
                cancelSendTime.Cancel();
            }

            if(cancelMatchTime != null)
            {
                cancelMatchTime.Cancel();
                Debug.Log($"PnlMatch if(cancelMatchTime != null)");
            }

        }

        //private void OnDestroy()
        //{
        //    Debug.Log($"PnlMatch OnDestroy...");
        //    if (cancelSendTime != null)
        //    {
        //        cancelSendTime.Cancel();
        //        cancelSendTime.Dispose();
        //    }
        //}


        async UniTaskVoid WaitInit()
        {
            matchReplayArchive.Reset();
            matchReplaySaved = false;
            PoolCoach.Instance.Initialize(physicsManager, shotController);
            PoolCoach.Instance.MatchReset();            // 시합정보 리셋




            await UniTask.WaitForSeconds(0.1f , cancellationToken: this.GetCancellationTokenOnDestroy());
            MatchInit();
        }

        void MatchInit()
        {
            //Debug.Log($"PnlMatch.MatchInit ******** ... OnMatchComplite : {PoolLogic.gameState.gameIsComplete}  ");


            ///
            //string str = "11.PnlMatch.MatchInit ";
            //if (rectInningBarSelf == null)
            //{
            //    str += "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
            //}
            //else
            //{
            //    str += "oooooooooooooooooooooooooo";
            //}
            //Debug.Log(str);



            //str = "22.PnlMatch.MatchInit ";
            //if (rectInningBarSelf == null)
            //{
            //    str += "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
            //}
            //else
            //{
            //    str += "oooooooooooooooooooooooooo";
            //}
            //Debug.Log(str);


            txtFinishSelf.color = new Color(0.26f, 0.8f, 0.93f);
            txtFinishOther.color = new Color(0.26f, 0.8f, 0.93f);

            gamFinishSelf.anchoredPosition = new Vector2(20f, 800f);
            gamFinishOther.anchoredPosition = new Vector2(-20f, 800f);

            //Debug.Log($"PnlMatch.MatchInit gamFinishSelf : {(gamFinishSelf == null ? "xxx" : gamFinishSelf.name)} =============================== ");
            //Debug.Log($"PoolCoach_OnNoticeHitBall gamFinishOther : {(gamFinishOther == null ? "xxx" : gamFinishOther.name)}");

            //gamFinishSelf.SetActive(false);
            //gamFinishOther.SetActive(false);

            EggBoxReset();

            chkMyTurn.SetCh(PoolPlayer.mainPlayer.myTurn);
            chkWait.SetCh();

            
            shotController.CuePutAside(); // 

            // 볼 뿌리기 
            if (PoolLogic.controlInNetwork)
            {
                physicsManager.InitRandPosFlutter();
            }

            //physicsManager.StartBallScatter();
        }

        void MatchTimeStart(int durationInSeconds)
        {
            DateTime endTime = DateTime.UtcNow.AddSeconds(durationInSeconds);
            cancelMatchTime = new CancellationTokenSource();
            MatchCountdown(endTime, cancelMatchTime.Token).Forget();
        }

        async UniTaskVoid MatchCountdown(DateTime endTime, CancellationToken _cancellationToken)
        {
            while (DateTime.UtcNow < endTime)
            {
                int remainingSeconds = (int)(endTime - DateTime.UtcNow).TotalSeconds;
                string remainTime = $" {remainingSeconds / 60:D2}:{remainingSeconds % 60:D2}";
                txtTime.text = remainTime;
                if (_cancellationToken.IsCancellationRequested)
                {
                    Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                    break;
                }
                //Debug.Log($"Time remaining: {remainTime}");
                await UniTask.Delay(1000, cancellationToken: _cancellationToken);
            }

            // 게임 종료
            //if (PoolLogic.controlInNetwork)
            //{
                PoolCoach.Instance.CallMatchTimeout();
            //}
            Debug.Log($"myturn .true 일때 전송보냄.");
        }

        void PhysicsMng_OnInitRandForce(string randForce)
        {
            NetworkManager.network.OnMadeTurn();
            chkWait?.SetCh(NetworkManager.network.opponenWaitingForYourTurn);

            //PoolCoach_OnSetGameInfo($"자신이 메시지 시작 뿌리기 보냄.");

            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.InitRandForceFlutter), randForce);
        }

        //


        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                HitSelf(1).Forget(); // 점수
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                HitSelf(2).Forget(); // 점수
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                EggBoxReset(); // 점수
            }

        }

        private void FixedUpdate()
        {
            if (recordingMatchReplay && physicsManager && physicsManager.inMove)
                matchReplayArchive.Sample(CaptureLegacyMatchBalls());
            // 볼이 이동중이면 패스
            if (physicsManager.inMove) return;

            // 큐대기시간진행
            PoolCoach.Instance.Update(Time.fixedDeltaTime);

            //Debug.Log($"PoolLogic.controlFromNetwork : {PoolLogic.controlFromNetwork}, shotController.inMove : {shotController.inMove} , physicsManager.inMove : {physicsManager.inMove}");
            // 큐변경된정보움직임 (상대턴, 스트로크 아닌상태)
            if (PoolLogic.controlFromNetwork & !shotController.inMove)
            {
                shotController.UpdateFromNetwork();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void PhysicsManager_OnMatchReplayShot(Ball cueBall, Impulse impulse)
        {
            if (PoolCoach.Instance == null || PoolCoach.Instance.playType != PlayType.Online || !cueBall || physicsManager.balls == null)
                return;
            var localPoint = cueBall.transform.InverseTransformPoint(impulse.point);
            var command = new MatchShotCommand
            {
                seat = Mathf.Clamp(cueBall.id, 0, physicsManager.balls.Length - 1),
                power = Mathf.Clamp01(impulse.impulse.magnitude),
                followThrough = Mathf.Clamp01(impulse.followThrough < 0 ? .2f : impulse.followThrough),
                yaw = cueBall.transform.eulerAngles.y,
                contact = new Vector2(localPoint.x, localPoint.z)
            };
            matchReplayArchive.Begin(command, cueBall.transform.rotation);
            matchReplaySaved = false;
            recordingMatchReplay = true;
            matchReplayArchive.Sample(CaptureLegacyMatchBalls());
        }

        void PhysicsManager_OnMatchReplayShotEnd(float _)
        {
            if (!recordingMatchReplay) return;
            matchReplayArchive.Finish(CaptureLegacyMatchBalls());
            recordingMatchReplay = false;
        }

        MatchBallState[] CaptureLegacyMatchBalls()
        {
            var source = physicsManager ? physicsManager.balls : null;
            if (source == null || source.Length == 0) return null;
            var snapshot = new MatchBallState[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var ball = source[i];
                if (!ball) return null;
                snapshot[i] = new MatchBallState
                {
                    position = ball.body.position,
                    rotation = ball.body.rotation,
                    velocity = ball.body.linearVelocity,
                    angularVelocity = ball.body.angularVelocity
                };
            }
            return snapshot;
        }

        public bool SaveMatchReplay(int slot, string directory, string title)
        {
            if (!CanSaveMatchReplay || !matchReplayArchive.SaveToSlot(directory, slot)) return false;
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"slot{slot + 1:00}.title.txt");
                File.WriteAllText(path, (title ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim(), Encoding.UTF8);
                matchReplaySaved = true;
                return true;
            }
            catch (IOException) { return false; }
            catch (System.UnauthorizedAccessException) { return false; }
        }

        void PoolCoach_OnSetPlayer(PoolPlayer player)
        {
            bool isFirstMatch = PoolPlayer.mainPlayer.winCnt == 0 && PoolPlayer.otherPlayer.winCnt == 0 ? true : false;
            if (player.playerId == 0)
            {
                txtNameSelf.text = player.name;
                txtCoinSelf.text = Utility.CoinNumToStr(player.coin);
                txtMatchCoinSelf.text = "0";
                txtHitCntSelf.text = "0";
                txtScoreSelf.text = isFirstMatch ? "" : $"{PoolPlayer.mainPlayer.winCnt}";

                  
                ///
                //string str = $"PnlMatch.PoolCoach_OnSetPlayer player : {player.playerId} , mytur : {player.myTurn}   .. ";
                //if (rectInningBarSelf == null)
                //{
                //    str += "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
                //}
                //else
                //{
                //    str += "oooooooooooooooooooooooooo";
                //    rectInningBarSelf.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);
                //}
                //Debug.Log(str);

                //rectInningBarSelf.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);
                //imgInningBarSelf.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
               

                //Debug.Log($"PoolCoach_OnSetPlayer playerId : {player.playerId} .. match coin : {txtMatchCoinSelf.text} ... OnMatchComplite : {PoolLogic.gameState.gameIsComplete} ");

            }
            else if (player.playerId == 1)
            {
                txtNameOther.text = player.name;
                txtCoinOther.text = Utility.CoinNumToStr(player.coin);
                txtMatchCoinOther.text = "0";
                txtHitCntOther.text = "0";
                txtScoreOther.text = isFirstMatch ? "" : $"{PoolPlayer.otherPlayer.winCnt}";
                if (rectInningBarOther != null)
                {

                    rectInningBarOther.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);
                    imgInningBarOther.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                }
                else
                {
                    Debug.LogWarning($"rectInningBarOther == null rectInningBarOther == null rectInningBarOther == null ");
                }
                Debug.Log($"PoolCoach_OnSetPlayer playerId : {player.playerId} .. match coin : {txtMatchCoinOther.text} ... OnMatchComplite : {PoolLogic.gameState.gameIsComplete} ");

            }
        }



        void PoolCoach_OnSetActivePlayer(int playerId)
        {
            //Debug.Log($"PoolCoach_OnSetActivePlayer playerId : {playerId}, myTurn : {PoolPlayer.mainPlayer.myTurn}");
            //PoolCoach_OnSetGameInfo($"PoolCoach_OnSetActivePlayer playerId : {playerId}, myTurn : {PoolPlayer.mainPlayer.myTurn}");

 
            shotController.OnEnableControl(PoolPlayer.mainPlayer.myTurn);
            PhysicsManager_OnBallMovStory( BallMovingDigest.None,  BallMovingStory.None, 0, Vector3.zero);

            // 


            if (PoolLogic.controlInNetwork)
            {
                cancelSendTime = new CancellationTokenSource();

                // 선수타임보냄, ..
                UpdateNetworkTimeAsync(cancelSendTime.Token).Forget();
            }

            // 종료 후 미비된 값 다시 보내줌.
            if (pre_isMyturn)
            {
                // 스터프정보, 코인최종값 보냄. (스터프점수,)(스터프점수,) Act
                MatchSummarySend();
            }

            chkMyTurn.SetCh(PoolPlayer.mainPlayer.myTurn);

            rectInningBarSelf.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);
            rectInningBarOther.sizeDelta = new Vector2(inningWidthMax, inningHeightMax);

            if (playerId == 0)
            {
                //txtNameSelf.text = PoolPlayer.mainPlayer.name;
                //txtCoinSelf.text = Utility.CoinNumToStr(PoolPlayer.mainPlayer.coin);
                //txtMatchCoinSelf.text = "0";
                //txtHitCntSelf.text = "0";
                imgInningBarSelf.color = Color.white;
                imgInningBarOther.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
            }
            else
            {
                //txtNameOther.text = PoolPlayer.otherPlayer.name;
                //txtCoinOther.text = Utility.CoinNumToStr(PoolPlayer.otherPlayer.coin);
                //txtMatchCoinOther.text = "0";
                //txtHitCntOther.text = "0";
                imgInningBarOther.color = Color.white;
                imgInningBarSelf.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
            }

            txtInning.text = $"이닝: {PoolPlayer.inning}     순번: {PoolPlayer.ord}";
            txtHitInfo.text = $"연타: {PoolPlayer.currentPlayer.hitInning}     최고: {PoolPlayer.currentPlayer.hitHigh}";
        }


        void MatchSummarySend()
        {
            physicsManager.SetBallsSync();

            // 상대편 기준으로 보냄
            string msg = $"({PoolPlayer.otherPlayer.matchCoin},0)";
            msg += $"({PoolPlayer.mainPlayer.matchCoin},{PoolLogic.gameState.coinGain})";

            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetMatchSummaryFromNetwork), msg);
            //Debug.Log($"[보냄]MatchSummarySend : {msg}");
        }


        public void SetMatchSummaryFromNetwork(string summaryData)
        {
            string msg = $"(자신: {PoolPlayer.mainPlayer.matchCoin},{PoolLogic.gameState.coinGain}) (상대: {PoolPlayer.otherPlayer.matchCoin},0)";

            string[] playerInfo = DataManager.ConvertArrayDataToBraceArray(summaryData);
            //Debug.Log($"[받음] -- {summaryData}  .. [기존] {msg}");

            long main_matchCoin = 0;
            long other_matchCoin = 0;
            for (int i = 0; i < playerInfo.Length; i++)
            {
                if (playerInfo[i] == string.Empty) continue;
                if (playerInfo[i].IndexOf(',') < 0) continue;

                // 상대편 정보
                if (i == 0)
                {
                    string[] info = DataManager.ConvertDataToBraceArray(playerInfo[i]);
                    main_matchCoin = int.Parse(info[0]);
                }else if(i == 1)
                {
                    string[] info = DataManager.ConvertDataToBraceArray(playerInfo[i]);
                    other_matchCoin = int.Parse(info[0]);
                }
            }

            if(main_matchCoin != PoolPlayer.mainPlayer.matchCoin)
            {
                Debug.LogWarning($" PoolPlayer.mainPlayer.matchCoin : { PoolPlayer.mainPlayer.matchCoin} .. main_matchCoin : {main_matchCoin} ");
            }
            if (other_matchCoin != PoolPlayer.otherPlayer.matchCoin)
            {
                Debug.LogWarning($" PoolPlayer.otherPlayer.matchCoin : { PoolPlayer.otherPlayer.matchCoin} .. other_matchCoin : {other_matchCoin} ");
            }
            PoolPlayer.SetPlayerMatchCoin(main_matchCoin, other_matchCoin);
            txtMatchCoinSelf.text = Utility.CoinNumToStr(main_matchCoin);
            txtMatchCoinOther.text = Utility.CoinNumToStr(other_matchCoin);
        }



        // 모든 볼이 멈춘 후
        void PoolCoach_OnEndShot(int playerId, bool isHit, int coin, float moveTime)
        {
             
            //PoolCoach_OnSetGameInfo($"자신이 메시지 모두멈춤완료 보냄.");
            this.moveTime = moveTime;


            //if (!PoolCoach.Instance.isMatchTimePlay)
            //{
            WaitAndStopMove();
            //}
            //else
            //{
            //    Time.timeScale = 0;
            //}

            //Debug.Log($"PoolCoach_OnEndShot gamFinishSelf : {(gamFinishSelf == null ? "xxx" : gamFinishSelf.name)}   =======");
        }


        float moveTime = 0;

        void WaitAndStopMove()
        {
            Time.timeScale = 1;
            
            //Debug.Log("WaitAndStopMove");

            if (PoolLogic.controlInNetwork)
            {
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.WaitAndStopMoveFromNetwork), moveTime);
            }

            string stuffData = string.Empty;

            if (PoolLogic.controlInNetwork)
            {
                // 
                //stuffData = $"[{0};{0}]";
            }
            else
            {
                //stuffData = drawStuffPos.stuff_data;
            }
            //physicsManager.stuffData;

            //Debug.Log($"PoolCoach_OnBallAllStop playerId : {playerId}, isHit : {isHit}, coin : {coin}");
            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.OnOpponentWaitingForYourTurn), stuffData);   // 상대 대기상대끝을 알림.  상대에게호출됨: NetworkManager_network_OnNetworkWaiting

        }


        // 모든 볼이 멈춘 후 상대에게도 알림을 받았다면 
        void NetworkManager_network_OnNetworkWaiting(string msg)
        {
            // 시합시작시 한번 실행
            //if (!PoolCoach.Instance.isMatchTimePlay)
            //{
                //MatchTimeStart();
                //PoolCoach.Instance.SetMatchTimePlay(true);
                //physicsManager.
                // 매치타임 시작처리 
                //if (PoolLogic.controlInNetwork)
                //   StartCoroutine(UpdateNetworkTime());
                //UpdateNetworkTimeAsync().Forget();  // 선수타임보냄, ..

                //PoolCoach.Instance.ActivePlayer();
            //}
            if (PoolLogic.controlInNetwork)
            {
                //string[] stuffRows = DataManager.ConvertArrayDataToBraceArray(msg);
                //for (int i = 0; i < stuffRows.Length; i++)
                //{
                //    if (stuffRows[i] == string.Empty) continue;
                //    if (stuffRows[i].IndexOf(',') < 0) continue;
                //    string[] stuff = DataManager.ConvertDataToBraceArray(stuffRows[i]);
                //    int pId = int.Parse(stuff[0]);
                //    int tId = int.Parse(stuff[1]);
                //    Debug.Log($"{i} >> pid : {pId} , tid : {tId} ");
                //}

                //drawStuffPos.stuff_array(msg);
                //string[] list = DataManager.ConvertDataToStringArray(msg);

            }
            else
            {
                //Debug.Log($"Waiting MSG : PASS __{msg}__ ");
            }

            //Debug.Log($"***** NetworkManager_network_OnNetworkWaiting myturn : {pre_isMyturn}, changeTurn : {PoolLogic.gameState.needToChangeTurn} ... PoolLogic.gameState.gameIsComplete : {PoolLogic.gameState.gameIsComplete} .. msg : {msg}");

            chkWait?.SetCh(NetworkManager.network.opponenWaitingForYourTurn);
            //PoolCoach_OnSetGameInfo($"[상대]에게 메시지 모두멈춤완료 받음. 턴변경필요: {PoolLogic.gameState.needToChangeTurn}");
            // 자신의 턴일때  턴의 변환이 있다면 변경시작한다.


            if (PoolLogic.gameState.gameIsComplete) return;

            pre_isMyturn = PoolPlayer.mainPlayer.myTurn;

            // 턴 변경이면

            if (PoolLogic.gameState.needToChangeTurn)
            {
              
                
                    ChangeTurnReady();
                
            }
            else
            {
                PoolCoach.Instance.ActivePlayer();
            }



        }

        void PoolCoach_OnEndTime()
        {
            ChangeTurnReady();
        }

        void ChangeTurnReady()
        {
            if (PoolLogic.controlInNetwork)
            {
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.WaitChangeTurnReadyFromNetwork));
                PoolCoach.Instance.ChangeTurnReady();
            }
        }


        public void PoolCoach_OnSetGameInfo(string msg)
        {

            //Debug.Log("PoolCoach_OnSetGameInfo : " + msg);
            //return;

            if (msgCnt < txtMsgs.Length)
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




        async UniTaskVoid UpdateNetworkTimeAsync(CancellationToken _cancellationToken)
        {

            //Debug.Log(" ================================= UpdateNetworkTimeAsync ================================= ");
            while (PoolLogic.controlInNetwork && !physicsManager.inMove)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    Debug.Log($"{this.name} .._cancellationToken.IsCancellationRequested : {_cancellationToken.IsCancellationRequested}");
                    break;
                }
                SendToNetwork();
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: _cancellationToken);
            }
            //Debug.Log(" ================================= UpdateNetworkTimeAsync END END END ================================= ");

        }

        void SendToNetwork()
        {
            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.OnSendTime), PoolCoach.Instance.playTime);

            if (CueChanged)
            {
                //Debug.Log($"SendToNetwork ......  ..... shotController.cuePivot.localRotation.eulerAngles.y : {shotController.cuePivot.localRotation.eulerAngles.y} ");
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.OnSendCueControl),
                    shotController.cuePivot.localRotation.eulerAngles.y,
                    shotController.cueVertical.localRotation.eulerAngles.x,
                    new Vector2(shotController.cueDisplacement.localPosition.x, shotController.cueDisplacement.localPosition.y),
                    shotController.cueSlider.localPosition.z,
                    shotController.force,
                    shotController.pullBarHandle.localPosition.y);
            }
        }

        public float cuePivotLocalRotationY { get; private set; }
        public float cueVerticalLocalRotationX { get; private set; }
        public Vector2 cueDisplacementLocalPositionXY { get; private set; }
        public float cueSliderLocalPositionZ { get; private set; }
        public float pullBarHandleLocalPositionY { get; private set; }
        public bool CueChanged
        {
            get
            {
                if (cuePivotLocalRotationY != shotController.cuePivot.localRotation.eulerAngles.y ||
                    cueVerticalLocalRotationX != shotController.cueVertical.localRotation.eulerAngles.x ||
                    cueDisplacementLocalPositionXY != new Vector2(shotController.cueDisplacement.localPosition.x, shotController.cueDisplacement.localPosition.y) ||
                    cueSliderLocalPositionZ != shotController.cueSlider.localPosition.z ||
                    pullBarHandleLocalPositionY != shotController.pullBarHandle.localPosition.y)
                {
                    cuePivotLocalRotationY = shotController.cuePivot.localRotation.eulerAngles.y;
                    cueVerticalLocalRotationX = shotController.cueVertical.localRotation.eulerAngles.x;
                    cueDisplacementLocalPositionXY = new Vector2(shotController.cueDisplacement.localPosition.x, shotController.cueDisplacement.localPosition.y);
                    cueSliderLocalPositionZ = shotController.cueSlider.localPosition.z;
                    pullBarHandleLocalPositionY = shotController.pullBarHandle.localPosition.y;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        void PoolCoach_OnUpdateTime(float timeProc)
        {
            float ing = Mathf.Lerp(inningWidthMax, 0, timeProc);
            RectTransform bar = PoolLogic.controlInNetwork ? rectInningBarSelf : rectInningBarOther;
            bar.sizeDelta = new Vector2(ing, inningHeightMax);

        }


        void PhysicsManager_OnStartReplayShot(string impulse)
        {
            NetworkManager.network.OnMadeTurn();
            chkWait?.SetCh(NetworkManager.network.opponenWaitingForYourTurn);

            if (PoolLogic.controlInNetwork)
            {
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.StartSimulate), impulse);
            }
            else
            {
                shotController.WaitAndStartShot(impulse).Forget();
            }

        }

        void PoolCoach_OnScoreChanged(int playerId, int cnt, int reward, RunPath runPath)
        {

            //Debug.Log($" PoolCoach_OnScoreChanged myturn : {PoolPlayer.mainPlayer.myTurn}, playerId : {playerId}, cnt : {cnt}, reward : {reward}, runPath : {runPath}");

            int turnId = PoolPlayer.turnId;

            if (cnt > 0)
            {
                if (playerId == 0)
                    HitSelf(cnt).Forget(); // 점수
                else
                    HitOther(cnt).Forget();

                shotAchieveAni.AniAchieve(turnId, - 1, reward, runPath);
            }

        }


        void EggBoxReset()
        {
            hitTarget = PoolCoach.Instance.targetHit;
            for (int i = 0; i < eggBoxSelf.childCount; i++)
            {
                if (i < hitTarget)
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

 

        async UniTaskVoid HitSelf(int cnt = 1)
        {
            await UniTask.Delay(1500);

            for (int i = 0; i < cnt; i++)
            {
                if (hitTarget >= 15) break;
                int choice = hitTarget - hitCntSelf;

                if (hitCntSelf == hitTarget)
                {
                    eggBoxSelf.GetChild(hitTarget).gameObject.SetActive(true);
                    eggBoxSelf.GetChild(hitTarget).GetComponent<RectTransform>().anchoredPosition = new Vector2(hitTarget * 0, 0);
                    hitTarget++;
                    choice = hitTarget;
                }

                if (hitCntSelf < hitTarget)
                {
                    RectTransform egg = eggBoxSelf.GetChild(choice - 1).GetComponent<RectTransform>();

                    float x_ing = egg.anchoredPosition.x;
                    float x_end = 230 - hitCntSelf * 15;
                    Debug.Log($"00 choice : {choice}  hitTarget : {hitTarget}, hitCntSelf : {hitCntSelf}, x_ing : {x_ing}, x_end : {x_end} ");
                    while (x_ing < x_end)
                    {
                        x_ing = egg.anchoredPosition.x + 10f;
                        if (x_ing > x_end) x_ing = x_end;
                        egg.anchoredPosition = new Vector2(x_ing, 0);
                        await UniTask.Yield( cancellationToken: this.GetCancellationTokenOnDestroy());
                    }
                    hitCntSelf++;
                    txtHitCntSelf.text = $"{hitCntSelf}";
                }

                await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());

            }

            if (PoolPlayer.mainPlayer.hitCnt == hitCntSelf)
            {
                //Debug.Log($"HitSelf hitCnt : {PoolPlayer.mainPlayer.hitCnt} ... matchCoin : {PoolPlayer.mainPlayer.matchCoin} ...hitInning : {PoolPlayer.mainPlayer.hitInning} ... UI.hitCntSelf : {hitCntSelf} ");
            }
            else
            {
                //Debug.LogError($"HitSelf hitCnt : {PoolPlayer.mainPlayer.hitCnt} ... matchCoin : {PoolPlayer.mainPlayer.matchCoin} ...hitInning : {PoolPlayer.mainPlayer.hitInning} ... UI.hitCntSelf : {hitCntSelf} ");
            }

        }

        async UniTaskVoid HitSelfMinus()
        {
            if (hitCntSelf <= 0) return;
            int choice = hitTarget - hitCntSelf;
            RectTransform egg = eggBoxSelf.GetChild(choice - 0).GetComponent<RectTransform>();

            float x_ing = egg.anchoredPosition.x;
            float x_end = 0 + (hitTarget - hitCntSelf) * 15;
            Debug.Log($"00 choice : {choice}  hitTarget : {hitTarget}, hitCntSelf : {hitCntSelf}, x_ing : {x_ing}, x_end : {x_end} ");
            while (x_ing > x_end)
            {
                x_ing = egg.anchoredPosition.x - 10f;
                if (x_ing < x_end) x_ing = x_end;
                egg.anchoredPosition = new Vector2(x_ing, 0);
                await UniTask.Yield( cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            hitCntSelf--;
            txtHitCntSelf.text = $"{hitCntSelf}";
        }


        async UniTaskVoid HitOther(int cnt = 1)
        {
            await UniTask.Delay(1500);

            for (int i = 0; i < cnt; i++)
            {
                if (hitTarget >= 15) break;
                int choice = hitTarget - hitCntOther;

                if (hitCntOther == hitTarget)
                {
                    eggBoxOther.GetChild(hitTarget).gameObject.SetActive(true);
                    eggBoxOther.GetChild(hitTarget).GetComponent<RectTransform>().anchoredPosition = new Vector2(230 - hitTarget * 0, 0);
                    hitTarget++;
                    choice = hitTarget;
                }

                if (hitCntOther < hitTarget)
                {
                    RectTransform egg = eggBoxOther.GetChild(choice - 1).GetComponent<RectTransform>();

                    float x_ing = egg.anchoredPosition.x;
                    float x_end = 0 + hitCntOther * 15;
                    Debug.Log($"22 hitTarget : {hitTarget}, hitCntOther : {hitCntOther}, x_ing : {x_ing}, x_end : {x_end} ");
                    while (x_ing > x_end)
                    {
                        x_ing = egg.anchoredPosition.x - 10f;
                        if (x_ing < x_end) x_ing = x_end;
                        egg.anchoredPosition = new Vector2(x_ing, 0);
                        await UniTask.Yield( cancellationToken: this.GetCancellationTokenOnDestroy());
                    }
                    hitCntOther++;
                    txtHitCntOther.text = $"{hitCntOther}";
                }
           

            }

            //if(PoolPlayer.otherPlayer.hitCnt == hitCntOther)
            //{
            //    //Debug.Log($"HitOther  hitCnt : {PoolPlayer.otherPlayer.hitCnt} ... matchCoin : {PoolPlayer.otherPlayer.matchCoin} ...hitInning : {PoolPlayer.otherPlayer.hitInning}  ... UI.hitCntOther : {hitCntOther} ");
            //}
            //else
            //{
            ////    Debug.LogError($"HitOther  hitCnt : {PoolPlayer.otherPlayer.hitCnt} ... matchCoin : {PoolPlayer.otherPlayer.matchCoin} ...hitInning : {PoolPlayer.otherPlayer.hitInning}  ... UI.hitCntOther : {hitCntOther} ");
            //}
        }



        async UniTaskVoid HitOtherMinus()
        {

 
            if (hitCntOther <= 0) return;
            int choice = hitTarget - hitCntOther;
            RectTransform egg = eggBoxOther.GetChild(choice - 0).GetComponent<RectTransform>();

            float x_ing = egg.anchoredPosition.x;
            float x_end = 230 - (hitTarget - hitCntOther) * 15;
            //Debug.Log($"22 hitTarget : {hitTarget}, hitCntOther : {hitCntOther}, x_ing : {x_ing}, x_end : {x_end} ");
            while (x_ing < x_end)
            {
                x_ing = egg.anchoredPosition.x + 10f;
                if (x_ing > x_end) x_ing = x_end;
                egg.anchoredPosition = new Vector2(x_ing, 0);
                await UniTask.Yield( cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            hitCntOther--;
            txtHitCntOther.text = $"{hitCntOther}";
               

       

        }


        void PhysicsManager_OnBallMovStory(BallMovingDigest digest, BallMovingStory story, int cnt, Vector3 pos)
        {

            //if (PoolLogic.controlInNetwork)
            //{
            //    string storyData = $"[{(int)digest};{(int)story};{cnt};{DataManager.Vector3ToString(pos)}]";
            //    NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetBallMovStoryFromNetwork), physicsManager.moveTime, storyData);
            //}

            //Debug.Log($"txt1 : {digest}, txt2 : {story}, cnt : {cnt}, pos : {pos}");
            txtDigest.text = digest == BallMovingDigest.None? "" : digest.ToString();
            txtStory.text = story == BallMovingStory.None?"": story.ToString();
            txtCnt.text = $"{cnt}__{PoolLogic.controlInNetwork}";
        }



        void NetworkManager_network_OnNetwork(NetworkState state)
        {
            switch (state)
            {
                case NetworkState.OpponentLeftRoom:
                    PoolCoach.Instance.OtherLefted();
                    break;
                case NetworkState.LeftRoom:
                    BackendDuty.Instance.ConsistPage(2);
                    SceneMove.LoadScene(SceneNames.Consist);
                    //SceneMove.LoadScene(SceneNames.Hall);
                    break;
            }
        }

        void PoolCoach_OnMatchComplite()
        {
            PoolCoach.Instance.SetCalculateTimeEnable(false);

            int matchCoin = 0;
            int winId = PoolPlayer.GetHitWin();
            int isWin;
            if (winId == -1) isWin = 0;
            else
            {
                PoolPlayer.SetWinner(winId);

                if (PoolPlayer.mainPlayer.isWinner)
                {
                    isWin = 1;
                    matchCoin = (int)PoolPlayer.mainPlayer.matchCoin;
                }
                else
                {
                    isWin = -1;
                    matchCoin = (int)PoolPlayer.otherPlayer.matchCoin * -1;
                }
            }
            int minute = 0;
            float average = PoolPlayer.mainPlayer.hitCnt / (float)PoolPlayer.inning;
            int highRun = PoolPlayer.mainPlayer.hitHigh;
            int inning = PoolPlayer.inning;
            


            Debug.Log($"PoolCoach_OnMatchComplite  >>>>> isWin : {isWin}, matchCoin : {matchCoin}, inning : {inning}, minute : {minute}, average : {average}, highRun : {highRun}  ");

            physicsManager.CosmosAllDeactive();
            drawStuffPos.SetStuffAllDeactive();
            popMatchFinish.MatchResultView(isWin, matchCoin, inning, minute, average, highRun);
            MatchCompliteMotionAsync().Forget();
        }

        public async UniTaskVoid MatchCompliteMotionAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: this.GetCancellationTokenOnDestroy());
            popMatchFinish.MatchResultClose();
            ges.Open(GesPnl.Pnl.One);
            Debug.Log("MatchCompliteMotion");
        }

        public void btnMatchEnd_Click()
        {
            Debug.Log("btnMatchEnd_Click ... ");
            PoolCoach.Instance.SelfAbort();
            NetworkManager.network.LeaveMatch();
        }

        void PoolCoach_OnNoticeHitBall(NoticeHitBall notice)
        {
            
            //Debug.Log($"PoolCoach_OnNoticeHitBall rectInningBarOther : {(rectInningBarOther == null?"xxx": rectInningBarOther.name)}");
            //Debug.Log($"PoolCoach_OnNoticeHitBall gamFinishSelf : {(gamFinishSelf==null?"xxx": gamFinishSelf.name)}");
            //Debug.Log($"PoolCoach_OnNoticeHitBall gamFinishOther : {(gamFinishOther == null?"xxx": gamFinishOther.name)}");
            //Debug.Log($"PoolCoach_OnNoticeHitBall {notice.ToString()}");
            switch (notice)
            {
                case NoticeHitBall.Foul:
                    popNoticeMsg.NoticeView(notice);
                    if (PoolPlayer.mainPlayer.myTurn)
                        HitSelfMinus().Forget();
                    else
                        HitOtherMinus().Forget();

                    break;
                case NoticeHitBall.Finish:
                    if (PoolPlayer.mainPlayer.myTurn)
                    {
                        gamFinishSelf.anchoredPosition = new Vector2(20f, 0f);
                        //gamFinishSelf.SetActive(true);
                    }
                    else
                    {
                        gamFinishOther.anchoredPosition = new Vector2(-20f, 0f);
                        //gamFinishOther.SetActive(true);
                    }
                    popNoticeMsg.NoticeView(notice);
                    break;
                case NoticeHitBall.Survival:
                    popNoticeMsg.NoticeView(notice);
                    break;
            }
        }

        void PoolCoach_OnFinishAchieve(int playerId)
        {
            if(playerId == 0)
            {
                txtFinishSelf.color = new Color(1, 1, 1, 0.15f);
            }
            else
            {
                txtFinishOther.color = new Color(1, 1, 1, 0.15f);
            }
        }

        void PoolCoach_OnMatchTimeEndProcess(int sec)
        {
            MatchTimeStart(sec);
        }

    }
}

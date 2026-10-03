using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        sealed class ReplayFrame
        {
            public bool cueRecorded, cueVisible;
            public Vector3 cuePosition, cueContact, cueSlide;
            public Quaternion cueRotation;
            public Vector3[] positions, velocities, spins;
            public Quaternion[] rotations;
            public float time;
        }
        readonly List<ReplayFrame> replayFrames = new List<ReplayFrame>();
        readonly List<ReplayFrame> recordingBuffer = new List<ReplayFrame>();
        const int MaxReplayFrames = 60000;
        readonly Queue<ReplayFrame> cuePreparation = new Queue<ReplayFrame>();
        bool recordingCuePreparation;

        public void BeginReplayCueStroke()
        {
            if (!IsLocalPractice || IsPracticeReplay || IsBallPlacement || replayLoadMode ||
                inMove || scatterPending || recordingShot) return;
            // A committed forward stroke replaces any abandoned attempt, never the last saved shot.
            ClearCuePreparation();
            recordingCuePreparation = true;
            RecordCuePreparation();
        }

        void RecordCuePreparation()
        {
            if (!recordingCuePreparation || IsPracticeReplay || inMove) return;
            // Only the short committed stroke before ball contact is buffered.
            var frame = cuePreparation.Count >= 128 ? cuePreparation.Dequeue() : null;
            cuePreparation.Enqueue(CaptureReplayFrame(0, frame));
        }

        void ClearCuePreparation()
        {
            recordingCuePreparation = false;
            cuePreparation.Clear();
        }
        ReplayFrame replayReturn;
        struct ReplayStroke
        {
            public bool available;
            public float power, follow;
            public Vector2 point;
            public Quaternion cueRotation;
        }
        ReplayStroke replayStroke;
        Vector2 replayReturnPoint;
        Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.BallPointBig replayPointPanel;
        LineRenderer replayTrail;
        bool replayLiveLineEnabled;
        int replayTrailFrame = -1;

        void UpdateReplayTrail()
        {
            if (!replayTrail)
            {
                var obj = new GameObject("Replay Cue Trail", typeof(LineRenderer));
                obj.transform.SetParent(transform, false);
                obj.layer = line.gameObject.layer;
                replayTrail = obj.GetComponent<LineRenderer>();
                replayTrail.sharedMaterials = line.sharedMaterials;
                replayTrail.widthCurve = line.widthCurve;
                replayTrail.widthMultiplier = line.widthMultiplier;
                replayTrail.colorGradient = line.colorGradient;
                replayTrail.alignment = line.alignment;
                replayTrail.textureMode = line.textureMode;
                replayTrail.numCornerVertices = line.numCornerVertices;
                replayTrail.numCapVertices = line.numCapVertices;
                replayTrail.sortingLayerID = line.sortingLayerID;
                replayTrail.sortingOrder = line.sortingOrder;
                replayTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                replayTrail.receiveShadows = false;
                replayTrail.useWorldSpace = true;
            }
            if (ReplayFrameIndex < replayTrailFrame) replayTrailFrame = -1;
            replayTrail.enabled = true;
            // Append only newly visited recorded frames; never simulate replay physics.
            if (replayTrailFrame == ReplayFrameIndex) return;
            replayTrail.positionCount = ReplayFrameIndex + 1;
            for (int i = replayTrailFrame + 1; i <= ReplayFrameIndex; i++)
            {
                var position = replayFrames[i].positions[replayCueId];
                position.y = 0f;
                replayTrail.SetPosition(i, position);
            }
            replayTrailFrame = ReplayFrameIndex;
        }
        bool recordingShot, replayPlaying, replayControl, replayCueActive;
        float replayStep, replayClock, replayTimeScale;
        int replayCueId;
        public bool IsPracticeReplay { get; private set; }
        public int ReplayFrameIndex { get; private set; }
        public int ReplayFrameCount => replayFrames.Count;
        public bool CanReplayShot => IsLocalPractice && !IsBallPlacement && !recordingShot && !inMove && !scatterPending &&
            !shotController.IsShotAnimating && replayFrames.Count > 1;
        RectTransform replayPanel;
        Button replayPlay, replayNext, replayReset, replayExit;
        Button replaySlower, replayFaster;
        Slider replaySeek;
        TMP_Text replayInfo, replayPlayText, replaySpeedText, matchReplayHeading, matchReplayTitleLabel;
        TMP_Text matchReplayProgressLabel, matchReplaySpeedLabel;
        readonly float[] replaySpeeds = { .5f, 1f, 2f, 4f, 8f, 16f, 32f };
        int replaySpeedIndex = 1;
        RectTransform replayDragHeader;

        void CreateReplayDragHeader()
        {
            var header = new GameObject("Replay Drag Header", typeof(RectTransform), typeof(Image),
                typeof(Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.ReplayPanelDrag));
            replayDragHeader = header.GetComponent<RectTransform>();
            replayDragHeader.SetParent(replayPanel, false);
            replayDragHeader.anchoredPosition = new Vector2(-351, 0);
            replayDragHeader.sizeDelta = new Vector2(20, 204);
            header.GetComponent<Image>().color = new Color(.12f, .28f, .32f, .96f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(replayDragHeader, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = replayPlayText.font; label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.text = "⋮\n⋮\n⋮";
            header.GetComponent<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.ReplayPanelDrag>().Initialize(replayPanel);
        }

        ReplayFrame NewReplayFrame() => new ReplayFrame { positions = new Vector3[ballcs.Length],
            rotations = new Quaternion[ballcs.Length], velocities = new Vector3[ballcs.Length], spins = new Vector3[ballcs.Length] };

        void PrepareRecordingBuffer()
        {
            // Allocate the first 30 seconds while entering the scene, then reuse across shots.
            for (int i = recordingBuffer.Count; i < 3000; i++) recordingBuffer.Add(NewReplayFrame());
            if (replayFrames.Capacity < 3000) replayFrames.Capacity = 3000;
        }

        ReplayFrame CaptureReplayFrame(float time, ReplayFrame frame = null)
        {
            if (frame == null) frame = NewReplayFrame();
            frame.time = time;
            frame.cueRecorded = true;
            frame.cueVisible = shotController.cueVertical.gameObject.activeSelf;
            frame.cuePosition = shotController.cueVertical.position;
            frame.cueRotation = shotController.cueVertical.rotation;
            frame.cueContact = shotController.cueDisplacement.localPosition;
            frame.cueSlide = shotController.cueSlider.localPosition;
            for (int i = 0; i < ballcs.Length; i++)
            {
                var body = ballcs[i].body;
                frame.positions[i] = body.position; frame.rotations[i] = body.rotation;
                frame.velocities[i] = body.linearVelocity; frame.spins[i] = body.angularVelocity;
            }
            return frame;
        }
        void BeginShotRecording()
        {
            var preparation = cuePreparation.ToArray();
            ClearShotReplay();
            if (!IsLocalPractice || scatterPending) return;
            replayCueId = shotController.cueBall.id;
            var pointPanel = FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.BallPointBig>(FindObjectsInactive.Include);
            replayStroke = new ReplayStroke {
                available = pointPanel != null,
                power = shotController.force, follow = shotController.pull,
                point = pointPanel ? pointPanel.CapturePracticePoint() : Vector2.zero,
                cueRotation = shotController.cuePivot.localRotation
            };
            replayStep = Time.fixedDeltaTime;
            foreach (var frame in preparation)
            {
                frame.time = replayFrames.Count * replayStep;
                replayFrames.Add(frame);
            }
            recordingShot = true;
            RecordShotFrame();
        }
        void RecordShotFrame()
        {
            if (!recordingShot) return;
            // At 100 Hz this retains up to five minutes of the latest shot.
            if (replayFrames.Count < MaxReplayFrames)
            {
                int index = replayFrames.Count;
                while (index >= recordingBuffer.Count) recordingBuffer.Add(NewReplayFrame());
                replayFrames.Add(CaptureReplayFrame(index * replayStep, recordingBuffer[index]));
            }
        }
        void FinishShotRecording()
        {
            if (shotController.IsShotAnimating) return;
            RecordShotFrame();
            recordingShot = false;
        }
        void ClearShotReplay()
        {
            ExitPracticeReplay();
            recordingShot = false; replayFrames.Clear(); ReplayFrameIndex = 0;
            ClearCuePreparation();
            replayStroke = default;
        }
        bool EnterPracticeReplay()
        {
            if (IsPracticeReplay) return true;
            if (!CanReplayShot) return false;
            replayReturn = CaptureReplayFrame(0);
            replayPointPanel = FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.BallPointBig>(FindObjectsInactive.Include);
            if (replayPointPanel) replayReturnPoint = replayPointPanel.CapturePracticePoint();
            shotController.ShowReplayStroke(replayStroke.available, replayStroke.power, replayStroke.follow,
                replayStroke.available ? replayStroke.cueRotation : Quaternion.identity);
            if (replayPointPanel) replayPointPanel.RestorePracticePoint(replayStroke.available ? replayStroke.point : Vector2.zero);
            replayTimeScale = Time.timeScale;
            replayControl = ShotCtrl.canControl;
            replayCueActive = shotController.cueVertical.gameObject.activeSelf;
            replayLiveLineEnabled = line.enabled; line.enabled = false; replayTrailFrame = -1;
            IsPracticeReplay = true; Time.timeScale = 0; ShotCtrl.canControl = false;
            ReplayFrameIndex = 0; replayClock = 0;
            ApplyReplayFrame(replayFrames[0]);
            var first = replayFrames[0];
            if (first.cueRecorded || replayStroke.available)
                shotController.ShowReplayAimOverlap(replayCueId, first.positions,
                    first.cueRecorded ? first.cueRotation : shotController.cuePivot.parent.rotation * replayStroke.cueRotation);
            else shotController.HideReplayAimOverlap();
            return true;
        }
        void ApplyReplayFrame(ReplayFrame frame)
        {
            for (int i = 0; i < ballcs.Length; i++)
            {
                var ball = ballcs[i];
                // Old three-ball recordings retain their original board during playback.
                ball.gameObject.SetActive(i < frame.positions.Length);
                ball.ballShadow.gameObject.SetActive(i < frame.positions.Length);
                if (i >= frame.positions.Length) continue;
                ball.body.position = frame.positions[i]; ball.body.rotation = frame.rotations[i];
                ball.transform.SetPositionAndRotation(frame.positions[i], frame.rotations[i]);
                ball.SetBallShadowAndBlickBlick();
            }
            Physics.SyncTransforms();
            shotController.cueVertical.gameObject.SetActive(frame.cueRecorded && frame.cueVisible);
            if (frame.cueRecorded)
            {
                shotController.cueVertical.SetPositionAndRotation(frame.cuePosition, frame.cueRotation);
                shotController.cueDisplacement.localPosition = frame.cueContact;
                shotController.cueSlider.localPosition = frame.cueSlide;
            }
            if (IsPracticeReplay && frame != replayReturn) UpdateReplayTrail();
        }
        public void TogglePracticeReplay()
        {
            if (!EnterPracticeReplay()) return;
            if (ReplayFrameIndex == replayFrames.Count - 1) ResetPracticeReplay();
            replaySlotMessage = "";
            replayPlaying = !replayPlaying;
        }
        public void NextPracticeReplayFrame()
        {
            if (!EnterPracticeReplay()) return;
            replaySlotMessage = "";
            replayPlaying = false; replayClock = 0;
            ReplayFrameIndex = Mathf.Min(ReplayFrameIndex + 1, replayFrames.Count - 1);
            ApplyReplayFrame(replayFrames[ReplayFrameIndex]);
        }
        public void ResetPracticeReplay()
        {
            if (!EnterPracticeReplay()) return;
            replaySlotMessage = "";
            replayPlaying = false; replayClock = 0; ReplayFrameIndex = 0;
            ApplyReplayFrame(replayFrames[0]);
        }
        void StopReplayView()
        {
            if (!IsPracticeReplay) return;
            replayPlaying = false;
            if (replayTrail) { replayTrail.enabled = false; replayTrail.positionCount = 0; }
            replayTrailFrame = -1; line.enabled = replayLiveLineEnabled;
            ApplyReplayFrame(replayReturn);
            Time.timeScale = replayTimeScale; ShotCtrl.canControl = replayControl;
            shotController.cueVertical.gameObject.SetActive(replayCueActive);
            shotController.HideReplayStroke();
            shotController.HideReplayAimOverlap();
            if (replayPointPanel) replayPointPanel.RestorePracticePoint(replayReturnPoint);
            IsPracticeReplay = false;
        }
        Button ReplayButton(string name, string text, float x, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(replayPanel, false);
            var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(124, 38); rect.anchoredPosition = new Vector2(x, -80);
            obj.GetComponent<Image>().color = new Color(.1f, .23f, .28f, .95f);
            var button = obj.GetComponent<Button>(); button.onClick.AddListener(action);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(rect, false); label.rectTransform.sizeDelta = rect.sizeDelta;
            label.font = undoButton.GetComponentInChildren<TextMeshProUGUI>(true).font; label.fontSize = 20;
            label.text = text; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return button;
        }
        void ChangeReplaySpeed(int direction)
        {
            replaySpeedIndex = Mathf.Clamp(replaySpeedIndex + direction, 0, replaySpeeds.Length - 1);
            if (replaySpeedText) replaySpeedText.text = $"{replaySpeeds[replaySpeedIndex]:0.#}×";
        }
        void CloseReplayPanel()
        {
            if (matchArchiveSaveMode) CloseMatchReplaySavePanel();
            else ExitPracticeReplay();
        }
        void ApplyMatchArchiveSaveLayout()
        {
            if (!replayPanel) return;
            replayPanel.anchoredPosition = new Vector2(0, 170);
            replayPanel.sizeDelta = new Vector2(860, 320);
            if (replaySlotButtons != null) for (int i = 0; i < replaySlotButtons.Length; i++)
            {
                var rect = (RectTransform)replaySlotButtons[i].transform;
                rect.anchoredPosition = new Vector2(-300 + (i % ReplaySlotsPerRow) * 68, 32 - (i / ReplaySlotsPerRow) * 42);
            }
            if (!matchReplayTitleLabel)
            {
                matchReplayTitleLabel = new GameObject("Match Replay Title Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                matchReplayTitleLabel.transform.SetParent(replayPanel, false);
                matchReplayTitleLabel.rectTransform.sizeDelta = new Vector2(70, 28);
                matchReplayTitleLabel.font = replayPlayText.font; matchReplayTitleLabel.fontSize = 18;
                matchReplayTitleLabel.alignment = TextAlignmentOptions.Center;
            }
            matchReplayTitleLabel.rectTransform.anchoredPosition = new Vector2(-245, 105);
            matchReplayTitleLabel.text = "제목:";
            if (replayTitle)
            {
                var rect = (RectTransform)replayTitle.transform;
                rect.anchoredPosition = new Vector2(0, 105); rect.sizeDelta = new Vector2(420, 32);
            }
            if (replayFolderScroll)
                replayFolderScroll.GetComponent<RectTransform>().anchoredPosition = new Vector2(-75, 140);
            if (replayFolderAdd) ((RectTransform)replayFolderAdd.transform).anchoredPosition = new Vector2(226, 140);
            if (replayFolderDelete) ((RectTransform)replayFolderDelete.transform).anchoredPosition = new Vector2(305, 140);
            if (replayInfo)
            {
                replayInfo.rectTransform.anchoredPosition = new Vector2(0, 68);
                replayInfo.rectTransform.sizeDelta = new Vector2(760, 28);
            }
            replayPlay.gameObject.SetActive(false); replayNext.gameObject.SetActive(false); replayReset.gameObject.SetActive(false);
            if (replaySlower) replaySlower.gameObject.SetActive(false);
            if (replayFaster) replayFaster.gameObject.SetActive(false);
            if (replaySeek) replaySeek.gameObject.SetActive(false);
            if (replaySpeedText) replaySpeedText.gameObject.SetActive(false);
            replaySave.gameObject.SetActive(true);
            ((RectTransform)replaySave.transform).anchoredPosition = new Vector2(220, -105);
            replaySave.GetComponentInChildren<TMP_Text>().text = "저장";
            ((RectTransform)replayExit.transform).anchoredPosition = new Vector2(360, -105);
            replayExit.gameObject.SetActive(true);
            replayExit.GetComponentInChildren<TMP_Text>().text = "취소";
            replayPanel.SetAsLastSibling();
        }
        void SeekMatchReplay(float normalized)
        {
            if (!IsMatchReplayBrowser || !CanReplayShot || replayFrames.Count < 2) return;
            if (!IsPracticeReplay && !EnterPracticeReplay()) return;
            replayPlaying = false; replayClock = 0;
            ReplayFrameIndex = Mathf.Clamp(Mathf.RoundToInt(normalized * (replayFrames.Count - 1)), 0, replayFrames.Count - 1);
            ApplyReplayFrame(replayFrames[ReplayFrameIndex]);
        }
        void UpdatePracticeReplay()
        {
            if (!replayPanel)
            {
                replayPanel = new GameObject("Practice Replay Controls", typeof(RectTransform)).GetComponent<RectTransform>();
                replayPanel.SetParent(undoButton.transform.parent, false);
                replayPanel.anchorMin = replayPanel.anchorMax = new Vector2(.5f, 0);
                replayPanel.anchoredPosition = new Vector2(0, IsMatchReplayBrowser ? 170 : 150);
                replayPanel.sizeDelta = IsMatchReplayBrowser ? new Vector2(1040, 320) : new Vector2(730, 224);
                var background = replayPanel.gameObject.AddComponent<Image>();
                background.color = new Color(.025f, .07f, .085f, .94f);
                replayPlay = ReplayButton("Replay Play", "재생", IsMatchReplayBrowser ? -205 : IsReplayBrowser ? -144 : -264, TogglePracticeReplay);
                replayNext = ReplayButton("Replay Next", "다음 프레임", IsMatchReplayBrowser ? -65 : IsReplayBrowser ? 0 : -132, NextPracticeReplayFrame);
                replayReset = ReplayButton("Replay Reset", IsMatchReplayBrowser ? "처음으로" : "재생 처음으로", IsMatchReplayBrowser ? 75 : IsReplayBrowser ? 144 : 0, ResetPracticeReplay);
                replayExit = ReplayButton("Replay Exit", "저장 닫기", 132, CloseReplayPanel);
                replayPlayText = replayPlay.GetComponentInChildren<TMP_Text>();
                replayInfo = new GameObject("Replay Time", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                replayInfo.transform.SetParent(replayPanel, false); replayInfo.rectTransform.sizeDelta = new Vector2(IsMatchReplayBrowser ? 760 : 310, 28);
                replayInfo.rectTransform.anchoredPosition = new Vector2(IsMatchReplayBrowser ? 0 : -170, IsMatchReplayBrowser ? 68 : 48);
                replayInfo.font = replayPlayText.font; replayInfo.fontSize = 19; replayInfo.alignment = TextAlignmentOptions.MidlineLeft; replayInfo.overflowMode = TextOverflowModes.Ellipsis; replayInfo.raycastTarget = false;
                if (IsMatchReplayBrowser)
                {
                    replaySlower = ReplayButton("Replay Slower", "<", -118, () => ChangeReplaySpeed(-1));
                    replayFaster = ReplayButton("Replay Faster", ">", -2, () => ChangeReplaySpeed(1));
                    foreach (var speedButton in new[] { replaySlower, replayFaster })
                    {
                        var speedRect = (RectTransform)speedButton.transform; speedRect.sizeDelta = new Vector2(44, 38);
                        speedButton.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = speedRect.sizeDelta;
                    }
                    replaySpeedText = new GameObject("Replay Speed", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                    replaySpeedText.transform.SetParent(replayPanel, false); replaySpeedText.rectTransform.sizeDelta = new Vector2(70, 38);
                    replaySpeedText.rectTransform.anchoredPosition = new Vector2(-60, -105); replaySpeedText.font = replayPlayText.font;
                    replaySpeedText.fontSize = 20; replaySpeedText.alignment = TextAlignmentOptions.Center; replaySpeedText.text = "1×";
                    matchReplayProgressLabel = CreateReplayControlLabel("진행:", new Vector2(-466, -105), new Vector2(70, 38));
                    matchReplaySpeedLabel = CreateReplayControlLabel("속도:", new Vector2(-173, -105), new Vector2(66, 38));
                    var seekObject = new GameObject("Replay Progress", typeof(RectTransform), typeof(Image), typeof(Slider));
                    var seekRect = (RectTransform)seekObject.transform; seekRect.SetParent(replayPanel, false);
                    seekRect.sizeDelta = new Vector2(220, 18); seekRect.anchoredPosition = new Vector2(-320, -105);
                    var seekImage = seekObject.GetComponent<Image>(); seekImage.color = new Color(.28f, .34f, .36f);
                    replaySeek = seekObject.GetComponent<Slider>(); replaySeek.minValue = 0; replaySeek.maxValue = 1;
                    replaySeek.targetGraphic = seekImage; replaySeek.direction = Slider.Direction.LeftToRight;
                    var fillArea = new GameObject("Fill Area", typeof(RectTransform));
                    var fillAreaRect = (RectTransform)fillArea.transform; fillAreaRect.SetParent(seekRect, false);
                    fillAreaRect.anchorMin = Vector2.zero; fillAreaRect.anchorMax = Vector2.one;
                    fillAreaRect.offsetMin = new Vector2(8, 0); fillAreaRect.offsetMax = new Vector2(-8, 0);
                    var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                    var fillRect = (RectTransform)fill.transform; fillRect.SetParent(fillAreaRect, false);
                    fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
                    fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                    replaySeek.fillRect = fillRect;
                    fill.GetComponent<Image>().color = new Color(.95f, .72f, .2f);
                    var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                    var handleRect = (RectTransform)handle.transform; handleRect.SetParent(seekRect, false);
                    handleRect.sizeDelta = new Vector2(16, 28); handle.GetComponent<Image>().color = new Color(.95f, .72f, .2f);
                    replaySeek.handleRect = handleRect;
                    replaySeek.onValueChanged.AddListener(SeekMatchReplay);
                    matchReplayHeading = new GameObject("Match Replay Heading", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                    matchReplayHeading.transform.SetParent(replayPanel, false); matchReplayHeading.rectTransform.sizeDelta = new Vector2(360, 28);
                    matchReplayHeading.rectTransform.anchoredPosition = new Vector2(0, 178); matchReplayHeading.font = replayPlayText.font;
                    matchReplayHeading.fontSize = 22; matchReplayHeading.alignment = TextAlignmentOptions.Center; matchReplayHeading.text = "리플레이 - 시합";
                    matchReplayTitleLabel = new GameObject("Match Replay Title Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                    matchReplayTitleLabel.transform.SetParent(replayPanel, false); matchReplayTitleLabel.rectTransform.sizeDelta = new Vector2(70, 28);
                    matchReplayTitleLabel.rectTransform.anchoredPosition = new Vector2(-245, 105); matchReplayTitleLabel.font = replayPlayText.font;
                    matchReplayTitleLabel.fontSize = 18; matchReplayTitleLabel.alignment = TextAlignmentOptions.Center; matchReplayTitleLabel.text = "제목:";
                    replaySlower.GetComponent<RectTransform>().anchoredPosition = new Vector2(-118, -105);
                    replayPlay.GetComponent<RectTransform>().anchoredPosition = new Vector2(120, -105);
                    replayNext.GetComponent<RectTransform>().anchoredPosition = new Vector2(260, -105);
                    replayReset.GetComponent<RectTransform>().anchoredPosition = new Vector2(400, -105);
                    replayFaster.GetComponent<RectTransform>().anchoredPosition = new Vector2(-2, -105);
                }
            }
            UpdateReplaySlots();
            if (!replayDragHeader) CreateReplayDragHeader();
            replayPanel.gameObject.SetActive(matchArchiveSaveMode ||
                IsLocalPractice && (IsReplayBrowser ? replayLoadMode : replaySavePanelOpen));
            if (IsPracticeReplay && replayPlaying)
            {
                replayClock += Time.unscaledDeltaTime * (IsMatchReplayBrowser ? replaySpeeds[replaySpeedIndex] : 1f);
                while (replayClock >= replayStep && ReplayFrameIndex < replayFrames.Count - 1)
                { replayClock -= replayStep; ReplayFrameIndex++; }
                ApplyReplayFrame(replayFrames[ReplayFrameIndex]);
                if (ReplayFrameIndex == replayFrames.Count - 1) replayPlaying = false;
            }
            replayPlay.interactable = replayNext.interactable = replayReset.interactable = CanReplayShot;
            replayExit.interactable = replaySavePanelOpen;
            replayExit.gameObject.SetActive(!IsReplayBrowser || matchArchiveSaveMode);
            replayPlayText.text = replayPlaying ? "일시정지" : "재생";
            replayInfo.text = !string.IsNullOrEmpty(replaySlotMessage) ? replaySlotMessage : recordingShot ? "샷 자동 기록 중" : IsPracticeReplay ?
                $"{ReplayFrameIndex + 1} / {replayFrames.Count} 프레임 · {replayFrames[ReplayFrameIndex].time:F2}초" : "직전 샷 기록 · 재생으로 운동 상태 확인";
            if (IsPracticeReplay && !replayStroke.available) replayInfo.text = "샷 설정 정보 없음 · " + replayInfo.text;
            if (IsMatchReplayBrowser)
            {
                replayInfo.text = MatchReplaySummary;
                replaySeek.SetValueWithoutNotify(replayFrames.Count > 1 ? (float)ReplayFrameIndex / (replayFrames.Count - 1) : 0);
                replaySeek.interactable = CanReplayShot;
                replaySlower.interactable = replaySpeedIndex > 0;
                replayFaster.interactable = replaySpeedIndex < replaySpeeds.Length - 1;
                replaySpeedText.text = $"{replaySpeeds[replaySpeedIndex]:0.#}×";
            }
        }
        TMP_Text CreateReplayControlLabel(string value, Vector2 position, Vector2 size)
        {
            var label = new GameObject("Label " + value, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(replayPanel, false); label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size; label.font = replayPlayText.font; label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center; label.text = value; label.raycastTarget = false;
            return label;
        }
    }
}

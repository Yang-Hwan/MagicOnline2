using System;
using System.Collections.Generic;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        sealed class UndoSnapshot
        {
            public Vector3[] positions;
            public Quaternion[] rotations;
            public PoolPlayer[] players;
            public int turn, order, inning;
            public float maxTime;
            public PoolState rules;
            public ItemMatchState.Snapshot items;
            public Dictionary<long, float> lifetimes;
            public ShotCtrl.PracticeAim aim;
            public Vector2 point;
            public UnityEngine.Random.State random;
            public int practiceAttempts, practiceStreak;
        }
        UndoSnapshot undoSnapshot;
        int undoVersion;
        int simulationVersion;
        Button undoButton;
        RectTransform undoButtonRect;
        public bool CanUndoShot => undoSnapshot != null && !IsBallPlacement &&
            IsLocalPractice && HasItemAuthority && !scatterPending && !shotController.IsShotAnimating;

        void CaptureUndoState()
        {
            if (!IsLocalPractice || !HasItemAuthority || scatterPending || inMove || !PoolCoach.Instance.isMatchTimePlay) return;
            var coach = PoolCoach.Instance;
            undoSnapshot = new UndoSnapshot {
                positions = Array.ConvertAll(ballcs, ball => ball.body.position),
                rotations = Array.ConvertAll(ballcs, ball => ball.body.rotation),
                players = PoolPlayer.CapturePracticePlayers(), turn = PoolPlayer.turnId,
                order = PoolPlayer.ord, inning = PoolPlayer.inning,
                maxTime = coach.maxPlayTime,
                rules = PoolLogic.gameState.Copy(), items = Items.Capture(),
                lifetimes = new Dictionary<long, float>(), aim = shotController.CapturePracticeAim(),
                random = UnityEngine.Random.state
            };
            undoSnapshot.practiceAttempts = coach.practiceAttempts;
            undoSnapshot.practiceStreak = coach.practiceStreak;
            foreach (var view in itemViews.Values) undoSnapshot.lifetimes.Add(view.SpawnId, view.RemainingLifetime);
            var pointPanel = FindAnyObjectByType<BallPointBig>(FindObjectsInactive.Include);
            if (pointPanel) undoSnapshot.point = pointPanel.CapturePracticePoint();
        }

        public bool UndoLastShot()
        {
            if (!CanUndoShot) return false;
            ExitPracticeReplay();
            var saved = undoSnapshot;
            undoVersion++;
            ConfigureItemSession(Guid.NewGuid().ToString("N"), true);
            externalItemSession = false;
            ObjectPooler.instance.SetAllInactivePool();
            Items.Applied -= ApplyItemChange;
            Items.SnapshotApplied -= RestoreItemViews;
            Items = ItemMatchState.CreatePracticeBranch(saved.items, Items.MatchId);
            Items.Applied += ApplyItemChange;
            Items.SnapshotApplied += RestoreItemViews;
            foreach (var score in saved.items.scores) appliedBonuses[score.playerId] = score.bonus;
            PoolPlayer.RestorePracticePlayers(saved.players, saved.turn, saved.order, saved.inning);
            PoolLogic.RestorePracticeState(saved.rules);
            PoolCoach.Instance.RestorePracticeScore(saved.practiceAttempts, saved.practiceStreak);
            RestoreItemViews();
            foreach (var view in itemViews.Values) view.RestoreRemainingLifetime(saved.lifetimes[view.SpawnId]);
            for (int i = 0; i < ballcs.Length; i++)
            {
                var ball = ballcs[i];
                ball.strokeFollowThrough=-1f;
                ball.strokeSpinPersistence=-1f;
                ball.body.linearVelocity = Vector3.zero;
                ball.body.angularVelocity = Vector3.zero;
                ball.body.position = saved.positions[i];
                ball.body.rotation = saved.rotations[i];
                ball.transform.SetPositionAndRotation(saved.positions[i], saved.rotations[i]);
                ball.body.Sleep();
                ball.SetBallShadowAndBlickBlick();
            }
            Physics.SyncTransforms();
            inMove = false; moveTime = 0; line.positionCount = 0;
            PoolCoach.Instance.RestorePracticeTurn(0f, saved.maxTime);
            foreach (var ball in ballcs) ball.OnState(BallState.SetState);
            shotController.RestorePracticeAim(saved.aim);
            var pointPanel = FindAnyObjectByType<BallPointBig>(FindObjectsInactive.Include);
            if (pointPanel) pointPanel.RestorePracticePoint(saved.point);
            var panel = FindAnyObjectByType<PnlMatch>();
            if (panel) panel.RefreshAfterPracticeUndo();
            var achievement = FindAnyObjectByType<AniCtrl>();
            if (achievement)
            {
                foreach (var child in achievement.GetComponentsInChildren<RectTransform>(true)) child.DOKill();
                achievement.sideBeamObj.transform.localScale = Vector3.zero;
                achievement.achieveMsgObj.transform.localScale = Vector3.zero;
                achievement.runPathMsgObj.transform.localScale = Vector3.zero;
            }
            foreach (var particles in cushionHit.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            UnityEngine.Random.state = saved.random;
            return true;
        }

        void CreateUndoButton()
        {
            var root = new GameObject("Practice Undo UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            var buttonObject = new GameObject("Undo Last Shot", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            undoButtonRect = buttonObject.GetComponent<RectTransform>();
            undoButtonRect.anchorMin = undoButtonRect.anchorMax = new Vector2(.5f, 0);
            undoButtonRect.pivot = new Vector2(.5f, 0);
            undoButtonRect.anchoredPosition = new Vector2(0, 18);
            undoButtonRect.sizeDelta = new Vector2(200, 52);
            buttonObject.GetComponent<Image>().color = new Color(.12f, .25f, .3f, .96f);
            undoButton = buttonObject.GetComponent<Button>();
            var colors = undoButton.colors;
            colors.disabledColor = new Color(.4f, .4f, .4f, .5f);
            undoButton.colors = colors;
            undoButton.onClick.AddListener(() => UndoLastShot());
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            var panel = FindAnyObjectByType<PnlMatch>();
            if (panel) label.font = panel.transform.Find("PlayerBoxs/PlayerSelf/TxtName").GetComponent<TMP_Text>().font;
            label.text = "되돌리기";
            label.fontSize = 26;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            undoButton.interactable = false;
        }
        public bool IsPointerOverUndoButton() => IsPointerOverUndoButton(Input.mousePosition);
        bool IsPointerOverUndoButton(Vector2 screenPosition) =>
            (optionsRect && optionsRect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(optionsRect, screenPosition)) ||
            undoButton && undoButton.gameObject.activeInHierarchy &&
            (RectTransformUtility.RectangleContainsScreenPoint(undoButtonRect, screenPosition) ||
                (placementButton && placementButton.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)placementButton.transform, screenPosition)) ||
                (replayLoad && replayLoad.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)replayLoad.transform, screenPosition)) ||
                (replayPanel && replayPanel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(replayPanel, screenPosition)));
        void LateUpdate()
        {
            UpdatePracticeOptions();
            if (!undoButton) return;
            undoButton.gameObject.SetActive(!externalItemSession && !IsReplayBrowser);
            undoButton.interactable = CanUndoShot;
            UpdatePracticeReplay();
            UpdateBallPlacement();
            bool interpolate = inMove && !scatterPending && !IsPracticeReplay && !IsBallPlacement;
            float alpha = Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / Time.fixedDeltaTime));
            foreach (var ball in ballcs) ball.UpdateMotionVisual(interpolate, alpha);
            UpdatePracticeMotion();
        }
    }
}

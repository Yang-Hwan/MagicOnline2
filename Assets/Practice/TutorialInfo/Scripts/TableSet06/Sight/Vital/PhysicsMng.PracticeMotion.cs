using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        RectTransform motionRect, spinDot;
        TextMeshProUGUI motionLabel;
        Image spinImage;
        float spinPhase;
        public bool IsLocalPractice => !externalItemSession &&
            PoolCoach.Instance.Configuration.Execution != Assets.Scripts.Often.MatchExecutionMode.OnlineMatch;

        public static string ClassifyPracticeMotion(Vector3 velocity, Vector3 angularVelocity, float radius)
        {
            float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            if (speed < .008f && angularVelocity.magnitude < .15f) return "정지";
            if (speed < .008f) return "제자리 회전";
            Vector3 slip = Vector3.ProjectOnPlane(velocity + Vector3.Cross(angularVelocity, Vector3.down * radius), Vector3.up);
            return slip.magnitude <= Mathf.Max(.02f, speed * .08f) ? "구름" : "미끄러짐";
        }

        void UpdatePracticeMotion()
        {
            if (!motionRect)
            {
                var root = new GameObject("Practice Ball Motion", typeof(RectTransform));
                motionRect = root.GetComponent<RectTransform>();
                motionRect.SetParent(undoButton.transform.parent, false);
                motionRect.sizeDelta = new Vector2(180, 62);
                var labelObject = new GameObject("Motion Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(motionRect, false);
                motionLabel = labelObject.GetComponent<TextMeshProUGUI>();
                motionLabel.rectTransform.sizeDelta = new Vector2(170, 62);
                motionLabel.font = undoButton.GetComponentInChildren<TextMeshProUGUI>(true).font;
                motionLabel.fontSize = 20;
                motionLabel.alignment = TextAlignmentOptions.Center;
                motionLabel.raycastTarget = false;
                var dot = new GameObject("Rotation Marker", typeof(RectTransform), typeof(Image));
                spinDot = dot.GetComponent<RectTransform>();
                spinDot.SetParent(motionRect, false);
                spinDot.sizeDelta = new Vector2(6, 6);
                spinImage = dot.GetComponent<Image>();
                spinImage.raycastTarget = false;
            }
            var camera = Camera.main;
            bool visible = IsLocalPractice && !IsBallPlacement && !scatterPending && PoolCoach.Instance.isMatchTimePlay && camera && shotController.cueBall;
            motionRect.gameObject.SetActive(visible);
            if (!visible) return;
            var ball = IsPracticeReplay ? ballcs[replayCueId] : shotController.cueBall;
            Vector3 screen = camera.WorldToScreenPoint(ball.VisualPosition);
            if (screen.z <= 0) { motionRect.gameObject.SetActive(false); return; }
            var parent = (RectTransform)motionRect.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var point);
            point += new Vector2(0, 60);
            point.x = Mathf.Clamp(point.x, parent.rect.xMin + 95, parent.rect.xMax - 95);
            float bottomMargin = replayPanel && replayPanel.gameObject.activeInHierarchy ? 235f : 40f;
            point.y = Mathf.Clamp(point.y, parent.rect.yMin + bottomMargin, parent.rect.yMax - 40);
            motionRect.anchoredPosition = point;
            float radius = ball.GetComponent<SphereCollider>().radius * ball.transform.lossyScale.x;
            Vector3 angular = IsPracticeReplay ? replayFrames[ReplayFrameIndex].spins[replayCueId] : ball.body.angularVelocity;
            Vector3 velocity = IsPracticeReplay ? replayFrames[ReplayFrameIndex].velocities[replayCueId] : ball.body.linearVelocity;
            string state = ClassifyPracticeMotion(velocity, angular, radius);
            Color color = state == "미끄러짐" ? new Color(1f, .82f, .25f) : new Color(.65f, 1f, 1f);
            motionLabel.color = spinImage.color = color;
            string format = state == "정지" ? "정지\n회전 {0:1}회/초" : state == "제자리 회전" ? "제자리 회전\n회전 {0:1}회/초" :
                state == "구름" ? "구름\n회전 {0:1}회/초" : "미끄러짐\n회전 {0:1}회/초";
            motionLabel.SetText(format, angular.magnitude / (2f * Mathf.PI));
            // Marker is a scalar rotation-speed cue, not a world-space spin-axis arrow.
            spinPhase = Mathf.Repeat(IsPracticeReplay ? angular.magnitude * replayFrames[ReplayFrameIndex].time :
                spinPhase + angular.magnitude * Time.deltaTime, 2f * Mathf.PI);
            spinDot.anchoredPosition = new Vector2(-75 + Mathf.Cos(spinPhase) * 9, Mathf.Sin(spinPhase) * 9);
        }
    }
}

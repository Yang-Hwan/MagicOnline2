using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        public bool IsBallPlacement { get; private set; }
        Button placementButton;
        RectTransform[] placementBoxes;
        int draggedBall = -1;
        Vector3 dragOffset;
        float placementTimeScale;
        bool placementControl, placementCueVisible;
        ShotCtrl.PracticeAim placementAim;
        public bool CanPlaceBalls => IsLocalPractice && !inMove && !scatterPending && !shotController.IsShotAnimating &&
            !IsPracticeReplay && !replayLoadMode && PoolCoach.Instance.isMatchTimePlay;
        public void ToggleBallPlacement()
        {
            if (IsBallPlacement) { EndBallPlacement(); return; }
            if (!CanPlaceBalls) return;
            placementAim = shotController.CapturePracticeAim();
            placementTimeScale = Time.timeScale; placementControl = ShotCtrl.canControl;
            placementCueVisible = shotController.cueVertical.gameObject.activeSelf;
            IsBallPlacement = true; Time.timeScale = 0; ShotCtrl.canControl = false;
            shotController.SetPlacementGuidesHidden(true);
            shotController.cueVertical.gameObject.SetActive(false);
        }
        public void EndBallPlacement()
        {
            if (!IsBallPlacement) return;
            draggedBall = -1;
            IsBallPlacement = false; Time.timeScale = placementTimeScale;
            shotController.CueReadyShot(); shotController.RestorePracticeAim(placementAim);
            shotController.cueVertical.gameObject.SetActive(placementCueVisible);
            shotController.SetPlacementGuidesHidden(false);
            ShotCtrl.canControl = placementControl;
            if (placementBoxes != null) foreach (var box in placementBoxes) box.gameObject.SetActive(false);
        }
        float PlacementRadius(int index)
        {
            var ball = ballcs[index];
            return ball.GetComponent<SphereCollider>().radius * Mathf.Max(ball.transform.lossyScale.x, ball.transform.lossyScale.z);
        }
        public bool PlacePracticeBall(int index, Vector3 position)
        {
            if (!IsBallPlacement || index < 0 || index >= ballcs.Length) return false;
            if (float.IsNaN(position.x) || float.IsNaN(position.z) || float.IsInfinity(position.x) || float.IsInfinity(position.z)) return false;
            float radius = PlacementRadius(index);
            Vector3 a = drawStuffPos.st.position, b = drawStuffPos.en.position;
            position.x = Mathf.Clamp(position.x, Mathf.Min(a.x,b.x) + radius, Mathf.Max(a.x,b.x) - radius);
            position.z = Mathf.Clamp(position.z, Mathf.Min(a.z,b.z) + radius, Mathf.Max(a.z,b.z) - radius);
            position.y = ballcs[index].body.position.y;
            for (int i = 0; i < ballcs.Length; i++)
                if (i != index && Vector3.ProjectOnPlane(position - ballcs[i].body.position, Vector3.up).magnitude < radius + PlacementRadius(i) + .001f) return false;
            if ((position - ballcs[index].body.position).sqrMagnitude < 1e-10f) return true;
            // A changed board invalidates the previous live-shot undo and unsaved replay.
            ClearShotReplay(); undoSnapshot = null; simulationVersion++; line.positionCount = 0;
            var ball = ballcs[index]; ball.body.position = position; ball.transform.position = position;
            ball.body.linearVelocity = Vector3.zero; ball.body.angularVelocity = Vector3.zero; ball.body.Sleep();
            Physics.SyncTransforms(); ball.SetBallShadowAndBlickBlick();
            return true;
        }
        public int DraggedPlacementBall => draggedBall;
        Camera PlacementCamera => InputOutput.usedCamera ? InputOutput.usedCamera : Camera.main;
        public bool BeginPlacementDrag(Vector2 screenPosition)
        {
            if (!IsBallPlacement || !PlacementCamera) return false;
            var camera = PlacementCamera;
            int picked = -1; float closest = float.MaxValue;
            // Pick the visible ball directly. Board/UI colliders must not intercept selection.
            for (int i = 0; i < ballcs.Length; i++)
            {
                Vector3 centre = camera.WorldToScreenPoint(ballcs[i].body.position);
                if (centre.z <= 0) continue;
                Vector3 edge = camera.WorldToScreenPoint(ballcs[i].body.position + camera.transform.right * PlacementRadius(i));
                float radius = Mathf.Max(12f, Vector2.Distance(centre, edge) + 5f);
                float distance = Vector2.Distance(screenPosition, centre);
                if (distance <= radius && distance < closest) { picked = i; closest = distance; }
            }
            if (picked < 0) return false;
            var ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, ballcs[picked].body.position);
            if (!plane.Raycast(ray, out float hitDistance)) return false;
            draggedBall = picked;
            dragOffset = ballcs[picked].body.position - ray.GetPoint(hitDistance);
            if (placementBoxes != null) placementBoxes[picked].gameObject.SetActive(false);
            return true;
        }
        public bool MovePlacementDrag(Vector2 screenPosition)
        {
            if (!IsBallPlacement || draggedBall < 0 || !PlacementCamera) return false;
            var ray = PlacementCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, ballcs[draggedBall].body.position);
            return plane.Raycast(ray, out float distance) && PlacePracticeBall(draggedBall, ray.GetPoint(distance) + dragOffset);
        }
        public void EndPlacementDrag()
        {
            int released = draggedBall; draggedBall = -1;
            if (IsBallPlacement && released >= 0 && placementBoxes != null) placementBoxes[released].gameObject.SetActive(true);
        }
        void OnApplicationFocus(bool focused) { if (!focused) EndPlacementDrag(); }

        // Use each queued mouse event's own position, even when down/move/up arrive in one frame.
        // Polling Input.mousePosition can otherwise select at the end of a fast drag.
        void OnGUI()
        {
            if (!IsBallPlacement) return;
            var e = Event.current;
            if (e.button != 0) return;
            Vector2 position = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
            if (e.type == EventType.MouseDown)
            {
                if (!IsPointerOverUndoButton(position) && BeginPlacementDrag(position)) e.Use();
            }
            else if (e.type == EventType.MouseDrag && draggedBall >= 0)
            {
                MovePlacementDrag(position); e.Use();
            }
            else if (e.rawType == EventType.MouseUp && draggedBall >= 0)
            {
                MovePlacementDrag(position); EndPlacementDrag(); e.Use();
            }
        }

        void CreatePlacementUI()
        {
            placementButton = ReplayButton("Practice Ball Placement", "공재배치", 0, ToggleBallPlacement);
            var rect = (RectTransform)placementButton.transform; rect.SetParent(undoButton.transform.parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,0); rect.pivot = new Vector2(.5f,0);
            rect.anchoredPosition = new Vector2(220,18); rect.sizeDelta = new Vector2(200,52);
            placementButton.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = rect.sizeDelta;
            undoButtonRect.anchoredPosition = new Vector2(-220,18);
            ((RectTransform)replayLoad.transform).anchoredPosition = new Vector2(0,18);
            placementBoxes = new RectTransform[ballcs.Length];
            for (int i = 0; i < ballcs.Length; i++)
            {
                var box = new GameObject("Ball Placement Box " + i, typeof(RectTransform)).GetComponent<RectTransform>();
                box.SetParent(undoButton.transform.parent, false); placementBoxes[i] = box;
                for (int edge = 0; edge < 4; edge++) for (int d = 0; d < 6; d++)
                {
                    var dash = new GameObject("Dash", typeof(RectTransform), typeof(Image)); dash.transform.SetParent(box, false);
                    var dr = (RectTransform)dash.transform;
                    float t = (d + .5f) / 6f;
                    bool horizontal = edge < 2;
                    dr.anchorMin = dr.anchorMax = horizontal ? new Vector2(t,edge) : new Vector2(edge-2,t);
                    dr.sizeDelta = horizontal ? new Vector2(5,2) : new Vector2(2,5);
                    var img = dash.GetComponent<Image>(); img.color = new Color(1,.88f,.3f); img.raycastTarget = false;
                }
                box.gameObject.SetActive(false);

            }
        }
        void UpdateBallPlacement()
        {
            if (!placementButton) CreatePlacementUI();
            placementButton.gameObject.SetActive(IsLocalPractice && !IsReplayBrowser);
            placementButton.interactable = IsBallPlacement || CanPlaceBalls;
            placementButton.GetComponentInChildren<TMP_Text>(true).text = IsBallPlacement ? "공재배치 완료" : "공재배치";
            if (!IsBallPlacement) return;
            var camera = PlacementCamera; if (!camera) return;
            var parent = (RectTransform)undoButton.transform.parent;
            for (int i = 0; i < ballcs.Length; i++)
            {
                var box = placementBoxes[i]; box.gameObject.SetActive(i != draggedBall);
                Vector3 screen = camera.WorldToScreenPoint(ballcs[i].body.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var p);
                box.anchoredPosition = p;
                Vector3 side = camera.WorldToScreenPoint(ballcs[i].body.position + camera.transform.right * PlacementRadius(i));
                float size = (Vector2.Distance(screen,side) * 2 + 16) / parent.GetComponent<Canvas>().scaleFactor;
                box.sizeDelta = new Vector2(size,size);

            }
        }
    }
}

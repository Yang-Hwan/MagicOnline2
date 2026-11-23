using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using PhysicsView = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
public static class ReplaySplitControlsValidation
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    public static void Run()
    {
        var mode = typeof(PracticeSceneFlow).GetField("<SelectedMode>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = mode.GetValue(null);
        var host = new GameObject("Inactive fixture"); host.SetActive(false);
        var canvas = new GameObject("Fixture Canvas", typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600, 900);
        GameObject innings = null;
        try
        {
            mode.SetValue(null, PracticeSceneFlow.Mode.MatchReplay);
            var physics = host.AddComponent<PhysicsView>();
            void Set(string field, object value) => typeof(PhysicsView).GetField(field, Private).SetValue(physics, value);
            T Get<T>(string field) => (T)typeof(PhysicsView).GetField(field, Private).GetValue(physics);
            void Refresh() => typeof(PhysicsView).GetMethod("UpdateMatchReplayControls", Private).Invoke(physics, null);
            var panel = new GameObject("Practice Replay Controls", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(canvas.transform, false); panel.sizeDelta = new Vector2(730,224); Set("replayPanel", panel);
            void EnsureCanvas() => typeof(PhysicsView).GetMethod("EnsureReplayControlCanvas", Private).Invoke(physics, null);
            EnsureCanvas();
            var controlCanvas = panel.GetComponent<Canvas>();
            Check(controlCanvas && controlCanvas.overrideSorting && controlCanvas.sortingOrder == 32001 && panel.GetComponent<GraphicRaycaster>(), "Missing control canvas/raycaster was not created");
            EnsureCanvas();
            Check(panel.GetComponents<Canvas>().Length == 1 && panel.GetComponent<Canvas>() == controlCanvas, "Repeated replay initialization duplicated canvas");
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<GraphicRaycaster>());
            UnityEngine.Object.DestroyImmediate(controlCanvas);
            EnsureCanvas();
            Check(panel.GetComponent<Canvas>() && panel.GetComponent<GraphicRaycaster>(), "Destroyed control canvas was not recreated");
            panel.GetComponent<Canvas>().enabled = false; panel.GetComponent<GraphicRaycaster>().enabled = false;
            EnsureCanvas();
            Check(panel.GetComponent<Canvas>().enabled && panel.GetComponent<GraphicRaycaster>().enabled, "Replay controls remained disabled");
            foreach (string name in new[] { "undoButton", "replayPlay", "replayNext", "replayReset", "replayExit", "replaySlower", "replayFaster" })
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(panel, false);
                var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(go.transform, false);
                Set(name, go.GetComponent<Button>());
            }
            foreach (string name in new[] { "replaySpeedText", "matchReplayProgressLabel", "matchReplayHeading" })
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(panel, false); Set(name, go.GetComponent<TMP_Text>());
            }
            Set("replayPlayText", Get<Button>("replayPlay").GetComponentInChildren<TMP_Text>());
            var info = new GameObject("Replay Info",typeof(RectTransform),typeof(TextMeshProUGUI));info.transform.SetParent(panel,false);Set("replayInfo",info.GetComponent<TMP_Text>());info.GetComponent<TMP_Text>().text="20:00 · 2.4 MB";
            var titleLabel=new GameObject("Title Label",typeof(RectTransform),typeof(TextMeshProUGUI));titleLabel.transform.SetParent(panel,false);Set("matchReplayTitleLabel",titleLabel.GetComponent<TMP_Text>());
            var title=new GameObject("Title",typeof(RectTransform),typeof(TMP_InputField));title.transform.SetParent(panel,false);Set("replayTitle",title.GetComponent<TMP_InputField>());
            var folders=new GameObject("Folders",typeof(RectTransform),typeof(ScrollRect));folders.transform.SetParent(panel,false);Set("replayFolderScroll",folders.GetComponent<ScrollRect>());
            foreach(string field in new[]{"replayFolderAdd","replayFolderDelete"}){var go=new GameObject(field,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(panel,false);var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);label.GetComponent<TMP_Text>().text=field=="replayFolderDelete"?"삭제":"+";Set(field,go.GetComponent<Button>());}
            var slider = new GameObject("Progress", typeof(RectTransform), typeof(Slider)); slider.transform.SetParent(panel, false); Set("replaySeek", slider.GetComponent<Slider>());
            foreach(string part in new[]{"Handle","Fill"}){var go=new GameObject(part,typeof(RectTransform),typeof(Image));go.transform.SetParent(slider.transform,false);((RectTransform)go.transform).anchorMin=new Vector2(0,0);((RectTransform)go.transform).anchorMax=new Vector2(0,1);((RectTransform)go.transform).sizeDelta=new Vector2(22,0);if(part=="Handle")slider.GetComponent<Slider>().handleRect=(RectTransform)go.transform;else slider.GetComponent<Slider>().fillRect=(RectTransform)go.transform;}

            var header = new GameObject("Header", typeof(RectTransform), typeof(Image),typeof(Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.ReplayPanelDrag)); header.transform.SetParent(panel, false); Set("replayDragHeader", (RectTransform)header.transform);
            var headerText=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));headerText.transform.SetParent(header.transform,false);
            var slot = new GameObject("Slot", typeof(RectTransform), typeof(Button)); slot.transform.SetParent(panel, false);
            int selection = 0; slot.GetComponent<Button>().onClick.AddListener(() => selection++);
            Refresh();
            var operations = Get<RectTransform>("matchReplayOperations"); var list = Get<RectTransform>("matchReplayList");
            Check(operations.gameObject.activeSelf && !list.gameObject.activeSelf, "Initial visibility");
            Check(Get<Button>("replayExit").gameObject.activeInHierarchy && Get<Button>("replayExit").interactable, "Exit is unavailable without a loaded replay");
            Check(slider.transform.parent == operations && slot.transform.parent == list, "Widgets were not separated");
            Check(!panel.GetComponent<Image>().enabled, "Full-screen root blocks background");
            Get<Button>("matchReplayListToggle").onClick.Invoke(); slot.GetComponent<Button>().onClick.Invoke();
            Check(list.gameObject.activeSelf && selection == 1 && operations.gameObject.activeSelf, "List toggle or slot binding");
            Get<Button>("matchReplayOperationsToggle").onClick.Invoke(); Refresh();
            Check(!operations.gameObject.activeSelf && list.gameObject.activeSelf, "Visibility resets on refresh");
            var history = new InningScoreHistory(); history.Reset(0); for(int visit=0;visit<14;visit++){history.Activate(visit%2);history.SetTotals(visit,visit/2);}
            innings = new GameObject("Innings", typeof(RectTransform)); ((RectTransform)innings.transform).sizeDelta = new Vector2(1600,900);
            var view = InningScoreView.Install(innings.transform, () => history, i => "Player", () => 0); Set("replayInningView", view); Refresh();
            Check(!view.IsOpen && !view.IconButton.gameObject.activeSelf, "Jump must start closed and use only the new toggle");
            Get<Button>("matchReplayJumpToggle").onClick.Invoke();
            typeof(InningScoreView).GetMethod("Update", Private).Invoke(view, null);
            Check(view.IsOpen && view.Panel.anchoredPosition.y > 200, "Jump panel not over top HUD");
            Get<Button>("matchReplayJumpToggle").onClick.Invoke(); Check(!view.IsOpen, "Jump cannot close");
            Get<Button>("matchReplayListToggle").onClick.Invoke(); Refresh();
            Check(!list.gameObject.activeSelf && !operations.gameObject.activeSelf && Get<Button>("replayExit").gameObject.activeInHierarchy, "All panels closed must keep exit alive");
            var slots=new Button[20];
            for(int i=0;i<20;i++){var go=new GameObject("Slot "+i,typeof(RectTransform),typeof(Image),typeof(Button));var rect=(RectTransform)go.transform;rect.SetParent(list,false);rect.sizeDelta=new Vector2(60,34);rect.anchoredPosition=new Vector2(-300+(i%10)*68,6-(i/10)*42);var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);((RectTransform)label.transform).sizeDelta=rect.sizeDelta;label.GetComponent<TMP_Text>().text=(i+1).ToString("00");label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;slots[i]=go.GetComponent<Button>();}Set("replaySlotButtons",slots);Set("selectedReplaySlot",15);for(int i=0;i<16;i++)Get<bool[]>("replaySlotFilled")[i]=true;
            slot.SetActive(false);Get<Button>("undoButton").gameObject.SetActive(false);
            var titleText=new GameObject("FixtureTitle",typeof(RectTransform),typeof(TextMeshProUGUI));titleText.transform.SetParent(title.transform,false);((RectTransform)titleText.transform).sizeDelta=new Vector2(290,28);titleText.GetComponent<TMP_Text>().text="타이거마스크 vs 왼손잡이";titleText.GetComponent<TMP_Text>().fontSize=20;
            foreach(var t in canvas.GetComponentsInChildren<TMP_Text>(true))t.font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            // Render the actual Unity controls, not a separate design approximation.
            Get<Button>("matchReplayOperationsToggle").onClick.Invoke();Get<Button>("matchReplayListToggle").onClick.Invoke();Get<Button>("matchReplayJumpToggle").onClick.Invoke();
            innings.transform.SetParent(canvas.transform,false);((RectTransform)innings.transform).sizeDelta=new Vector2(1600,900);
            typeof(InningScoreView).GetMethod("Update",Private).Invoke(view,null);view.Panel.GetComponent<CanvasGroup>().alpha=1;
            var previewCamera=new GameObject("Preview camera").AddComponent<Camera>();previewCamera.transform.position=new Vector3(0,0,-100);previewCamera.orthographic=true;previewCamera.orthographicSize=450;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.035f,.055f,.045f);
            var target=new RenderTexture(1600,900,24);previewCamera.targetTexture=target;Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true)){text.ForceMeshUpdate(true,true);text.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();previewCamera.Render();
            var previousTarget=RenderTexture.active;RenderTexture.active=target;var picture=new Texture2D(1600,900,TextureFormat.RGB24,false);picture.ReadPixels(new Rect(0,0,1600,900),0,0);picture.Apply();File.WriteAllBytes("Logs/replay-chrome-preview.png",picture.EncodeToPNG());RenderTexture.active=previousTarget;previewCamera.targetTexture=null;UnityEngine.Object.DestroyImmediate(picture);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(previewCamera.gameObject);
            File.WriteAllText("Logs/replay-split-controls-result.txt", "PASS " + checks + " checks");
        }
        catch (Exception e) { File.WriteAllText("Logs/replay-split-controls-result.txt", e.ToString()); Debug.LogException(e); }
        finally { mode.SetValue(null,previous); if(innings)UnityEngine.Object.DestroyImmediate(innings); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(canvas); }
    }
}

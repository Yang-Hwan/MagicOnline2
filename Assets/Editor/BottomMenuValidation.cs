using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Assets.Scripts.Sight.Surface.Consist;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Prack.BackSys.ChartData;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BottomMenuValidation
{
    static BottomMenuValidation()
    {
        EditorApplication.update += () =>
        {
            const string request = "Logs/bottom-menu-validation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request); Run();
        };
    }
    [MenuItem("Tools/Magic Online/Validate Bottom Menu")]
    public static void Run()
    {
        var preview = default(UnityEngine.SceneManagement.Scene);
        var savedHalls = BackendChart.skillMatchData;
        var wallet = Assets.Scripts.Prack.BackSys.BackendGame.Instance.UserMainData;
        long savedCoin = wallet.Coin;
        int savedJem = wallet.Jem;
        try
        {
            Directory.CreateDirectory("Logs/BottomMenu");
            foreach (string name in new[] { "home", "single", "multiplayer", "ranking", "shop" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Menu/" + name + ".png");
                importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");
            var roots = preview.GetRootGameObjects();
            var legacy = roots.SelectMany(r => r.GetComponentsInChildren<MainSwipe>(true)).Single();
            var view = BottomMenuView.Install(legacy);
            Require(view, "Failed to install menu");
            view.transform.parent.gameObject.SetActive(true);
            Require(!view.transform.parent.Find("PnlNav").gameObject.activeSelf, "Old sidebar still visible");
            Require(!view.transform.parent.Find("MainScrollView").gameObject.activeSelf, "Old vertical scroll still visible");
            var canvas = view.GetComponentInParent<Canvas>();
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
            camera.scene = preview; camera.enabled = false;
            camera.rect = new Rect(0, 0, 1, 1); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var events = roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).First();
            for (int i = 0; i < 5; i++)
            {
                view.DesignRoot.Find("Nav" + i).GetComponent<Button>().onClick.Invoke();
                Require(view.CurrentPage == i, "Navigation index " + i);
                Render(1672, 941, "Logs/BottomMenu/" + i + ".png");
                if (i == 1) Require(view.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Practice")) == 4, "Missing practice routes");
                if (i == 4) Require(view.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Product")).All(b => !b.interactable), "Unconfigured payment enabled");
            }
            // Only synthetic in-memory chart rows; no API writes, account mutations, or matchmaking.
            BackendChart.skillMatchData = new List<SkillMatchData> {
                new SkillMatchData { HallIdx = 901, HallName = "검증 경기장 A", MatchBall = 4, PrizeCoin = 0, MaxCoin = 10000, MatchTotMin = 10 },
                new SkillMatchData { HallIdx = 902, HallName = "검증 경기장 B", MatchBall = 3, MatchCushion = 3, TargetHit = 10, PrizeCoin = 100, MaxCoin = 5000, MatchTotMin = 15, FinishMission = 1 },
                new SkillMatchData { HallIdx = 903, HallName = "검증 경기장 C", MatchBall = 3, MatchCushion = 3, TargetHit = 15, PrizeCoin = 1000, MaxCoin = 20000, MatchTotMin = 20 }
            };
            view.SelectPage(2);
            view.GetComponentsInChildren<Button>().Single(b => b.name == "NextArena").onClick.Invoke();
            Require(view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "검증 경기장 B"), "Arena chart binding");
            Require(BottomMenuView.CanEnter(BackendChart.skillMatchData[1], 100), "Minimum entry inclusive");
            Require(BottomMenuView.CanEnter(BackendChart.skillMatchData[1], 5000), "Maximum entry inclusive");
            Require(!BottomMenuView.CanEnter(BackendChart.skillMatchData[1], 99), "Insufficient balance accepted");
            Require(!BottomMenuView.CanEnter(BackendChart.skillMatchData[1], 5001), "Excess balance accepted");
            Render(1672, 941, "Logs/BottomMenu/2-chart-fixture.png");
            typeof(BottomMenuView).GetMethod("ShowBustNotice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, new object[] { 1L });
            foreach (var graphic in view.DesignRoot.Find("BustSupportNotice").GetComponentsInChildren<Graphic>())
            { graphic.SetAllDirty(); graphic.Rebuild(CanvasUpdate.PreRender); graphic.Rebuild(CanvasUpdate.LatePreRender); }
            Render(1672, 941, "Logs/BottomMenu/bust-notice.png");
            var notice = view.DesignRoot.Find("BustSupportNotice");
            Require(notice.Find("NoticeOpaqueFill").GetComponent<Image>().color.a == 1, "Notice must be opaque");
            Require(notice.Find("NoticeMessage").GetComponent<TMP_Text>().fontStyle == FontStyles.Normal, "Notice text must use normal weight");
            Require(notice.Find("NoticeRemaining").GetComponent<TMP_Text>().text == "사용가능 횟수(1회)", "Notice remaining count");
            UnityEngine.Object.DestroyImmediate(notice.gameObject);
            view.SelectPage(0);
            wallet.Coin = 100000000000L; wallet.Jem = 10000000;
            view.SelectPage(0);
            Render(1672, 941, "Logs/BottomMenu/wallet-max.png");
            foreach (string field in new[] { "Coins", "Gems" })
            {
                var text = view.DesignRoot.Find(field).GetComponent<TMP_Text>();
                text.ForceMeshUpdate();
                Require(text.text == (field == "Coins" ? "100,000,000,000" : "10,000,000"), "Wallet formatting " + field);
                Require(!text.isTextOverflowing && text.textInfo.lineCount == 1, "Wallet number clipped " + field);
            }
            Render(1024, 768, "Logs/BottomMenu/0-4x3.png");
            Render(2560, 1080, "Logs/BottomMenu/0-ultrawide.png");
            wallet.Coin = savedCoin; wallet.Jem = savedJem;
            // Repeated switching must leave one active set of content and one root.
            for (int i = 0; i < 20; i++) view.SelectPage(i % 5);
            Require(view.transform.parent.GetComponentsInChildren<BottomMenuView>(true).Length == 1, "Duplicate menu root");
            File.WriteAllText("Logs/BottomMenu/result.json", "{\"passed\":true,\"pages\":5,\"renders\":10,\"bustNotice\":true,\"walletMaxDigits\":true,\"navigation\":true,\"arenaBounds\":true,\"unconfiguredPaymentsDisabled\":true}");
            Debug.Log("Bottom menu validation passed.");
            if (Application.isBatchMode) EditorApplication.Exit(0);

            void Render(int width, int height, string path)
            {
                var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases(); view.Resize(); Canvas.ForceUpdateCanvases(); camera.Render();
                // Raycast through the rendered geometry, not merely Invoke: decoration must not swallow tabs.
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                for (int tab = 0; tab < 5; tab++)
                {
                    var button = view.DesignRoot.Find("Nav" + tab).GetComponent<Button>();
                    var pos = RectTransformUtility.WorldToScreenPoint(camera, button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                    var hits = new List<RaycastResult>();
                    raycaster.Raycast(new PointerEventData(events) { position = pos }, hits);
                    Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Tab hit target at " + width + "x" + height + ": " + tab);
                }
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs/BottomMenu"); File.WriteAllText("Logs/BottomMenu/error.txt", e.ToString()); Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        finally
        {
            BackendChart.skillMatchData = savedHalls;
            wallet.Coin = savedCoin; wallet.Jem = savedJem;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

using System;
using System.IO;
using System.Reflection;
using System.Linq;
using Assets.Scripts.Sight.Surface.Come;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StarryLoginValidation
{
    static StarryLoginValidation()
    {
        EditorApplication.update += () =>
        {
            const string request = "Logs/login-starry-validation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request); Run();
        };
    }
    public static void Run()
    {
        UnityEngine.SceneManagement.Scene preview = default;
        try
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Login/StarryNightBackplate.png");
            importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Come.unity");
            var roots = preview.GetRootGameObjects();
            var panel = roots.SelectMany(r => r.GetComponentsInChildren<PnlLogin>(true)).Single();
            foreach (Transform sibling in panel.transform.parent) sibling.gameObject.SetActive(sibling == panel.transform);
            var id = panel.transform.Find("InpId").GetComponent<TMP_InputField>();
            var pw = panel.transform.Find("InpPw").GetComponent<TMP_InputField>();
            var login = panel.transform.Find("BtnLogin").GetComponent<Button>();
            var register = panel.transform.Find("BtnEntry").GetComponent<Button>();
            var msg = roots.SelectMany(r => r.GetComponentsInChildren<TextMeshProUGUI>(true)).Single(t => t.name == "TxtRetMsg");
            Require(StarryLoginView.Apply(panel, id, pw, login, register, msg), "View apply failed");
            panel.transform.Find("BtnGuest").gameObject.SetActive(false);
            panel.transform.Find("BtnHashKey").gameObject.SetActive(false);
            void Set(Type type, string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, value);
            Set(typeof(PnlLogin), "inpId", id); Set(typeof(PnlLogin), "inpPw", pw);
            Set(typeof(PnlLogin), "imgId", id.GetComponent<Image>()); Set(typeof(PnlLogin), "imgPw", pw.GetComponent<Image>());
            Set(typeof(LoginBase), "txtMsg", msg);
            Require(panel.transform.Find("StarryNightBackplate"), "Backplate missing");
            Require(panel.transform.Find("BtnLoginTemp").gameObject.activeSelf, "Test login 1 hidden");
            Require(panel.transform.Find("BtnLoginTemp2").gameObject.activeSelf, "Test login 2 hidden");
            Require(panel.transform.Find("BtnLoginTemp").GetComponentInChildren<TMP_Text>().text.Contains("user01"), "Test login 1 label");
            Require(panel.transform.Find("BtnLoginTemp2").GetComponentInChildren<TMP_Text>().text.Contains("user02"), "Test login 2 label");
            Require(pw.contentType == TMP_InputField.ContentType.Password, "Password is not masked");
            var authenticate = typeof(PnlLogin).GetMethod("Authenticate", BindingFlags.Instance | BindingFlags.NonPublic);
            id.text = ""; pw.text = ""; authenticate.Invoke(panel, new object[]{false});
            Require(msg.text.Contains("아이디"), "Empty login validation failed");
            id.text = "preview_user"; authenticate.Invoke(panel, new object[]{true});
            Require(msg.text.Contains("비밀번호"), "Empty registration validation failed");
            id.text = ""; pw.text = ""; msg.text = "";
            id.GetComponent<Image>().color = Color.clear; pw.GetComponent<Image>().color = Color.clear;
            var canvas = panel.GetComponentInParent<Canvas>();
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
            camera.scene = preview;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.rect = new Rect(0, 0, 1, 1); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            Render(1672, 941, "Logs/login-starry-actual.png");
            Render(1024, 768, "Logs/login-starry-4x3.png");
            File.WriteAllText("Logs/login-starry-validation.json", "{\"passed\":true,\"emptyLogin\":true,\"emptyRegistration\":true,\"passwordMasked\":true,\"renders\":2}");
            if (Application.isBatchMode) EditorApplication.Exit(0);

            void Render(int width, int height, string path)
            {
                var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                typeof(StarryLoginView).GetMethod("Resize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel.GetComponent<StarryLoginView>(), null);
                Canvas.ForceUpdateCanvases(); camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e); File.WriteAllText("Logs/login-starry-validation-error.txt", e.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}

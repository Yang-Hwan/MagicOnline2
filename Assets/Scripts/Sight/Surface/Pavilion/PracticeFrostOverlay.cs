using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Capture the authored sprite masks and snow together, above all gameplay HUD.
    public sealed class PracticeFrostOverlay : MonoBehaviour
    {
        const int EffectLayer = 31;
        Camera capture;
        RenderTexture texture;
        RawImage image;
        Renderer[] renderers;
        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var item in GetComponentsInChildren<Transform>(true)) item.gameObject.layer = EffectLayer;
            var view = new GameObject("Frost above HUD", typeof(RectTransform), typeof(Canvas));
            view.transform.SetParent(transform, false);
            var canvas = view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            var display = new GameObject("Frost", typeof(RectTransform), typeof(RawImage));
            display.transform.SetParent(view.transform, false);
            image = display.GetComponent<RawImage>();
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var cameraObject = new GameObject("Frost capture", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            capture = cameraObject.GetComponent<Camera>(); capture.enabled = false;
            foreach (var renderer in renderers) renderer.forceRenderingOff = true;
        }
        void LateUpdate()
        {
            var source = Camera.main;
            if (!source || !capture) return;
            int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            if (!texture || texture.width != width || texture.height != height) {
                if (texture) { texture.Release(); Destroy(texture); }
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                texture.Create(); image.texture = texture;
            }
            capture.CopyFrom(source); capture.enabled = false;
            capture.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            capture.targetTexture = texture; capture.rect = new Rect(0, 0, 1, 1);
            capture.cullingMask = 1 << EffectLayer;
            capture.clearFlags = CameraClearFlags.SolidColor; capture.backgroundColor = Color.clear;
            capture.allowHDR = false; capture.allowMSAA = false;
            try {
                foreach (var renderer in renderers) if (renderer) renderer.forceRenderingOff = false;
                if (GraphicsSettings.currentRenderPipeline) {
                    var data = capture.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = false;
                    data.renderShadows = false;
                    data.requiresColorOption = CameraOverrideOption.Off;
                    data.requiresDepthOption = CameraOverrideOption.Off;
                    RenderPipeline.SubmitRenderRequest(capture,
                        new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                } else capture.Render();
            } finally {
                foreach (var renderer in renderers) if (renderer) renderer.forceRenderingOff = true;
            }
        }
        void OnDisable() { if (texture) { var previous = RenderTexture.active; RenderTexture.active = texture; GL.Clear(true, true, Color.clear); RenderTexture.active = previous; } }
        void OnDestroy() { if (texture) { texture.Release(); Destroy(texture); } }
    }
}

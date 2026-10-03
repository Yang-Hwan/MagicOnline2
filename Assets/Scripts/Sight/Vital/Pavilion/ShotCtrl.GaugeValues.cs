using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Sight.Surface.Pavilion;
namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public partial class ShotCtrl
    {
        RectTransform gaugeValueCanvas;
        TextMeshProUGUI powerValueLabel, followValueLabel;
        int displayedPower = -1, displayedFollow = -1;
        void CreateGaugeValueLabels()
        {
            var root=new GameObject("Gauge Values",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);
            gaugeValueCanvas=root.GetComponent<RectTransform>();
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var panel=FindAnyObjectByType<PnlMatch>();
            TMP_FontAsset font=panel ? panel.transform.Find("PlayerBoxs/PlayerSelf/TxtName").GetComponent<TMP_Text>().font : TMP_Settings.defaultFontAsset;
            powerValueLabel=CreateGaugeValueLabel("Power",font,new Color(.65f,.92f,1));
            followValueLabel=CreateGaugeValueLabel("Follow",font,new Color(1,.87f,.52f));
            UpdateGaugeValueLabels();
        }

        TextMeshProUGUI CreateGaugeValueLabel(string name,TMP_FontAsset font,Color color)
        {
            var box=new GameObject(name+" Value",typeof(RectTransform),typeof(Image));
            box.transform.SetParent(gaugeValueCanvas,false);
            var rect=box.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(110,30);
            rect.pivot=new Vector2(.5f,0);
            var image=box.GetComponent<Image>();image.color=new Color(.03f,.05f,.06f,.88f);image.raycastTarget=false;
            var textObject=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
            textObject.transform.SetParent(box.transform,false);
            var text=textObject.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=19;
            text.alignment=TextAlignmentOptions.Center;text.color=color;text.raycastTarget=false;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=Vector2.zero;text.rectTransform.offsetMax=Vector2.zero;
            return text;
        }

        void UpdateGaugeValueLabels()
        {
            if(!gaugeValueCanvas) return;
            int power=Mathf.RoundToInt(Mathf.Clamp01(force)*100),follow=Mathf.RoundToInt(Mathf.Clamp01(pull)*100);
            if(power!=displayedPower) { powerValueLabel.text="파워 "+(power*.01f).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);displayedPower=power; }
            if(follow!=displayedFollow) { followValueLabel.text="팔로 "+(follow*.01f).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);displayedFollow=follow; }
            var camera=InputOutput.usedCamera ? InputOutput.usedCamera : Camera.main;
            PositionGaugeValue(powerValueLabel,forceHandle,0.1f,camera);
            PositionGaugeValue(followValueLabel,pullBarHandle,pullSliderMaxYPos+.1f,camera);
        }

        void PositionGaugeValue(TextMeshProUGUI label,Transform handle,float top,Camera camera)
        {
            bool visible=handle && handle.parent && handle.gameObject.activeInHierarchy && camera;
            var box=(RectTransform)label.transform.parent;
            if(!visible) { box.gameObject.SetActive(false);return; }
            Vector3 screen=camera.WorldToScreenPoint(handle.parent.TransformPoint(new Vector3(0,top,0)));
            box.gameObject.SetActive(screen.z>0);
            if(screen.z<=0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gaugeValueCanvas,screen,null,out var local);
            box.anchoredPosition=local;
        }
    }
}

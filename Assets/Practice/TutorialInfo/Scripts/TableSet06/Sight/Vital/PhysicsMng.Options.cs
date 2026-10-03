using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        public bool ItemsEnabled { get; private set; } = true;
        public bool CueGuideEnabled { get; private set; } = true;
        RectTransform optionsRect;
        Toggle itemsToggle, cueGuideToggle;

        void CreatePracticeOptions()
        {
            var panel=new GameObject("Game Options",typeof(RectTransform));
            optionsRect=panel.GetComponent<RectTransform>();optionsRect.SetParent(undoButtonRect.parent,false);
            optionsRect.anchorMin=optionsRect.anchorMax=new Vector2(.5f,1);optionsRect.pivot=new Vector2(.5f,1);
            optionsRect.anchoredPosition=new Vector2(0,-145);optionsRect.sizeDelta=new Vector2(330,36);
            var match=FindAnyObjectByType<PnlMatch>();
            var font=match ? match.transform.Find("PlayerBoxs/PlayerSelf/TxtName").GetComponent<TMP_Text>().font : TMP_Settings.defaultFontAsset;
            itemsToggle=CreateOptionToggle("아이템",-80,font);
            cueGuideToggle=CreateOptionToggle("수구라인",80,font);
            itemsToggle.onValueChanged.AddListener(SetItemsEnabled);
            cueGuideToggle.onValueChanged.AddListener(SetCueGuideEnabled);
        }

        Toggle CreateOptionToggle(string label,float x,TMP_FontAsset font)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Toggle));
            go.transform.SetParent(optionsRect,false);var rect=go.GetComponent<RectTransform>();
            rect.sizeDelta=new Vector2(150,36);rect.anchoredPosition=new Vector2(x,0);
            var background=go.GetComponent<Image>();background.color=new Color(.025f,.05f,.05f,.85f);
            var square=new GameObject("Checkbox",typeof(RectTransform),typeof(Image));square.transform.SetParent(rect,false);
            var sr=square.GetComponent<RectTransform>();sr.sizeDelta=new Vector2(24,24);sr.anchoredPosition=new Vector2(-53,0);
            square.GetComponent<Image>().color=new Color(.6f,.7f,.7f);square.GetComponent<Image>().raycastTarget=false;
            var check=new GameObject("Checked",typeof(RectTransform),typeof(Image));check.transform.SetParent(sr,false);
            var cr=check.GetComponent<RectTransform>();cr.sizeDelta=new Vector2(18,18);
            var mark=check.GetComponent<Image>();mark.color=new Color(.15f,.8f,.5f);mark.raycastTarget=false;
            var textGo=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(rect,false);
            var text=textGo.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=22;text.text=label;
            text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
            text.rectTransform.sizeDelta=new Vector2(107,36);text.rectTransform.anchoredPosition=new Vector2(14,0);
            var toggle=go.GetComponent<Toggle>();toggle.targetGraphic=background;toggle.graphic=mark;toggle.isOn=true;
            return toggle;
        }

        public void SetItemsEnabled(bool enabled)
        {
            // Network item rules belong to the match authority/transport, not a local UI override.
            if(!IsLocalPractice) { itemsToggle?.SetIsOnWithoutNotify(ItemsEnabled);return; }
            ItemsEnabled=enabled;itemsToggle?.SetIsOnWithoutNotify(enabled);
            if(enabled) { if(Items!=null) RestoreItemViews();return; }
            itemSessionVersion++;
            if(HasItemAuthority) foreach(var item in Items.Capture().items) Items.Expire(item.id);
            if(cushionHit) foreach(var particles in cushionHit.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(ObjectPooler.instance) {
                foreach(var effect in ObjectPooler.instance.GetActivePools<EffCtrl>("Effect")) effect.DeactiveDelay();
                foreach(var visual in ObjectPooler.instance.GetActivePools("Cosmos")) visual.SetActive(false);
            }
        }

        public void SetCueGuideEnabled(bool enabled)
        {
            if (!IsLocalPractice) { cueGuideToggle?.SetIsOnWithoutNotify(CueGuideEnabled); return; }
            CueGuideEnabled=enabled;cueGuideToggle?.SetIsOnWithoutNotify(enabled);
            shotController.SetPlacementGuidesHidden(!enabled || IsBallPlacement || IsPracticeReplay);
        }

        void UpdatePracticeOptions()
        {
            if(!itemsToggle) return;
            itemsToggle.interactable=IsLocalPractice;
            itemsToggle.SetIsOnWithoutNotify(ItemsEnabled);
        }
    }
}

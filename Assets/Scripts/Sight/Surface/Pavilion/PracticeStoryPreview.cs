using System.Linq;
using DG.Tweening;
using System.Collections.Generic;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Vital.Pavilion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Local-only animation browser. Uses scene-authored actors, never sends match events.
    public sealed class PracticeStoryPreview : MonoBehaviour
    {
        Transform storyRoot;
        MovStoryAni[] stories;
        MovStoryAni activeStory;
        TMP_Dropdown selection;
        GameObject controls;
        PracticeSentimentController sentiment;
        bool legacy;
        readonly List<MovStoryAni> legacyChoices = new List<MovStoryAni>();
        static readonly string[] Categories = { "", "분노", "격려", "실망", "불만", "좌절", "기쁨", "감사", "안도" };
        void Start()
        {
            var config=PracticeSceneFlow.Configuration;
            if(config.Execution!=MatchExecutionMode.LocalPractice || (config.BallCount!=3 && config.BallCount!=4)) { enabled=false; return; }
            var table=GameObject.Find("Table");
            storyRoot=table ? table.transform.Find("Addition/MovStorys") : null;
            if(!storyRoot) { Debug.LogError("Practice story actors are missing from the scene."); return; }
            stories=storyRoot.GetComponentsInChildren<MovStoryAni>(true)
                .OrderBy(x=>x.name=="Jogmman"?0:x.name=="Dabdab"?1:x.name=="Ggeung"?2:3).ThenBy(x=>x.name).ToArray();
            foreach(Transform child in storyRoot) child.gameObject.SetActive(false);
            var sentimentRoot=table.transform.Find("Addition/Sentiment");
            if(!sentimentRoot) { Debug.LogError("Practice Sentiment actors are missing from the scene."); return; }
            sentiment=gameObject.AddComponent<PracticeSentimentController>();
            sentiment.Initialize(sentimentRoot);
            sentiment.Emotion+=OnEmotion;
            sentiment.ShotStarted+=StopStory;
            var root=new GameObject("StoryPreviewControls",typeof(RectTransform),typeof(Image));
            controls=root;root.transform.SetParent(transform,false);
            var rect=root.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=new Vector2(0,65);rect.sizeDelta=new Vector2(660,48);
            root.GetComponent<Image>().color=new Color(.025f,.035f,.05f,.95f);
            var dropdown=TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            dropdown.name="StorySelection";dropdown.transform.SetParent(root.transform,false);
            SetRect((RectTransform)dropdown.transform,new Vector2(150,4),new Vector2(350,40));
            selection=dropdown.GetComponent<TMP_Dropdown>();
            var font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            foreach(var text in dropdown.GetComponentsInChildren<TMP_Text>(true)) {
                text.font=font;text.fontSize=22;text.color=Color.black;text.raycastTarget=false;
                text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=22;
            }
            selection.ClearOptions();
            RefreshOptions();
            selection.template.anchorMin=new Vector2(0,1);selection.template.anchorMax=new Vector2(1,1);
            selection.template.pivot=new Vector2(.5f,0);selection.template.anchoredPosition=new Vector2(0,4);
            selection.template.sizeDelta=new Vector2(0,260);
            var buttonObject=new GameObject("PlayStory",typeof(RectTransform),typeof(Image),typeof(Button));
            buttonObject.transform.SetParent(root.transform,false);
            SetRect((RectTransform)buttonObject.transform,new Vector2(508,4),new Vector2(144,40));
            buttonObject.GetComponent<Image>().color=new Color(1,.86f,.12f);
            var button=buttonObject.GetComponent<Button>();button.onClick.AddListener(PlaySelected);
            button.interactable=stories.Length>0;
            var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(buttonObject.transform,false);
            var textRect=(RectTransform)label.transform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=textRect.offsetMax=Vector2.zero;
            var caption=label.GetComponent<TMP_Text>();caption.font=font;caption.text="연출재생";caption.fontSize=23;caption.color=Color.black;
            caption.alignment=TextAlignmentOptions.Center;caption.raycastTarget=false;
            var toggleObject=DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleObject.name="LegacySentimentToggle";toggleObject.transform.SetParent(root.transform,false);
            SetRect((RectTransform)toggleObject.transform,new Vector2(10,4),new Vector2(136,40));
            var oldLabel=toggleObject.GetComponentInChildren<Text>();
            oldLabel.enabled=false;
            var toggleLabel=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI));
            toggleLabel.transform.SetParent(toggleObject.transform,false);
            SetRect((RectTransform)toggleLabel.transform,new Vector2(28,0),new Vector2(106,40));
            var toggleText=toggleLabel.GetComponent<TMP_Text>();toggleText.font=font;toggleText.text="구감정";
            toggleText.fontSize=22;toggleText.alignment=TextAlignmentOptions.MidlineLeft;toggleText.raycastTarget=false;
            var toggle=toggleObject.GetComponent<Toggle>();toggle.SetIsOnWithoutNotify(false);
            // DefaultControls has no built-in check sprite in this project: use a solid cyan check square.
            toggle.graphic.color=Color.cyan;
            toggle.onValueChanged.AddListener(value=> {
                StopStory();legacy=value;sentiment.SetLegacy(value);RefreshOptions();
            });
        }
        void RefreshOptions()
        {
            selection.ClearOptions();
            selection.AddOptions(legacy ? stories.Select(x=>x.name=="Dabdab"?"DabDab":x.name).ToList() :
                sentiment.Actors.Select(x=>$"{x.name} · {Categories[x.category]} · {x.phrase}").ToList());
            selection.SetValueWithoutNotify(0);selection.RefreshShownValue();
        }
        void OnEmotion(int category,Vector3 position)
        {
            StopStory();
            if(!legacy)return;
            legacyChoices.Clear();
            foreach(var story in stories) {
                string n=story.name.ToLowerInvariant();
                bool match=category==1 ? n=="foul" : category==2 ? n=="jogmman" :
                    category==3 ? n=="dabdab"||n=="ggeung"||n=="tearcry" :
                    category==4 ? n=="ehyu"||n=="anumuhe"||n=="buglbugl"||n=="akissuu" :
                    category==5 ? n=="glassbroken"||n=="crowfly"||n=="darkcloud"||n=="deadleaves"||n=="dogbone"||n=="snowstorm"||n=="bombdebris"||n=="ponghammer" :
                    category==7 ? n=="hehe"||n=="kissgamsa"||n=="kissgamsa2" :
                    n=="heartniceshot"||n=="jjagjjag"||n=="lightemit"||n=="spotlight"||n=="musicnote"||n=="bellylaugh";
                if(match)legacyChoices.Add(story);
            }
            if(legacyChoices.Count>0) {
                PlayOld(legacyChoices[UnityEngine.Random.Range(0,legacyChoices.Count)],position);
                sentiment.LogDisplayed(category,$"구감정 · {activeStory.name}");
            }
        }
        static void SetRect(RectTransform rect,Vector2 position,Vector2 size) {
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;
        }
        public void PlaySelected()
        {
            if(!selection || !sentiment) return;
            StopStory();
            sentiment.Hide();
            if(!legacy) {
                if(sentiment.Actors.Length>0)sentiment.Play(sentiment.Actors[selection.value],sentiment.PreviewPosition,true);
                return;
            }
            if(stories.Length>0) {
                PlayOld(stories[selection.value],new Vector3(0,.05f,0));
                sentiment.LogDisplayed(0,activeStory.name,true);
            }
        }
        void PlayOld(MovStoryAni story,Vector3 position)
        {
            activeStory=story;
            storyRoot.gameObject.SetActive(true);
            activeStory.gameObject.SetActive(true); // Awake initializes the original scene animation.
            activeStory.StartSequence(activeStory.storyAniPos==StoryAniPos.Full ? Vector3.zero : position);
        }
        void StopStory()
        {
            if(!activeStory)return;
            activeStory.ReleaseSequence();
            foreach(var particle in activeStory.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            activeStory.gameObject.SetActive(false);activeStory=null;
        }
        void OnDisable() {
            if(selection)selection.Hide();
            StopStory();if(storyRoot)storyRoot.gameObject.SetActive(false);
        }
        void OnDestroy() { if(sentiment) { sentiment.Emotion-=OnEmotion;sentiment.ShotStarted-=StopStory; } StopStory(); }
    }
}

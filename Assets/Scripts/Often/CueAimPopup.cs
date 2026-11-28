using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;

namespace Assets.Scripts.Often
{
    // One presentation/input implementation shared by practice and online play.
    public sealed class CueAimPopup : MonoBehaviour
    {
        RectTransform ball, anglePanel, root;
        TMP_Text pointTab, angleTab, degrees;
        Image pointUnderline, angleUnderline;
        Slider slider;
        CueAngleLines lines;
        Action<Vector2> setPoint;
        Action<float> setAngle;
        Action close;
        Func<float> readAngle;
        bool angleMode;
        public void Configure(RectTransform frame, Action<Vector2> point, Func<float> read, Action<float> angle, Action dismiss)
        {
            root=(RectTransform)transform;ball=frame;setPoint=point;readAngle=read;setAngle=angle;close=dismiss;
            root.localRotation=Quaternion.identity;root.anchorMin=root.anchorMax=root.pivot=Vector2.one*.5f;root.anchoredPosition=Vector2.zero;root.sizeDelta=new Vector2(1000,1000);
            var background=root.GetComponent<Graphic>();if(background)background.color=Color.clear;
            ball.anchoredPosition=Vector2.zero;
            ball.localScale=Vector3.one*(680f/Mathf.Max(1,ball.rect.width));
            var legacyCentre=ball.Find("PointM");if(legacyCentre)legacyCentre.gameObject.SetActive(false);
            foreach(Transform child in root)if(child!=ball)child.gameObject.SetActive(false);
            var hit=ball.GetComponent<Image>();if(!hit)hit=ball.gameObject.AddComponent<Image>();
            hit.enabled=true;hit.color=Color.clear;hit.raycastTarget=true;
            var input=ball.gameObject.AddComponent<CueAimGesture>();input.Move=e=>{
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(ball,e.position,e.pressEventCamera,out var p))setPoint(p);
            };input.Accept=e=>{GameAudio.Play(SoundCue.Aim,.4f);close();};
            input.Valid=e=>RectTransformUtility.ScreenPointToLocalPointInRectangle(ball,e.position,e.pressEventCamera,out var p)&&p.magnitude<=ball.rect.width*.5f;
            foreach(var g in ball.GetComponentsInChildren<Graphic>())if(g!=hit)g.raycastTarget=false;
            Bar(root,"Tab box border",0,440,606,88,new Color(.45f,.52f,.53f,1));
            Bar(root,"Tab box background",0,440,602,84,Color.black);
            pointTab=Tab("당점",-150,()=>Select(false));angleTab=Tab("큐각도",150,()=>Select(true));
            Bar(root,"Tab divider",0,440,2,58,new Color(.45f,.52f,.53f,1));
            pointUnderline=Bar(root,"Contact underline",-150,398,130,3,new Color(.3f,1,.75f));
            angleUnderline=Bar(root,"Angle underline",150,398,155,3,new Color(.3f,1,.75f));
            anglePanel=Rect(root,"Angle content",0,0,900,750);
            var view=Rect(anglePanel,"Cue side view",0,0,850,600);
            lines=view.gameObject.AddComponent<CueAngleLines>();lines.raycastTarget=false;
            degrees=Label(anglePanel,"Degrees","0°",270,-135,150,70,44,Color.white);
            var track=Rect(anglePanel,"Angle slider",0,-340,660,112);
            var touch=track.gameObject.AddComponent<Image>();touch.color=Color.clear;
            Bar(track,"Track",0,0,660,3,new Color(.6f,.65f,.65f));
            var fillArea=Rect(track,"Fill area",0,0,660,3);
            var fill=Bar(fillArea,"Fill",0,0,660,3,new Color(.3f,1,.75f));
            // Slider controls the horizontal anchors. Extra sizeDelta would extend
            // the fill past both endpoints (and leave a visible fill even at zero).
            fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;
            fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
            // Slider stretches the handle vertically, so a zero-height rail keeps
            // its square size instead of adding the rail height to the thumb.
            var handleArea=Rect(track,"Handle area",0,0,660,0);
            var handleRect=Rect(handleArea,"Handle",0,0,81.6f,81.6f);
            var handle=handleRect.gameObject.AddComponent<CueAngleThumb>();handle.color=Color.white;handle.raycastTarget=true;
            slider=track.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=80;
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            slider.onValueChanged.AddListener(v=>{setAngle(v);ShowAngle(v);});
            var release=track.gameObject.AddComponent<CueAimGesture>();release.Accept=e=>{GameAudio.Play(SoundCue.Aim,.4f);close();};
            Label(anglePanel,"Minimum","0°",-390,-340,100,55,30,Color.white);
            Label(anglePanel,"Maximum","80°",390,-340,100,55,30,Color.white);
            Refresh();
        }
        public void Refresh(){if(!slider)return;float v=readAngle();slider.SetValueWithoutNotify(v);ShowAngle(v);Select(angleMode);Fit();}
        void ShowAngle(float value){lines.Angle=value;lines.SetVerticesDirty();degrees.text=Mathf.RoundToInt(value)+"°";}
        void Select(bool angle){angleMode=angle;ball.gameObject.SetActive(!angle);anglePanel.gameObject.SetActive(angle);pointTab.color=angle?new Color(.72f,.76f,.77f):Color.white;angleTab.color=angle?Color.white:new Color(.72f,.76f,.77f);pointTab.transform.parent.GetComponent<Image>().color=angle?TabIdle:TabActive;angleTab.transform.parent.GetComponent<Image>().color=angle?TabActive:TabIdle;pointUnderline.enabled=!angle;angleUnderline.enabled=angle;}
        static readonly Color TabIdle=new Color(.005f,.008f,.01f,1),TabActive=new Color(.01f,.06f,.045f,1);
        void LateUpdate(){Fit();}
        void Fit(){if(!root)return;var canvas=GetComponentInParent<Canvas>();if(!canvas)return;var area=((RectTransform)canvas.transform).rect.size;float s=Mathf.Min(1,Mathf.Min(area.x/1050,area.y/1030));root.localScale=Vector3.one*s;}
        TMP_Text Tab(string text,float x,Action action){var rect=Rect(root,text+" tab",x,440,298,80);var hit=rect.gameObject.AddComponent<Image>();hit.color=TabIdle;var button=rect.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>action());return Label(rect,"Label",text,0,0,260,70,42,Color.white);}
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.gameObject.layer=parent.gameObject.layer;r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
        static Image Bar(Transform parent,string name,float x,float y,float w,float h,Color c){var r=Rect(parent,name,x,y,w,h);var g=r.gameObject.AddComponent<Image>();g.color=c;g.raycastTarget=false;return g;}
        static TMP_Text Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color){var r=Rect(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CueAngleThumb : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();float radius=Mathf.Min(rect.width,rect.height)*.5f;
            mesh.AddVert(rect.center,color,Vector2.zero);
            const int segments=64;
            for(int i=0;i<=segments;i++){
                float a=i*Mathf.PI*2/segments;
                mesh.AddVert(rect.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);
                if(i>0)mesh.AddTriangle(0,i,i+1);
            }
        }
    }
    public sealed class CueAimGesture : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public Action<PointerEventData> Move,Accept;
        public Func<PointerEventData,bool> Valid;
        bool pressed;
        public void OnPointerDown(PointerEventData e){pressed=e.button==PointerEventData.InputButton.Left&&(Valid==null||Valid(e));if(pressed)Move?.Invoke(e);}
        public void OnDrag(PointerEventData e){if(pressed)Move?.Invoke(e);}
        public void OnPointerUp(PointerEventData e){if(!pressed)return;pressed=false;Move?.Invoke(e);Accept?.Invoke(e);}
        void OnDisable(){pressed=false;}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CueAngleLines : MaskableGraphic
    {
        public float Angle;
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();var centre=new Vector2(-130,38);float radius=288;
            CuePointGuide.ShadedBall(vh,centre,radius);
            CuePointGuide.Line(vh,new Vector2(-430,-250),new Vector2(400,-250),3,new Color(.8f,.83f,.83f));
            float a=Angle*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var n=new Vector2(-dir.y,dir.x)*16;
            var tip=centre+dir*radius;
            // Keep steep cue angles below the tabs as the enlarged ball raises the tip.
            float cueLength=dir.y>.001f?Mathf.Min(330,(360-tip.y)/dir.y):330;
            if(dir.x>.001f)cueLength=Mathf.Min(cueLength,(400-tip.x)/dir.x);
            var end=tip+dir*Mathf.Max(0,cueLength);
            CuePointGuide.Line(vh,tip+n,end+n,3.2f,Color.white);CuePointGuide.Line(vh,tip-n,end-n,3.2f,Color.white);
            CuePointGuide.Line(vh,tip,tip+dir*16,32,new Color(.15f,.65f,1));
            for(int i=0;i<32;i++){float p=a*i/32,q=a*(i+1)/32;CuePointGuide.Line(vh,centre+new Vector2(Mathf.Cos(p),Mathf.Sin(p))*313,centre+new Vector2(Mathf.Cos(q),Mathf.Sin(q))*313,2,new Color(1,1,1,.7f));}
        }
    }
}

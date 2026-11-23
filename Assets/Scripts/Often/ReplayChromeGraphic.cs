using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Assets.Scripts.Often
{
    // Resolution-independent chrome: corner radii and edge widths are never stretched.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReplayChromeGraphic : MaskableGraphic
    {
        public enum Finish { Charcoal, Emerald, Amber }
        Finish finish;
        bool panel;
        public void SetStyle(Finish value, bool isPanel = false)
        { if(finish==value && panel==isPanel)return;finish=value;panel=isPanel;SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(panel?15:9,r.height*.3f);
            Layer(vh,new Rect(r.x+1,r.y-3,r.width,r.height),radius,new Color(0,0,0,.32f),new Color(0,0,0,.32f));
            var top=finish==Finish.Emerald?new Color(.66f,.85f,.65f):finish==Finish.Amber?new Color(1,.85f,.43f):new Color(.76f,.72f,.59f);
            var bottom=finish==Finish.Emerald?new Color(.19f,.43f,.29f):new Color(.36f,.29f,.17f);
            Layer(vh,r,radius,bottom,top);
            r=Inset(r,1.3f);Layer(vh,r,radius-1,new Color(.025f,.028f,.023f,.98f),new Color(.11f,.12f,.1f,.98f));
            r=Inset(r,1.3f);
            Color fillTop=finish==Finish.Emerald?new Color(.14f,.32f,.19f):finish==Finish.Amber?new Color(.50f,.29f,.06f):new Color(.15f,.15f,.13f);
            Color fillBottom=finish==Finish.Emerald?new Color(.025f,.10f,.055f):finish==Finish.Amber?new Color(.20f,.095f,.015f):new Color(.035f,.041f,.039f);
            Layer(vh,r,radius-2,fillBottom,fillTop);
            // Narrow highlight at the upper edge, with a quiet sheen across the face.
            var sheen=new Rect(r.x+radius*.5f,r.yMax-2,r.width-radius,1);
            Layer(vh,sheen,.5f,new Color(1,1,.9f,.08f),new Color(1,1,.9f,.22f));
        }
        static Rect Inset(Rect r,float p)=>new Rect(r.x+p,r.y+p,Mathf.Max(0,r.width-p*2),Mathf.Max(0,r.height-p*2));
        void Layer(VertexHelper vh,Rect r,float radius,Color bottom,Color top)
        {
            if(r.width<=0||r.height<=0)return;radius=Mathf.Clamp(radius,0,Mathf.Min(r.width,r.height)*.5f);
            int start=vh.currentVertCount;Add(r.center,Color.Lerp(bottom,top,.5f));
            for(int corner=0;corner<4;corner++)for(int step=0;step<=8;step++){
                float a=(corner*90+step*90f/8)*Mathf.Deg2Rad;
                var p=new Vector2((corner==0||corner==3?r.xMax-radius:r.xMin+radius)+Mathf.Cos(a)*radius,(corner<2?r.yMax-radius:r.yMin+radius)+Mathf.Sin(a)*radius);
                Add(p,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,p.y)));
            }
            for(int i=0;i<36;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%36);
            void Add(Vector2 p,Color c){c*=color;vh.AddVert(p,c,Vector2.zero);}
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReplayControlIcon : MaskableGraphic
    {
        public int Kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var ink=new Color(.96f,.94f,.84f);
            if(Kind==0){vh.AddVert(new Vector2(-6,-9),ink,Vector2.zero);vh.AddVert(new Vector2(-6,9),ink,Vector2.zero);vh.AddVert(new Vector2(9,0),ink,Vector2.zero);vh.AddTriangle(0,1,2);}
            else if(Kind==1){for(int i=0;i<3;i++){float y=7-i*7;Box(-9,y-1.5f,3,3);Box(-3,y-1,13,2);}}
            else{Box(-9,-7,3,11);Box(-9,2,16,3);vh.AddVert(new Vector2(3,-2),ink,Vector2.zero);vh.AddVert(new Vector2(3,9),ink,Vector2.zero);vh.AddVert(new Vector2(11,3.5f),ink,Vector2.zero);int n=vh.currentVertCount;vh.AddTriangle(n-3,n-2,n-1);}
            void Box(float x,float y,float w,float h){int n=vh.currentVertCount;vh.AddVert(new Vector2(x,y),ink,Vector2.zero);vh.AddVert(new Vector2(x,y+h),ink,Vector2.zero);vh.AddVert(new Vector2(x+w,y+h),ink,Vector2.zero);vh.AddVert(new Vector2(x+w,y),ink,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        }
    }
    public static class ReplayChrome
    {
        public static ReplayChromeGraphic Apply(GameObject target,ReplayChromeGraphic.Finish finish=ReplayChromeGraphic.Finish.Charcoal,bool panel=false)
        {
            var child=target.transform.Find("Replay Chrome");
            ReplayChromeGraphic chrome;
            if(!child){
                var go=new GameObject("Replay Chrome",typeof(RectTransform),typeof(CanvasRenderer),typeof(ReplayChromeGraphic));
                var rect=(RectTransform)go.transform;rect.SetParent(target.transform,false);rect.SetAsFirstSibling();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                chrome=go.GetComponent<ReplayChromeGraphic>();chrome.raycastTarget=false;
                if(target.TryGetComponent<Image>(out var image)){image.color=Color.clear;image.raycastTarget=true;}
                if(target.TryGetComponent<Button>(out var button)){
                    button.targetGraphic=chrome;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.1f,1.1f,1.06f);colors.pressedColor=new Color(.72f,.78f,.74f);colors.selectedColor=Color.white;colors.disabledColor=new Color(.5f,.5f,.5f,.7f);colors.fadeDuration=.12f;button.colors=colors;
                    var label=button.GetComponentInChildren<TMP_Text>(true);if(label){label.color=new Color(.96f,.94f,.84f);label.fontStyle=FontStyles.Bold;label.fontSize=21;}
                }
            }else chrome=child.GetComponent<ReplayChromeGraphic>();
            if(target.TryGetComponent<Image>(out var background))background.color=Color.clear;
            chrome.SetStyle(finish,panel);return chrome;
        }
    }
}

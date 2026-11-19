using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Confirmed coin delta only. No scoring, wallet mutation, input or colliders.
    public sealed class QuickShotBonusMotion : MonoBehaviour
    {
        RectTransform titleRect;
        CanvasGroup titleAlpha;
        QuickShotCoinGraphic coins;
        TMP_Text amount;
        MatchHudView owner;
        Vector2 origin, destination;
        float elapsed=3;
        public long Bonus {get;private set;}
        public bool IsShowing=>elapsed<2;
        public static QuickShotBonusMotion Install(RectTransform hud,MatchHudView view)
        {
            var go=new GameObject("Quick Shot Bonus",typeof(RectTransform),typeof(QuickShotBonusMotion));
            go.layer=hud.gameObject.layer;go.transform.SetParent(hud,false);
            var r=(RectTransform)go.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            var v=go.GetComponent<QuickShotBonusMotion>();v.owner=view;
            v.titleRect=Child("Bonus Title",go.transform,new Vector2(460,120));
            v.titleAlpha=v.titleRect.gameObject.AddComponent<CanvasGroup>();v.titleAlpha.alpha=0;v.titleAlpha.blocksRaycasts=false;v.titleAlpha.interactable=false;
            var art=Child("Quick Shot Artwork",v.titleRect,new Vector2(255,85));art.anchorMin=art.anchorMax=Vector2.one*.5f;art.anchoredPosition=new Vector2(-95,5);
            var image=art.gameObject.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>("Gameplay/MatchHud/QuickShot");image.raycastTarget=false;
            v.amount=Text("Extra Coins",v.titleRect,new Vector2(145,14),new Vector2(240,80),56);
            var sub=Text("Bonus Rate",v.titleRect,new Vector2(100,-42),new Vector2(250,30),21);sub.text="+20% 보너스";
            var graphic=new GameObject("Gold Coin Flight",typeof(RectTransform),typeof(CanvasRenderer),typeof(QuickShotCoinGraphic));graphic.layer=hud.gameObject.layer;graphic.transform.SetParent(go.transform,false);
            var gr=(RectTransform)graphic.transform;gr.anchorMin=Vector2.zero;gr.anchorMax=Vector2.one;gr.offsetMin=gr.offsetMax=Vector2.zero;
            v.coins=graphic.GetComponent<QuickShotCoinGraphic>();v.coins.raycastTarget=false;
            return v;
        }
        static RectTransform Child(string name,Transform parent,Vector2 size)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.gameObject.layer=parent.gameObject.layer;
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=Vector2.one*.5f;r.sizeDelta=size;return r;
        }
        static TMP_Text Text(string name,Transform parent,Vector2 position,Vector2 size,float fontSize)
        {
            var r=Child(name,parent,size);r.anchorMin=r.anchorMax=Vector2.one*.5f;r.anchoredPosition=position;
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            t.fontSize=fontSize;t.fontSizeMax=fontSize;t.fontSizeMin=18;t.enableAutoSizing=true;t.fontStyle=FontStyles.Bold|FontStyles.Italic;
            t.color=new Color32(255,213,91,255);t.enableVertexGradient=true;
            t.colorGradient=new VertexGradient(new Color(1,.98f,.77f),new Color(1,.98f,.77f),new Color(1,.58f,.08f),new Color(1,.58f,.08f));
            t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;t.raycastTarget=false;
            var shadow=r.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.08f,.015f,0,.95f);shadow.effectDistance=new Vector2(2,-3);
            return t;
        }
        public void Show(int side,long bonus)
        {
            if(bonus<=0)return;
            Bonus=bonus;amount.text="+"+bonus.ToString("N0",CultureInfo.InvariantCulture);
            origin=new Vector2(side==0?440:1120,250);destination=new Vector2(side==0?610:950,125);
            elapsed=0;titleAlpha.alpha=0;titleRect.localScale=Vector3.one*1.55f;
            titleRect.anchoredPosition=new Vector2(origin.x,-origin.y);
            transform.SetAsLastSibling();coins.Set(0,origin,destination);
        }
        void Update()
        {
            if(!owner || !owner.isActiveAndEnabled){Hide();return;}
            if(elapsed>=2)return;
            Sample(elapsed+Time.unscaledDeltaTime);
        }
        public void Sample(float time)
        {
            elapsed=Mathf.Clamp(time,0,2);
            float enter=1-Mathf.Pow(1-Mathf.Clamp01(elapsed/.18f),3);
            titleRect.localScale=Vector3.one*Mathf.Lerp(1.55f,1,enter);
            titleRect.anchoredPosition=new Vector2(origin.x,-origin.y+Mathf.SmoothStep(0,18,Mathf.Clamp01((elapsed-.85f)/.8f)));
            titleAlpha.alpha=Mathf.Clamp01(elapsed/.09f)*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-1.3f)/.5f)));
            coins.Set(elapsed,origin,destination);
        }
        public void Hide(){elapsed=3;if(titleAlpha)titleAlpha.alpha=0;if(coins)coins.Set(3,origin,destination);}
        void OnDisable(){Hide();}
    }

    public sealed class QuickShotCoinGraphic : MaskableGraphic
    {
        float time=3;Vector2 origin,destination;
        public void Set(float elapsed,Vector2 start,Vector2 end){time=elapsed;origin=start;destination=end;SetVerticesDirty();}
        Vector3 Local(Vector2 p)=>new Vector3(rectTransform.rect.xMin+p.x,rectTransform.rect.yMax-p.y);
        void Disc(VertexHelper vh,Vector2 p,float rx,float ry,Color center,Color edge)
        {
            int n=vh.currentVertCount;vh.AddVert(Local(p),center,Vector2.zero);
            const int count=18;
            for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;vh.AddVert(Local(p+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry)),edge,Vector2.zero);}
            for(int i=0;i<count;i++)vh.AddTriangle(n,n+i+1,n+i+2);
        }
        static Vector2 Flight(Vector2 a,Vector2 b,float t,int index)
        {
            float ease=1-Mathf.Pow(1-t,2);
            return Vector2.Lerp(a,b,ease)+new Vector2(Mathf.Sin(t*Mathf.PI)*(index%2==0?22:-22),-Mathf.Sin(t*Mathf.PI)*(70+index*3));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(time<.28f || time>1.65f)return;
            for(int i=0;i<9;i++)
            {
                float t=(time-.28f-i*.055f)/.65f;if(t<0||t>1)continue;
                Vector2 start=origin+new Vector2(110+i*3,18);Vector2 p=Flight(start,destination,t,i);
                float alpha=Mathf.Clamp01(t/.08f)*Mathf.Clamp01((1-t)/.12f);
                for(int tail=3;tail>0;tail--)
                {
                    Vector2 q=Flight(start,destination,Mathf.Max(0,t-tail*.025f),i);
                    Disc(vh,q,5-tail,3-tail*.4f,new Color(1,.68f,.1f,alpha*.13f),new Color(1,.64f,.03f,0));
                }
                float rx=5+Mathf.Abs(Mathf.Cos(t*9+i))*2;
                Disc(vh,p,rx+1,8,new Color(1,.62f,.04f,alpha),new Color(.65f,.24f,.015f,alpha));
                Disc(vh,p+new Vector2(-.4f,-.8f),rx*.72f,5.5f,new Color(1,.94f,.55f,alpha),new Color(1,.68f,.09f,alpha));
                Disc(vh,p+new Vector2(-2,-4),1.8f,1.2f,new Color(1,1,.88f,alpha),new Color(1,1,.8f,0));
            }
            float pulse=1-Mathf.Abs((time-1.02f)/.28f);
            if(pulse>0)Disc(vh,destination,22,12,new Color(1,.85f,.36f,pulse*.5f),new Color(1,.7f,.1f,0));
        }
    }
}

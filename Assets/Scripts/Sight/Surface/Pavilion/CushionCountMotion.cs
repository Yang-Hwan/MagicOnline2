using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Presentation only: never awards points or changes collision/rule state.
    public sealed class CushionCountMotion : MonoBehaviour
    {
        static CushionCountMotion current;
        readonly List<Image> glyphs=new List<Image>();
        RectTransform artwork;
        static Sprite[] sprites;
        RectTransform rect;
        CanvasGroup opacity;
        Behaviour matchPanel;
        float elapsed=1;
        const float Duration=.65f;

        public static void Install(Transform canvas,TMP_FontAsset font,Behaviour panel)
        {
            var go=new GameObject("Cushion Count Motion",typeof(RectTransform),typeof(CanvasGroup),typeof(CushionCountMotion));
            go.layer=canvas.gameObject.layer;go.transform.SetParent(canvas,false);
            var motion=go.GetComponent<CushionCountMotion>();
            motion.rect=go.GetComponent<RectTransform>();
            motion.rect.anchorMin=motion.rect.anchorMax=new Vector2(.5f,.5f);
            motion.rect.sizeDelta=new Vector2(440,150);
            var art=new GameObject("Brush Lettering",typeof(RectTransform));
            art.layer=go.layer;art.transform.SetParent(go.transform,false);
            motion.artwork=art.GetComponent<RectTransform>();
            motion.opacity=go.GetComponent<CanvasGroup>();motion.opacity.alpha=0;
            motion.opacity.blocksRaycasts=false;motion.opacity.interactable=false;
            motion.matchPanel=panel;current=motion;
        }

        public static void Show(int count)
        {
            if(!current || count<1 || !current.matchPanel || !current.matchPanel.isActiveAndEnabled)return;
            if(!current.SetArtwork(count))return;
            current.elapsed=0;current.opacity.alpha=1;
            current.rect.localScale=Vector3.one*.65f;
        }

        public static Sprite BrushGlyph(int index)
        {
            if(sprites==null) {
                var atlas=Resources.Load<Texture2D>("Gameplay/CushionGlyphs");
                if(!atlas) {Debug.LogError("Missing cushion brush-lettering atlas.");return null;}
                // Pixel bounds in the original 1536x1024 atlas; retain ink edges with padding.
                var bounds=new[] {
                    new Rect(78,674,267,287),new Rect(475,667,210,301),
                    new Rect(802,664,320,295),new Rect(1201,673,263,295),
                    new Rect(61,332,291,305),new Rect(439,337,284,301),
                    new Rect(837,333,233,304),new Rect(1224,337,254,297),
                    new Rect(72,24,257,275),new Rect(472,27,224,279),new Rect(918,21,440,262)
                };
                sprites=new Sprite[bounds.Length];
                for(int i=0;i<bounds.Length;i++) {
                    var b=bounds[i];b.xMin-=4;b.yMin-=4;b.xMax+=4;b.yMax+=4;
                    // Normalized bounds also handle platform texture downscaling.
                    b=new Rect(b.x*atlas.width/1536f,b.y*atlas.height/1024f,
                        b.width*atlas.width/1536f,b.height*atlas.height/1024f);
                    sprites[i]=Sprite.Create(atlas,b,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                }
            }
            return sprites[Mathf.Clamp(index,0,10)];
        }

        bool SetArtwork(int count)
        {
            if(!BrushGlyph(0))return false;
            string digits=count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int length=digits.Length+1;
            while(glyphs.Count<length) {
                var go=new GameObject("Glyph",typeof(RectTransform),typeof(Image));
                go.layer=gameObject.layer;go.transform.SetParent(artwork,false);
                var img=go.GetComponent<Image>();img.raycastTarget=false;glyphs.Add(img);
            }
            var tint=count==1 ? new Color(.176f,.898f,1) :
                count==2 ? new Color(1,.843f,.176f) : new Color(1,.71f,.106f);
            float width=0;
            for(int i=0;i<glyphs.Count;i++) {
                var img=glyphs[i];img.gameObject.SetActive(i<length);
                if(i>=length)continue;
                bool suffix=i==digits.Length;
                img.sprite=sprites[suffix ? 10 : digits[i]-'0'];
                img.color=suffix ? Color.white : tint;
                float height=suffix ? 70 : 100;
                float w=height*img.sprite.rect.width/img.sprite.rect.height;
                img.rectTransform.sizeDelta=new Vector2(w,height);
                width+=w;
            }
            const float spacing=-10;
            width+=spacing*(length-1);
            float x=-width*.5f;
            for(int i=0;i<length;i++) {
                var r=glyphs[i].rectTransform;
                r.anchoredPosition=new Vector2(x+r.sizeDelta.x*.5f,0);
                x+=r.sizeDelta.x+spacing;
            }
            artwork.localScale=Vector3.one*Mathf.Min(1,400/width);
            return true;
        }

        public static void Hide()
        {
            if(!current)return;
            current.elapsed=Duration;
            if(current.opacity)current.opacity.alpha=0;
            if(current.rect) {
                current.rect.anchoredPosition=Vector2.zero;
                current.rect.localRotation=Quaternion.identity;
                current.rect.localScale=Vector3.one;
            }
        }

        void Update()
        {
            if(!matchPanel || !matchPanel.isActiveAndEnabled) {if(current==this)Hide();return;}
            if(elapsed>=Duration)return;
            elapsed+=Time.unscaledDeltaTime;
            float pop=elapsed<.09f ? Mathf.Lerp(.65f,1.18f,elapsed/.09f) :
                Mathf.Lerp(1.18f,1,Mathf.Clamp01((elapsed-.09f)/.13f));
            float shake=Mathf.Sin(elapsed*65)*6*Mathf.Clamp01(1-elapsed/.3f);
            rect.localScale=Vector3.one*pop;
            rect.anchoredPosition=new Vector2(shake,Mathf.Sin(elapsed*48)*2*Mathf.Clamp01(1-elapsed/.3f));
            rect.localRotation=Quaternion.Euler(0,0,shake*.45f);
            opacity.alpha=1-Mathf.Clamp01((elapsed-.36f)/.29f);
            if(elapsed>=Duration && current==this)Hide();
        }

        void OnDisable() {if(current==this)Hide();}
        void OnDestroy() {if(current==this)current=null;}
    }
}

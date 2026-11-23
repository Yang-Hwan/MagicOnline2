using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Presentation only: original transforms remain the authority for input, networking and shots.
    [DefaultExecutionOrder(1000)]
    public sealed class ElasticShotControls : MonoBehaviour
    {
        public const float PowerTravel=.91f, FollowTravel=PowerTravel*3f/7f, LegacyFollowTravel=.31f;
        public static ElasticShotControls Active {get;private set;}
        public static bool UsingNewAppearance => Active && Active.shown;
        public static bool InputBlocked => Active && Active.shown && Active.IsCovered;
        public static float PowerDragScale => UsingNewAppearance ? Active.dragScale : 1;
        public static float FollowDragScale => UsingNewAppearance ? Active.followDragScale : 1;
        float dragScale=1,followDragScale=1;
        const float BottomPadding=1.15f;
        ElasticControlSettings settings;
        MonoBehaviour owner;
        Func<Camera> cameraProvider;
        Func<bool> opponentTurn;
        Func<bool> gameplayVisible;
        bool suppressed;
        public bool IsCovered => opponentTurn?.Invoke()==true;
        Transform power,follow,space;
        Renderer[] oldRenderers;bool[] oldEnabled;
        TMP_Text oldPower,oldFollow,powerLabel,followLabel;
        RectTransform canvasRoot,panel,powerPad,followPad,zero,stem,stemOcclusion,powerDisplay,followDisplay,metalCover;
        ElasticControlGraphic band;
        Material plastic,rubber;
        Texture2D photoAtlas;
        Rect screenRect;
        bool shown;
        public static ElasticShotControls Install(MonoBehaviour owner,Transform power,Transform follow,TMP_Text powerText,TMP_Text followText,Func<Camera> cameraProvider,Func<bool> opponentTurn=null,Func<bool> gameplayVisible=null)
        {
            if(!power || !follow)return null;
            var view=owner.gameObject.AddComponent<ElasticShotControls>();
            view.cameraProvider=cameraProvider;view.owner=owner;view.power=power;view.follow=follow;view.space=power.parent;
            view.opponentTurn=opponentTurn;view.gameplayVisible=gameplayVisible;
            view.oldPower=powerText;view.oldFollow=followText;
            view.settings=Resources.Load<ElasticControlSettings>("Gameplay/ElasticControls/Settings");
            view.oldRenderers=view.space.GetComponentsInChildren<Renderer>(true);
            view.oldEnabled=Array.ConvertAll(view.oldRenderers,r=>r.enabled);
            view.Build(powerText ? powerText.font : TMP_Settings.defaultFontAsset);
            return view;
        }
        static RectTransform Node(string name,Transform parent)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;return r;
        }
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        ElasticControlGraphic Surface(string name,Transform parent,Color color,bool isRubber=false)
        {
            var g=Node(name,parent).gameObject.AddComponent<ElasticControlGraphic>();g.rubber=isRubber;g.color=color;g.material=isRubber?rubber:plastic;g.raycastTarget=false;return g;
        }
        TMP_Text Label(string name,Transform parent,TMP_FontAsset font)
        {
            var t=Node(name,parent).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;if(font)t.fontSharedMaterial=font.material;
            t.color=new Color(.96f,.92f,.81f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.fontSize=13;t.enableAutoSizing=false;return t;
        }
        RectTransform Display(string name,TMP_FontAsset font,out TMP_Text label)
        {
            var rim=Surface(name,panel,new Color(.16f,.17f,.16f)).rectTransform;
            Surface("Smoked glass",rim,new Color(.012f,.014f,.012f));
            label=Label("Digits",rim,font);
            return rim;
        }
        void PlaceDisplay(RectTransform display,TMP_Text label,float x,float y,float w,float h,float scale)
        {
            Place(display,x,y,w,h);
            Place((RectTransform)display.Find("Smoked glass"),scale,scale,w-2*scale,h-2*scale);
            Place(label.rectTransform,0,0,w,h);
            label.fontSize=14*scale;
        }
        public static string GaugeText(float normalized) =>
            (Mathf.Clamp01(normalized)*10f).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
        void ApplyPhoto(ElasticControlGraphic graphic, Rect pixelBounds, bool endCaps=false)
        {
            if(!photoAtlas)return;
            graphic.texture=photoAtlas;
            graphic.sourcePixelSize=pixelBounds.size;
            graphic.uvRect=new Rect(pixelBounds.x/photoAtlas.width,1-pixelBounds.yMax/photoAtlas.height,pixelBounds.width/photoAtlas.width,pixelBounds.height/photoAtlas.height);
            graphic.color=Color.white;
            graphic.material=null; // UI/Default preserves the image lighting and alpha.
            graphic.preserveEndCaps=endCaps;
        }
        void Build(TMP_FontAsset font)
        {
            photoAtlas=Resources.Load<Texture2D>("Gameplay/ElasticControls/RoundedGunmetalParts");
            var shader=Resources.Load<Shader>("Gameplay/ElasticControls/Surface");
            plastic=new Material(shader);rubber=new Material(shader);rubber.SetFloat("_Rubber",1);
            canvasRoot=Node("Elastic shot controls",transform);var canvas=canvasRoot.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=109;
            panel=Surface("Plastic shell",canvasRoot,new Color(.22f,.23f,.23f)).rectTransform;
            Surface("Recess",panel,new Color(.055f,.06f,.06f));
            stemOcclusion=Surface("Original dark inset",panel,Color.black).rectTransform;
            stem=Surface("Sliding metal stem",panel,new Color(.65f,.65f,.65f)).rectTransform;
            band=Surface("Textured elastic",panel,new Color(.56f,.085f,.11f),true);
            zero=Node("Fixed zero",panel);
            Surface("Anchor clamp",zero,new Color(.28f,.30f,.30f));
            Surface("Left notch",zero,new Color(.62f,.64f,.62f));Surface("Right notch",zero,new Color(.62f,.64f,.62f));
            // Fixed displays are behind both moving parts, so the cap/grip occlude
            // the glass and digits together rather than moving the values with them.
            powerDisplay=Display("Fixed power display",font,out powerLabel);
            followDisplay=Display("Fixed follow display",font,out followLabel);
            followPad=Surface("Follow cap",panel,new Color(.19f,.20f,.20f)).rectTransform;
            Surface("Stop lip",followPad,new Color(.045f,.05f,.05f));
            powerPad=Surface("Power grip",panel,new Color(.18f,.19f,.19f)).rectTransform;
            for(int i=0;i<3;i++)Surface("Grip rib "+i,powerPad,new Color(.35f,.36f,.36f));
            if(photoAtlas)
            {
                ApplyPhoto(panel.GetComponent<ElasticControlGraphic>(),new Rect(249,343,143,463),true);
                ApplyPhoto(band,new Rect(722,373,38,65));
                ApplyPhoto(powerPad.GetComponent<ElasticControlGraphic>(),new Rect(678,465,128,132));
                powerPad.GetComponent<ElasticControlGraphic>().roundedSilhouette=true;
                ApplyPhoto(followPad.GetComponent<ElasticControlGraphic>(),new Rect(255,170,129,63));
                ApplyPhoto(stem.GetComponent<ElasticControlGraphic>(),new Rect(286,233,67,109),true);
                // Only straight middle material stretches. The rounded ends,
                // screws and shaft fittings keep their photographed proportions.
                panel.GetComponent<ElasticControlGraphic>().sourceBottomBorder=85;
                panel.GetComponent<ElasticControlGraphic>().sourceTopBorder=45;
                stem.GetComponent<ElasticControlGraphic>().sourceBottomBorder=12;
                stem.GetComponent<ElasticControlGraphic>().sourceTopBorder=12;
                stemOcclusion.gameObject.SetActive(false);
                panel.Find("Recess").gameObject.SetActive(false);
                followPad.Find("Stop lip").gameObject.SetActive(false);
                for(int i=0;i<3;i++)powerPad.Find("Grip rib "+i).gameObject.SetActive(false);
            }
            var z=Label("Zero value",zero,font);z.text="0.0";
            zero.gameObject.SetActive(false);
            metalCover=Surface("Plain metal opponent cover",canvasRoot,new Color(.16f,.17f,.17f)).rectTransform;
            var cover=metalCover.GetComponent<ElasticControlGraphic>();
            cover.texture=Resources.Load<Texture2D>("Gameplay/ElasticControls/PlainMetalCover");
            if(cover.texture){
                var bounds=new Rect(347,113,326,1320);
                cover.sourcePixelSize=bounds.size;
                cover.uvRect=new Rect(bounds.x/cover.texture.width,1-bounds.yMax/cover.texture.height,bounds.width/cover.texture.width,bounds.height/cover.texture.height);
                cover.material=null;cover.color=Color.white;
                cover.preserveEndCaps=true;cover.sourceBottomBorder=185;cover.sourceTopBorder=170;
                cover.coverSilhouette=true;cover.silhouetteEndRadii=new Vector2(165,105);
            }
            metalCover.gameObject.SetActive(false);
            canvasRoot.gameObject.SetActive(false);
        }
        public static bool TryClassify(Vector2 pointer,out bool isFollow)
        {
            isFollow=false;var a=Active;if(!a || !a.shown || a.IsCovered)return false;
            var hit=a.screenRect;hit.yMin-=hit.width*BottomPadding;hit.yMax+=hit.width*.45f;
            if(!hit.Contains(pointer))return false;
            isFollow=pointer.y>=a.panel.position.y+a.panel.rect.height;return true;
        }
        public static float DisplayPosition(float localY) => localY>0?localY*FollowTravel/LegacyFollowTravel:localY;
        public static float BandWidth(float tension) => Mathf.Lerp(.65f,.44f,Mathf.Clamp01(tension));
        void LateUpdate()
        {
            if(gameplayVisible?.Invoke()==false)
            {
                if(!suppressed) { Restore(); foreach(var r in oldRenderers)if(r)r.enabled=false;
                    if(oldPower)oldPower.transform.parent.gameObject.SetActive(false);
                    if(oldFollow)oldFollow.transform.parent.gameObject.SetActive(false);
                    suppressed=true; }
                return;
            }
            if(suppressed)Restore();
            bool covered=owner && owner.isActiveAndEnabled && IsCovered;
            bool want=settings && settings.useNewAppearance && owner && owner.isActiveAndEnabled && space && power && follow &&
                (covered || space.gameObject.activeInHierarchy && power.gameObject.activeInHierarchy && follow.gameObject.activeInHierarchy);
            if(!want){Restore();return;}
            var camera=cameraProvider?.Invoke();if(!camera)camera=Camera.main;if(!camera){Restore();return;}
            Vector3 low=camera.WorldToScreenPoint(space.TransformPoint(new Vector3(0,-PowerTravel,0)));
            Vector3 high=camera.WorldToScreenPoint(space.TransformPoint(new Vector3(0,FollowTravel,0)));
            if(low.z<=0 || high.z<=0 || high.y-low.y<10){Restore();return;}
            float projectedHeight=high.y-low.y;
            // Photoshop target at 1368x768: body (10,334)-(98,736), cap bottom 239 at full follow.
            float uiScale=camera.pixelHeight/768f;
            float width=88*uiScale, bodyHeight=settings.bodyHeight*uiScale;
            float gripH=92*uiScale, capH=39*uiScale;
            float powerTravelPixels=(settings.bodyHeight-100)*uiScale, followTravelPixels=settings.followTravel*uiScale;
            float fixedOrigin=camera.pixelRect.yMin+(32+settings.bodyHeight)*uiScale;
            low.y=fixedOrigin-powerTravelPixels;
            dragScale=projectedHeight*.7f/powerTravelPixels;
            followDragScale=projectedHeight*LegacyFollowTravel/(PowerTravel+FollowTravel)/followTravelPixels;
            screenRect=new Rect(camera.pixelRect.xMin+10*uiScale,low.y,width,powerTravelPixels+followTravelPixels);
            if(!shown){shown=true;canvasRoot.gameObject.SetActive(true);}
            Active=this;
            foreach(var r in oldRenderers)if(r)r.enabled=false;
            if(oldPower)oldPower.transform.parent.gameObject.SetActive(false);
            if(oldFollow)oldFollow.transform.parent.gameObject.SetActive(false);
            Place(panel,screenRect.x,camera.pixelRect.yMin+32*uiScale,width,bodyHeight);
            // The cover has its own fixed layout; it never moves network/input
            // handles and stays visible even when the opponent hides those handles.
            Place(metalCover,screenRect.x-2*uiScale,camera.pixelRect.yMin+30*uiScale,width+4*uiScale,bodyHeight+followTravelPixels+capH+4*uiScale);
            metalCover.gameObject.SetActive(covered);
            panel.gameObject.SetActive(!covered);
            Place((RectTransform)panel.Find("Recess"),3,3,width-6,bodyHeight-6);
            float origin=bodyHeight;
            float powerY=Mathf.Min(power.localPosition.y,follow.localPosition.y);
            float p=origin+(powerY>0 ? powerY/LegacyFollowTravel*followTravelPixels : powerY/PowerTravel*powerTravelPixels);
            float f=origin+Mathf.Clamp01(follow.localPosition.y/LegacyFollowTravel)*followTravelPixels;
            float tension=Mathf.Clamp01(-power.localPosition.y/PowerTravel);
            // Hide only the stem baked into the source housing; preserve original top screws and rails.
            Place(stemOcclusion,width*.25f,origin-45*uiScale,width*.50f,45*uiScale);
            // Top cap is never stretched. Only the straight shaft below it extends.
            Place(stem,22*uiScale,origin,44*uiScale,Mathf.Max(0,f-origin)+2*uiScale);
            Place(powerPad,2*uiScale,p-gripH,84*uiScale,gripH);
            Place(followPad,2*uiScale,f,84*uiScale,capH);
            Place((RectTransform)followPad.Find("Stop lip"),0,-3,width*.94f,5);
            for(int i=0;i<3;i++)Place((RectTransform)powerPad.Find("Grip rib "+i),width*.19f,gripH*.15f+i*gripH*.13f,width*.46f,2);
            PlaceDisplay(powerDisplay,powerLabel,25*uiScale,43*uiScale,38*uiScale,20*uiScale,uiScale);
            PlaceDisplay(followDisplay,followLabel,25*uiScale,origin+4*uiScale,38*uiScale,20*uiScale,uiScale);
            powerLabel.text=GaugeText(tension);
            followLabel.text=GaugeText(follow.localPosition.y/LegacyFollowTravel);
            Place(zero,0,origin,width,1);
            Place((RectTransform)zero.Find("Anchor clamp"),width*.16f,-1,width*.68f,4);
            Place((RectTransform)zero.Find("Left notch"),1,-2,width*.12f,4);Place((RectTransform)zero.Find("Right notch"),width*.87f,-2,width*.12f,4);
            Place((RectTransform)zero.Find("Zero value"),width*.70f,2,width*.30f,12);
            float bandW=width*BandWidth(tension);
            // Show the elastic only while the grip is pulled below the body top.
            // Returning to the follow-through stop must not leave a tail under it.
            band.gameObject.SetActive(power.localPosition.y<-.0001f);
            float bottom=p;
            float top=origin;
            Place(band.rectTransform,(width-bandW)/2,bottom,bandW,Mathf.Max(3,top-bottom));
            band.tension=tension;band.SetVerticesDirty();
        }
        void Restore()
        {
            if(!shown&&!suppressed)return;shown=false;suppressed=false;if(Active==this)Active=null;
            if(canvasRoot)canvasRoot.gameObject.SetActive(false);
            for(int i=0;i<oldRenderers.Length;i++)if(oldRenderers[i])oldRenderers[i].enabled=oldEnabled[i];
            if(oldPower)oldPower.transform.parent.gameObject.SetActive(true);
            if(oldFollow)oldFollow.transform.parent.gameObject.SetActive(true);
        }
        void OnDisable()=>Restore();
        void OnDestroy(){Restore();if(canvasRoot)Destroy(canvasRoot.gameObject);if(plastic)Destroy(plastic);if(rubber)Destroy(rubber);}
    }
}

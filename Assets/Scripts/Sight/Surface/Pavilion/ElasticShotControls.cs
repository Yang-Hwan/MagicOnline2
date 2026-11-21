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
        public static float PowerDragScale => UsingNewAppearance ? Active.dragScale : 1;
        public static float FollowDragScale => UsingNewAppearance ? Active.followDragScale : 1;
        float dragScale=1,followDragScale=1;
        const float HousingAspect=3.45f, BottomPadding=1.15f, GripWidth=.94f;
        ElasticControlSettings settings;
        MonoBehaviour owner;
        Func<Camera> cameraProvider;
        Transform power,follow,space;
        Renderer[] oldRenderers;bool[] oldEnabled;
        TMP_Text oldPower,oldFollow,powerLabel,followLabel;
        RectTransform canvasRoot,panel,powerPad,followPad,zero,stem,stemOcclusion;
        ElasticControlGraphic band;
        Material plastic,rubber;
        Texture2D photoAtlas;
        Rect screenRect;
        bool shown;
        public static ElasticShotControls Install(MonoBehaviour owner,Transform power,Transform follow,TMP_Text powerText,TMP_Text followText,Func<Camera> cameraProvider)
        {
            if(!power || !follow)return null;
            var view=owner.gameObject.AddComponent<ElasticShotControls>();
            view.cameraProvider=cameraProvider;view.owner=owner;view.power=power;view.follow=follow;view.space=power.parent;
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
            t.color=new Color(.85f,.85f,.80f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.fontSize=13;t.enableAutoSizing=true;t.fontSizeMin=8;t.fontSizeMax=13;return t;
        }
        void ApplyPhoto(ElasticControlGraphic graphic, Rect pixelBounds, bool endCaps=false)
        {
            if(!photoAtlas)return;
            graphic.texture=photoAtlas;
            graphic.sourcePixelSize=pixelBounds.size;
            graphic.uvRect=new Rect(pixelBounds.x/1860f,1-pixelBounds.yMax/845f,pixelBounds.width/1860f,pixelBounds.height/845f);
            graphic.color=Color.white;
            graphic.material=null; // UI/Default preserves the image lighting and alpha.
            graphic.preserveEndCaps=endCaps;
        }
        void Build(TMP_FontAsset font)
        {
            photoAtlas=Resources.Load<Texture2D>("Gameplay/ElasticControls/ApprovedParts");
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
            followPad=Surface("Follow cap",panel,new Color(.19f,.20f,.20f)).rectTransform;
            Surface("Stop lip",followPad,new Color(.045f,.05f,.05f));
            powerPad=Surface("Power grip",panel,new Color(.18f,.19f,.19f)).rectTransform;
            for(int i=0;i<3;i++)Surface("Grip rib "+i,powerPad,new Color(.35f,.36f,.36f));
            if(photoAtlas)
            {
                ApplyPhoto(panel.GetComponent<ElasticControlGraphic>(),new Rect(253,348,132,447),true);
                ApplyPhoto(band,new Rect(722,367,38,84));
                ApplyPhoto(powerPad.GetComponent<ElasticControlGraphic>(),new Rect(682,463,120,134));
                ApplyPhoto(followPad.GetComponent<ElasticControlGraphic>(),new Rect(258,174,123,56));
                ApplyPhoto(stem.GetComponent<ElasticControlGraphic>(),new Rect(289,234,60,148));
                ApplyPhoto(stemOcclusion.GetComponent<ElasticControlGraphic>(),new Rect(292,400,56,230));
                panel.GetComponent<ElasticControlGraphic>().sourceBottomBorder=50;
                panel.GetComponent<ElasticControlGraphic>().sourceTopBorder=40;
                followPad.GetComponent<ElasticControlGraphic>().domeSilhouette=true;
                panel.Find("Recess").gameObject.SetActive(false);
                followPad.Find("Stop lip").gameObject.SetActive(false);
                for(int i=0;i<3;i++)powerPad.Find("Grip rib "+i).gameObject.SetActive(false);
            }
            followLabel=Label("Follow value",followPad,font);powerLabel=Label("Power value",powerPad,font);
            var z=Label("Zero value",zero,font);z.text="0.0";
            powerLabel.gameObject.SetActive(false);followLabel.gameObject.SetActive(false);zero.gameObject.SetActive(false);
            canvasRoot.gameObject.SetActive(false);
        }
        public static bool TryClassify(Vector2 pointer,out bool isFollow)
        {
            isFollow=false;var a=Active;if(!a || !a.shown)return false;
            var hit=a.screenRect;hit.yMin-=hit.width*BottomPadding;hit.yMax+=hit.width*.45f;
            if(!hit.Contains(pointer))return false;
            isFollow=pointer.y>=a.panel.position.y+a.panel.rect.height;return true;
        }
        public static float DisplayPosition(float localY) => localY>0?localY*FollowTravel/LegacyFollowTravel:localY;
        public static float BandWidth(float tension) => Mathf.Lerp(.65f,.44f,Mathf.Clamp01(tension));
        void LateUpdate()
        {
            bool want=settings && settings.useNewAppearance && owner && owner.isActiveAndEnabled && space && space.gameObject.activeInHierarchy && power.gameObject.activeInHierarchy && follow.gameObject.activeInHierarchy;
            if(!want){Restore();return;}
            var camera=cameraProvider?.Invoke();if(!camera)camera=Camera.main;if(!camera){Restore();return;}
            Vector3 low=camera.WorldToScreenPoint(space.TransformPoint(new Vector3(0,-PowerTravel,0)));
            Vector3 high=camera.WorldToScreenPoint(space.TransformPoint(new Vector3(0,FollowTravel,0)));
            if(low.z<=0 || high.z<=0 || high.y-low.y<10){Restore();return;}
            float projectedHeight=high.y-low.y;
            // Photoshop target at 1368x768: body (10,334)-(98,736), cap bottom 239 at full follow.
            float uiScale=camera.pixelHeight/768f;
            float width=88*uiScale, bodyHeight=402*uiScale;
            float gripH=92*uiScale, capH=39*uiScale;
            float powerTravelPixels=302*uiScale, followTravelPixels=95*uiScale;
            float fixedOrigin=camera.pixelRect.yMin+434*uiScale;
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
            Place(powerLabel.rectTransform,0,gripH*.73f,width*GripWidth,gripH*.18f);
            Place(followLabel.rectTransform,0,1,width*.94f,capH-2);
            powerLabel.text="파워 "+tension.ToString("0.00");
            followLabel.text="팔로 "+Mathf.Clamp01(follow.localPosition.y/LegacyFollowTravel).ToString("0.00");
            Place(zero,0,origin,width,1);
            Place((RectTransform)zero.Find("Anchor clamp"),width*.16f,-1,width*.68f,4);
            Place((RectTransform)zero.Find("Left notch"),1,-2,width*.12f,4);Place((RectTransform)zero.Find("Right notch"),width*.87f,-2,width*.12f,4);
            Place((RectTransform)zero.Find("Zero value"),width*.70f,2,width*.30f,12);
            float bandW=width*BandWidth(tension);
            // Below zero the origin is fixed. Above zero a short relaxed tail follows the grip.
            float bottom=power.localPosition.y<0?p:p-gripH-width*.13f;
            float top=power.localPosition.y<0?origin:p-gripH+2;
            Place(band.rectTransform,(width-bandW)/2,bottom,bandW,Mathf.Max(3,top-bottom));
            band.tension=tension;band.SetVerticesDirty();
        }
        void Restore()
        {
            if(!shown)return;shown=false;if(Active==this)Active=null;
            if(canvasRoot)canvasRoot.gameObject.SetActive(false);
            for(int i=0;i<oldRenderers.Length;i++)if(oldRenderers[i])oldRenderers[i].enabled=oldEnabled[i];
            if(oldPower)oldPower.transform.parent.gameObject.SetActive(true);
            if(oldFollow)oldFollow.transform.parent.gameObject.SetActive(true);
        }
        void OnDisable()=>Restore();
        void OnDestroy(){Restore();if(canvasRoot)Destroy(canvasRoot.gameObject);if(plastic)Destroy(plastic);if(rubber)Destroy(rubber);}
    }
}

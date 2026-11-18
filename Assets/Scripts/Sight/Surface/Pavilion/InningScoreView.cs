using System;
using System.Collections.Generic;
using Assets.Scripts.Often;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    public sealed class InningScoreView : MonoBehaviour
    {
        static readonly List<InningScoreView> instances=new List<InningScoreView>();
        static readonly Color Gold=new Color32(239,200,112,255);
        const float Cell=64;
        Func<InningScoreHistory> source;
        Func<int,string> playerName;
        Func<int> selfSeat;
        InningScoreHistory history;
        RectTransform root,panel,buttonRect,viewport,content;
        CanvasGroup group;
        InningScoreIcon icon;
        ScrollRect scroll;
        TMP_Text[] names=new TMP_Text[2],totals=new TMP_Text[2];
        readonly List<RectTransform> columns=new List<RectTransform>();
        readonly List<TMP_Text[]> cells=new List<TMP_Text[]>();
        readonly List<Image[]> highlights=new List<Image[]>();
        TMP_FontAsset font;
        int revision=-1;
        float progress,scale=1,previousWidth;
        bool open,follow=true,changingScroll,scrollToLatest;
        Vector2 panelPosition;
        public bool IsOpen=>open;
        public ScrollRect InningsScroll=>scroll;
        public RectTransform Panel=>panel;
        public RectTransform IconButton=>buttonRect;
        public InningScoreHistory History=>source?.Invoke();

        public static InningScoreView Install(Transform owner,Func<InningScoreHistory> source,Func<int,string> name,Func<int> self)
        {
            var view=owner.GetComponent<InningScoreView>();
            if(!view)view=owner.gameObject.AddComponent<InningScoreView>();
            view.source=source;view.playerName=name;view.selfSeat=self;
            if(!view.root)view.Build();return view;
        }
        public void Bind(InningScoreHistory model){source=()=>model;revision=-1;}
        public static bool ContainsPointer(Vector2 point)
        {
            foreach(var view in instances)
                if(view && view.isActiveAndEnabled && view.root &&
                    (RectTransformUtility.RectangleContainsScreenPoint(view.buttonRect,point,view.UICamera) ||
                    view.open && RectTransformUtility.RectangleContainsScreenPoint(view.panel,point,view.UICamera)))return true;
            return false;
        }
        Camera UICamera {get {var canvas=root.GetComponentInParent<Canvas>();return canvas && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;}}
        void OnEnable(){if(!instances.Contains(this))instances.Add(this);}
        void OnDisable(){instances.Remove(this);}
        RectTransform Node(string name,Transform parent)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);return r;
        }
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        Image Fill(RectTransform r,Color color){var image=r.gameObject.AddComponent<Image>();image.color=color;return image;}
        TMP_Text Text(string name,Transform parent,string value,float size,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var r=Node(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;
            t.color=Color.white;t.alignment=alignment;t.fontStyle=FontStyles.Bold;t.richText=false;t.raycastTarget=false;
            t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        void Build()
        {
            font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            root=Node("Inning Score UI",transform);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.pivot=Vector2.one*.5f;root.offsetMin=root.offsetMax=Vector2.zero;
            panel=Node("Inning Score Panel",root);panel.anchorMin=panel.anchorMax=Vector2.one*.5f;panel.pivot=new Vector2(.5f,0);
            panel.gameObject.AddComponent<CanvasRenderer>();panel.gameObject.AddComponent<InningScorePanelGraphic>();
            group=panel.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;
            buttonRect=Node("Score Icon Button",root);buttonRect.anchorMin=buttonRect.anchorMax=buttonRect.pivot=Vector2.one*.5f;
            buttonRect.gameObject.AddComponent<CanvasRenderer>();
            icon=buttonRect.gameObject.AddComponent<InningScoreIcon>();var button=buttonRect.gameObject.AddComponent<Button>();button.targetGraphic=icon;button.transition=Selectable.Transition.None;button.onClick.AddListener(Toggle);
            Text("Fixed Player Header",panel,"선수",22);Text("Fixed Total Header",panel,"합계",22);
            for(int row=0;row<2;row++)
            {
                names[row]=Text("Fixed Player "+row,panel,"",24,TextAlignmentOptions.MidlineLeft);
                totals[row]=Text("Fixed Total "+row,panel,"0",28);totals[row].color=Gold;
            }
            foreach(string name in new[]{"Name Divider","Total Divider","Header Divider","Row Divider"})
            {var line=Node(name,panel);Fill(line,new Color(.8f,.67f,.36f,.4f)).raycastTarget=false;}
            var area=Node("Scrollable Innings Only",panel);scroll=area.gameObject.AddComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.inertia=true;scroll.decelerationRate=.08f;scroll.movementType=ScrollRect.MovementType.Clamped;
            area.gameObject.AddComponent<InningScoreNavigation>().Navigate=()=>{follow=false;scrollToLatest=false;};
            viewport=Node("Innings Viewport",area);Fill(viewport,new Color(0,0,0,.12f));viewport.gameObject.AddComponent<RectMask2D>();
            content=Node("Innings Content",viewport);scroll.viewport=viewport;scroll.content=content;scroll.scrollSensitivity=35;
            var bar=Node("Inning Scrollbar",area);Fill(bar,new Color(.28f,.27f,.22f,1));var sb=bar.gameObject.AddComponent<Scrollbar>();
            bar.gameObject.AddComponent<InningScoreNavigation>().Navigate=()=>{follow=false;scrollToLatest=false;};
            var track=Node("Track",bar);track.anchorMin=Vector2.zero;track.anchorMax=Vector2.one;track.offsetMin=track.offsetMax=Vector2.zero;
            var handle=Node("Thumb",track);handle.offsetMin=handle.offsetMax=Vector2.zero;Fill(handle,Gold);sb.handleRect=handle;sb.targetGraphic=handle.GetComponent<Image>();sb.direction=Scrollbar.Direction.LeftToRight;
            scroll.horizontalScrollbar=sb;
            scroll.onValueChanged.AddListener(_=>{if(!changingScroll)RenderColumns();});
            for(int i=0;i<24;i++)
            {
                var column=Node("Inning Column "+i,content);columns.Add(column);
                var divider=Node("Divider",column);Fill(divider,new Color(.7f,.58f,.3f,.15f)).raycastTarget=false;
                var texts=new TMP_Text[3];var glow=new Image[2];texts[0]=Text("Inning",column,"",22);
                for(int row=0;row<2;row++)
                {
                    var r=Node("Current Visit "+row,column);glow[row]=Fill(r,new Color(1,.75f,.2f,.18f));glow[row].raycastTarget=false;
                    texts[row+1]=Text("Score "+row,column,"",26);
                }
                cells.Add(texts);highlights.Add(glow);
            }
            Layout();Refresh();
        }
        public void Toggle(){SetOpen(!open);}
        public void SetOpen(bool value)
        {
            open=value;group.blocksRaycasts=value;group.interactable=value;icon.Open=value;icon.SetVerticesDirty();
            if(value){follow=true;revision=-1;Refresh();}
        }
        void Update()
        {
            if(!root)return;Layout();Refresh();
            progress=Mathf.Lerp(progress,open?1:0,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
            group.alpha=progress;panel.anchoredPosition=panelPosition+Vector2.down*(1-progress)*22*scale;
        }
        void LateUpdate()
        {
            if(!scroll || (!scrollToLatest && !(open && follow)))return;
            bool rebuild=scrollToLatest;scrollToLatest=false;changingScroll=true;
            if(rebuild)Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.horizontalNormalizedPosition=1;
            content.anchoredPosition=new Vector2(-Mathf.Max(0,content.rect.width-viewport.rect.width),0);
            changingScroll=false;RenderColumns();
        }
        void Refresh()
        {
            var model=source?.Invoke();if(model==null)return;
            if(!ReferenceEquals(history,model)){history=model;revision=-1;follow=true;}
            int self=Mathf.Clamp(selfSeat?.Invoke()??0,0,1);
            for(int row=0;row<2;row++){int seat=row==0?self:1-self;names[row].text="●  "+(playerName?.Invoke(seat)??"선수");names[row].color=seat==0?new Color(1,.97f,.85f):new Color(1,.85f,.4f);totals[row].text=model.Total(seat).ToString();}
            if(revision==model.Revision)return;revision=model.Revision;
            float width=Mathf.Max(viewport.rect.width,Mathf.Max(1,model.Columns)*Cell*scale);content.sizeDelta=new Vector2(width,112*scale);
            if(follow)scrollToLatest=true;
            RenderColumns();
        }
        void RenderColumns()
        {
            if(history==null)return;int first=Mathf.Max(0,Mathf.FloorToInt(-content.anchoredPosition.x/(Cell*scale)));
            int self=Mathf.Clamp(selfSeat?.Invoke()??0,0,1);
            for(int i=0;i<columns.Count;i++)
            {
                int index=first+i;bool visible=index<history.Columns && i*Cell*scale<viewport.rect.width+Cell*scale;columns[i].gameObject.SetActive(visible);if(!visible)continue;
                Place(columns[i],index*Cell*scale,0,Cell*scale,112*scale);cells[i][0].text=(index+1).ToString();
                Place((RectTransform)columns[i].Find("Divider"),0,0,1,110*scale);
                Place(cells[i][0].rectTransform,0,0,Cell*scale,32*scale);
                for(int row=0;row<2;row++)
                {
                    int seat=row==0?self:1-self;var score=history.Score(seat,index);cells[i][row+1].text=score.HasValue?score.Value.ToString():"—";
                    bool active=!history.Finished && seat==history.Active && index==history.Count(seat)-1;
                    highlights[i][row].gameObject.SetActive(active);Place(highlights[i][row].rectTransform,1,32*scale+row*38*scale,(Cell-2)*scale,38*scale);
                    Place(cells[i][row+1].rectTransform,0,(32+row*38)*scale,Cell*scale,38*scale);cells[i][row+1].color=active?Gold:score.HasValue?Color.white:new Color(.5f,.5f,.48f);
                }
            }
        }
        bool ProjectBounds(Bounds bounds,Camera camera,out Rect area)
        {
            Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var screen=camera.WorldToScreenPoint(new Vector3(bounds.center.x+x*bounds.extents.x,bounds.max.y,bounds.center.z+z*bounds.extents.z));
                if(screen.z<=0){area=default;return false;}
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,UICamera,out var p);min=Vector2.Min(min,p);max=Vector2.Max(max,p);
            }
            area=Rect.MinMaxRect(min.x,min.y,max.x,max.y);return area.width>1 && area.height>1;
        }
        void Layout()
        {
            float w=root.rect.width,h=root.rect.height;scale=w/1600f;if(scale<=0)return;
            Rect table=new Rect(-w*.4f,-h*.36f,w*.8f,h*.65f);var camera=Camera.main;var cloth=GameObject.Find("Table/ClothSpace");
            if(camera && cloth && ProjectBounds(new Bounds(cloth.transform.position,cloth.transform.lossyScale),camera,out var projected))table=projected;
            float pw=table.width*.96f;panel.sizeDelta=new Vector2(pw,148*scale);panelPosition=new Vector2(table.center.x,table.yMin+12*scale);
            buttonRect.sizeDelta=Vector2.one*58*1.8f*scale;var wheel=GameObject.Find("DetailTurn");Vector2 iconPosition=new Vector2(w*.45f,0);
            if(camera && wheel)
            {
                var shapes=wheel.GetComponentsInChildren<Renderer>();
                if(shapes.Length>0)
                {
                    var b=shapes[0].bounds;foreach(var shape in shapes)if(shape.enabled)b.Encapsulate(shape.bounds);
                    var screen=camera.WorldToScreenPoint(b.center);RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,UICamera,out var center);
                    float top=center.y;
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    {var p=camera.WorldToScreenPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));RectTransformUtility.ScreenPointToLocalPointInRectangle(root,p,UICamera,out var q);top=Mathf.Max(top,q.y);}
                    iconPosition=new Vector2(center.x,top+42*scale);
                }
            }
            float iconMargin=buttonRect.sizeDelta.x*.5f+8*scale;
            iconPosition.x=Mathf.Clamp(iconPosition.x,-w*.5f+iconMargin,w*.5f-iconMargin);iconPosition.y=Mathf.Clamp(iconPosition.y,-h*.5f+iconMargin,h*.5f-100*scale);buttonRect.anchoredPosition=iconPosition;
            if(Mathf.Abs(previousWidth-pw)<.1f)return;previousWidth=pw;revision=-1;
            float left=224*scale,right=96*scale,cw=pw-left-right;
            Place((RectTransform)panel.Find("Name Divider"),left-1,4*scale,1,112*scale);
            Place((RectTransform)panel.Find("Total Divider"),pw-right,4*scale,1,112*scale);
            Place((RectTransform)panel.Find("Header Divider"),8*scale,38*scale,pw-16*scale,1);
            Place((RectTransform)panel.Find("Row Divider"),8*scale,77*scale,pw-16*scale,1);
            Place(panel.Find("Fixed Player Header") as RectTransform,12*scale,5*scale,left-12*scale,32*scale);
            Place(panel.Find("Fixed Total Header") as RectTransform,pw-right,5*scale,right,32*scale);
            for(int row=0;row<2;row++){Place(names[row].rectTransform,14*scale,(39+row*38)*scale,left-24*scale,38*scale);Place(totals[row].rectTransform,pw-right,(39+row*38)*scale,right,38*scale);}
            var area=(RectTransform)scroll.transform;Place(area,left,5*scale,cw,135*scale);Place(viewport,0,0,cw,112*scale);
            Place((RectTransform)scroll.horizontalScrollbar.transform,10*scale,123*scale,cw-20*scale,6*scale);
            foreach(var texts in cells)foreach(var t in texts)t.fontSize=(t==texts[0]?22:26)*scale;
            foreach(var t in names)t.fontSize=24*scale;foreach(var t in totals)t.fontSize=28*scale;
        }
    }

    public sealed class InningScoreNavigation : MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IScrollHandler
    {
        public Action Navigate;
        public void OnPointerDown(PointerEventData data)=>Navigate?.Invoke();
        public void OnBeginDrag(PointerEventData data)=>Navigate?.Invoke();
        public void OnScroll(PointerEventData data)=>Navigate?.Invoke();
    }

    public sealed class InningScorePanelGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float thickness=Mathf.Max(1,r.height/140),radius=12*thickness;
            var inner=new Rect(r.xMin+thickness,r.yMin+thickness,r.width-2*thickness,r.height-2*thickness);
            var gold=new Color(.9f,.74f,.4f,.85f);var black=new Color(.006f,.008f,.012f,.96f);
            for(int i=0;i<28;i++)vh.AddVert(Point(r,radius,i),gold,Vector2.zero);
            for(int i=0;i<28;i++)vh.AddVert(Point(inner,radius-thickness,i),gold,Vector2.zero);
            for(int i=0;i<28;i++){int next=(i+1)%28;vh.AddTriangle(i,next,28+i);vh.AddTriangle(next,28+next,28+i);}
            int center=vh.currentVertCount;vh.AddVert(inner.center,black,Vector2.zero);
            for(int i=0;i<28;i++)vh.AddVert(Point(inner,radius-thickness,i),black,Vector2.zero);
            for(int i=0;i<28;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%28);
        }
        static Vector2 Point(Rect r,float radius,int index)
        {
            int corner=index/7,step=index%7;float angle=(corner*90+step*15)*Mathf.Deg2Rad;
            return new Vector2((corner==0 || corner==3?r.xMax-radius:r.xMin+radius)+Mathf.Cos(angle)*radius,
                (corner<2?r.yMax-radius:r.yMin+radius)+Mathf.Sin(angle)*radius);
        }
    }

    public sealed class InningScoreIcon : MaskableGraphic
    {
        public bool Open;
        Material metal;
        protected override void OnEnable()
        {
            base.OnEnable();
            var shader=Resources.Load<Shader>("Gameplay/MatchHud/ScoreIcon");
            if(!metal && shader && shader.isSupported){metal=new Material(shader);material=metal;}
        }
        protected override void OnDestroy(){if(metal)Destroy(metal);base.OnDestroy();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float s=rectTransform.rect.width/58;
            var rimTop=Open?new Color(1,.86f,.48f):new Color(.79f,.7f,.49f);
            var rimBottom=Open?new Color(.44f,.27f,.07f):new Color(.21f,.17f,.105f);
            Rounded(28,7,new Color(.055f,.065f,.075f),new Color(.52f,.56f,.59f));
            Rounded(27,6,rimBottom,rimTop);
            Rounded(25.5f,5,new Color(.004f,.006f,.009f),new Color(.075f,.085f,.095f));
            Rounded(24.4f,4.5f,new Color(.012f,.016f,.021f),new Color(.036f,.043f,.052f));
            for(int row=0;row<2;row++)Dot(-19,-7+row*15,2.1f);
            // A larger, raised silver grille fills the face without decorative lettering.
            Bevel(-12,-15,33,1.8f);Bevel(-12,0,33,1.8f);Bevel(-12,15,33,1.8f);
            for(int i=0;i<3;i++)Bevel(-12+i*15.6f,-15,1.8f,31.8f);
            void Vertex(float x,float y,Color color)=>vh.AddVert(new Vector3(x*s,y*s),color,new Vector2(x/58+.5f,y/58+.5f));
            void Rounded(float half,float radius,Color bottom,Color top)
            {
                Color Tint(float y)=>Color.Lerp(bottom,top,Mathf.InverseLerp(-half,half,y));
                int center=vh.currentVertCount;Vertex(0,0,Tint(0));
                for(int corner=0;corner<4;corner++)for(int step=0;step<=6;step++)
                {
                    float angle=(corner*90+step*15)*Mathf.Deg2Rad;
                    float x=(corner==0 || corner==3?1:-1)*(half-radius)+Mathf.Cos(angle)*radius;
                    float y=(corner<2?1:-1)*(half-radius)+Mathf.Sin(angle)*radius;
                    Vertex(x,y,Tint(y));
                }
                for(int i=0;i<28;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%28);
            }
            void Quad(float x,float y,float w,float h,Color bottom,Color top)
            {
                int start=vh.currentVertCount;Vertex(x,y,bottom);Vertex(x,y+h,top);Vertex(x+w,y+h,top);Vertex(x+w,y,bottom);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
            void Bevel(float x,float y,float w,float h)
            {
                var shadow=new Color(.002f,.003f,.004f);
                Quad(x+.45f,y-.55f,w,h,shadow,shadow);
                Quad(x,y,w,h,new Color(.27f,.31f,.35f),new Color(.85f,.88f,.91f));
                Quad(x,y+h-.35f,w,.35f,new Color(.95f,.97f,1),new Color(.95f,.97f,1));
            }
            void Dot(float x,float y,float radius)
            {
                int center=vh.currentVertCount;Vertex(x,y,new Color(.65f,.69f,.73f));
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI*2/16;float dy=Mathf.Sin(angle)*radius;
                    Vertex(x+Mathf.Cos(angle)*radius,y+dy,Color.Lerp(new Color(.19f,.23f,.27f),new Color(.92f,.95f,.98f),(dy/radius+1)*.5f));
                }
                for(int i=0;i<16;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%16);
            }
        }
    }
}

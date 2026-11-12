using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // Shared geometry and input boundary for the existing practice/online selector.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CuePointGuide : MaskableGraphic
    {
        public const float InnerRatio = .8f;
        static float EquipmentRatio => InnerRatio * Assets.Scripts.Exert.Network.MatchEquipmentRuntime.Shooter.TipScale;
        public static float Limit(RectTransform frame, RectTransform marker) =>
            Mathf.Max(0, Mathf.Min(frame.rect.width,frame.rect.height)*.5f*EquipmentRatio
                - Mathf.Max(marker.rect.width,marker.rect.height)*.5f);

        public static void Install(RectTransform frame)
        {
            // Keep its RectTransform for drag interpolation, but hide the debug marker.
            var debugPoint=frame.parent.Find("Point_V");
            if(debugPoint) {
                var debugGraphic=debugPoint.GetComponent<Graphic>();
                if(debugGraphic) {
                    debugGraphic.enabled=false;
                    debugGraphic.raycastTarget=false;
                }
            }
            var old=frame.GetComponent<Image>();
            // This graphic now draws the white fill as well as the guide rings.
            if(old) old.enabled=false;
            var bottom=frame.Find("bottom");
            if(bottom) {
                bottom.gameObject.SetActive(true);
                // Keep the requested white bottom line legible over the white fill.
                var shadow=bottom.GetComponent<Shadow>();
                if(!shadow)shadow=bottom.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(0,0,0,.35f);
                shadow.effectDistance=new Vector2(0,-2);
            }
            var existing=frame.Find("Cue Point Guide");
            if(existing) {
                existing.gameObject.layer=frame.gameObject.layer;
                if(!existing.GetComponent<CanvasRenderer>())existing.gameObject.AddComponent<CanvasRenderer>();
                existing.gameObject.SetActive(true);
                var guide=existing.GetComponent<CuePointGuide>();
                if(guide) {guide.enabled=true;guide.SetAllDirty();}
                return;
            }
            var go=new GameObject("Cue Point Guide",typeof(RectTransform),typeof(CanvasRenderer),typeof(CuePointGuide));
            go.layer=frame.gameObject.layer;
            var rect=go.GetComponent<RectTransform>();rect.SetParent(frame,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;rect.SetAsFirstSibling();
            go.GetComponent<CuePointGuide>().raycastTarget=false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();
            float r=Mathf.Min(rect.width,rect.height)*.5f;
            if(r<=3)return;
            Disc(mesh,rect.center,r-1,Color.white);
            Ring(mesh,rect.center,r-3,4,new Color(1,1,1,.95f));
            Ring(mesh,rect.center,r*EquipmentRatio,2,new Color(0,0,0,.25f));
            // Centre the upward arrow halfway between the ball centre and its top rim.
            var arrowCentre=rect.center+Vector2.up*(r*.5f);
            var a=arrowCentre+Vector2.down*30;
            var b=arrowCentre+Vector2.up*30;
            Line(mesh,a,b,4,new Color(0,0,0,.4f));
            Line(mesh,b,b+new Vector2(-16,-18),4,new Color(0,0,0,.4f));
            Line(mesh,b,b+new Vector2(16,-18),4,new Color(0,0,0,.4f));
        }

        static void Disc(VertexHelper mesh,Vector2 centre,float radius,Color tint)
        {
            const int count=192;
            int start=mesh.currentVertCount;
            mesh.AddVert(centre,tint,Vector2.zero);
            for(int i=0;i<=count;i++) {
                float angle=i*2*Mathf.PI/count;
                var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(centre+n*radius,tint,Vector2.zero);
                if(i>0)mesh.AddTriangle(start,start+i,start+i+1);
            }
        }

        static void Ring(VertexHelper mesh,Vector2 centre,float radius,float width,Color tint)
        {
            const int count=192;
            int start=mesh.currentVertCount;
            for(int i=0;i<=count;i++) {
                float angle=i*2*Mathf.PI/count;
                var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(centre+n*(radius-width*.5f),tint,Vector2.zero);
                mesh.AddVert(centre+n*(radius+width*.5f),tint,Vector2.zero);
                if(i==0)continue;
                int j=start+2*i;
                mesh.AddTriangle(j-2,j-1,j);mesh.AddTriangle(j-1,j+1,j);
            }
        }

        static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 d=(b-a).normalized;
            var n=new Vector2(-d.y,d.x)*width*.5f;
            int i=mesh.currentVertCount;
            mesh.AddVert(a-n,tint,Vector2.zero);mesh.AddVert(a+n,tint,Vector2.zero);
            mesh.AddVert(b+n,tint,Vector2.zero);mesh.AddVert(b-n,tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }
}

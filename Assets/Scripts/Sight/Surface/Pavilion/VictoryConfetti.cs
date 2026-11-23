using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // One UI mesh; no physics, textures, input interception or per-frame allocations.
    public sealed class VictoryConfetti : MaskableGraphic
    {
        const int Count = 190;
        const int OpeningCount = 70;
        readonly Vector3[] corners = new Vector3[4];
        readonly UIVertex[] quad = new UIVertex[4];
        static readonly Color[] Palette = {
            new Color(1f,.73f,.18f), new Color(.98f,.34f,.42f),
            new Color(.22f,.82f,.7f), new Color(.36f,.66f,1f),
            new Color(.78f,.48f,.95f), new Color(1f,.91f,.59f)
        };
        float elapsed;
        bool playing;
        Rect area;
        public void Play()
        {
            raycastTarget = false;
            maskable = false;
            gameObject.SetActive(true);
            elapsed = 0;
            playing = true;
            SetVerticesDirty();
        }
        public void Stop()
        {
            playing = false;
            gameObject.SetActive(false);
        }
        void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }
        static float Noise(int index,int salt) => Mathf.Repeat(Mathf.Sin(index*127.1f+salt*311.7f)*43758.5453f,1f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!playing || !canvas) return;
            ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(corners);
            var bottom = rectTransform.InverseTransformPoint(corners[0]);
            var top = rectTransform.InverseTransformPoint(corners[2]);
            area = Rect.MinMaxRect(bottom.x,bottom.y,top.x,top.y);
            float scale = area.height / 900f;
            float fade = Mathf.Clamp01(elapsed/.18f);
            for (int i=0;i<Count;i++)
            {
                // Seed the opening shower at different heights, then feed the whole
                // width continuously. Each piece recycles only after leaving the screen.
                float speed=(170+Noise(i,4)*170)*scale;
                float spawnY=area.yMax+(20+Noise(i,8)*80)*scale;
                float flight=(spawnY-area.yMin+30*scale)/Mathf.Max(.01f,speed);
                float start=i<OpeningCount ? -Noise(i,1)*flight*.45f : Noise(i,1)*2.6f;
                float totalAge=elapsed-start;
                if(totalAge<0)continue;
                int pass=Mathf.FloorToInt(totalAge/flight);
                float age=Mathf.Repeat(totalAge,flight);
                float phase=Noise(i+pass*Count,2)*Mathf.PI*2;
                float drift=(18+Noise(i,9)*34)*scale;
                float x=area.xMin+area.width*Noise(i+pass*Count,3)
                    +Mathf.Sin(age*(1.3f+Noise(i,10))+phase)*drift
                    +Mathf.Sin(age*3.1f+phase)*8*scale;
                float y=spawnY-age*speed;
                if(y<area.yMin-20*scale)continue;
                float angle=phase+age*(1.5f+Noise(i,5)*3);
                float width=(6+Noise(i,6)*6)*scale;
                float height=(10+Noise(i,7)*7)*scale;
                float flip=.15f+.85f*Mathf.Abs(Mathf.Cos(age*5+phase));
                Vector2 right=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*width*flip*.5f;
                Vector2 up=new Vector2(-Mathf.Sin(angle),Mathf.Cos(angle))*height*.5f;
                Vector2 center=new Vector2(x,y);
                Color tint=Palette[i%Palette.Length];
                // Keep the central result artwork legible even in text fallback mode.
                tint.a=fade*(Mathf.Abs(x-area.center.x)<area.width*.25f && Mathf.Abs(y-area.center.y)<area.height*.18f ? .25f : .85f);
                for(int n=0;n<4;n++)
                {
                    var v=UIVertex.simpleVert;
                    v.position=center+right*(n==0||n==3?-1:1)+up*(n<2?-1:1);
                    v.color=tint;
                    quad[n]=v;
                }
                vh.AddUIVertexQuad(quad);
            }
        }
    }
}

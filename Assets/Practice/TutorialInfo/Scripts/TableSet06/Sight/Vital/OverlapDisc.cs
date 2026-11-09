using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // UI mesh avoids texture assets and remains crisp when the screen is resized.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OverlapDisc : MaskableGraphic
    {
        static Sprite panelSprite;
        public static Sprite PanelSprite
        {
            get
            {
                if (panelSprite) return panelSprite;
                const int size=64;
                const float corner=18;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
                texture.name="Overlap Rounded Panel";
                texture.wrapMode=TextureWrapMode.Clamp;
                texture.filterMode=FilterMode.Bilinear;
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
                    var p=new Vector2(x+.5f-size*.5f,y+.5f-size*.5f);
                    var q=new Vector2(Mathf.Max(Mathf.Abs(p.x)-(size*.5f-corner),0),
                        Mathf.Max(Mathf.Abs(p.y)-(size*.5f-corner),0));
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(corner-q.magnitude));
                }
                texture.SetPixels(pixels);texture.Apply(false,true);
                panelSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,
                    SpriteMeshType.FullRect,new Vector4(corner,corner,corner,corner));
                return panelSprite;
            }
        }

        static Texture2D ballAtlas;
        public override Texture mainTexture
        {
            get
            {
                if (!ballAtlas) ballAtlas=Resources.Load<Texture2D>("Gameplay/AimOverlapBalls");
                return ballAtlas ? ballAtlas : Texture2D.whiteTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect=GetPixelAdjustedRect();
            Vector2 centre=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            if(radius<=0)return;
            // The approved art supplies the highlights and rim, without vertex tinting.
            // Use a circular mesh to exclude the generated atlas's stray outer pixels.
            float atlasX=color.g<.5f ? 377f : color.b<.5f ? 1796f : 1084f;
            Vector2 atlasCentre=new Vector2(atlasX/2172f,1f-350f/724f);
            Vector2 atlasRadius=new Vector2(290f/2172f,290f/724f);
            Color tint=new Color(1,1,1,color.a);
            mesh.AddVert(centre,tint,atlasCentre);
            const int segments=128;
            for(int i=0;i<=segments;i++) {
                float angle=2*Mathf.PI*i/segments;
                var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(centre+n*(radius-.5f),tint,atlasCentre+Vector2.Scale(n,atlasRadius));
                if(i>0)mesh.AddTriangle(0,i,i+1);
            }
            int edge=mesh.currentVertCount;
            tint.a=0;
            for(int i=0;i<=segments;i++) {
                float angle=2*Mathf.PI*i/segments;
                var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(centre+n*radius,tint,atlasCentre+Vector2.Scale(n,atlasRadius));
                if(i>0) {
                    mesh.AddTriangle(i,edge+i-1,edge+i);
                    mesh.AddTriangle(i,edge+i,i+1);
                }
            }
        }
    }
}

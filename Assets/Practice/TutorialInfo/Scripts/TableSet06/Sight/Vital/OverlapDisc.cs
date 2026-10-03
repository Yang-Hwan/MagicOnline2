using UnityEngine;
using UnityEngine.UI;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // UI mesh avoids texture assets and remains crisp when the screen is resized.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OverlapDisc : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();Vector2 centre=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            mesh.AddVert(centre,color,Vector2.zero);
            const int segments=64;
            for(int i=0;i<=segments;i++) {
                float a=2*Mathf.PI*i/segments;
                var p=centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                mesh.AddVert(p,color,Vector2.zero);
                if(i>0)mesh.AddTriangle(0,i,i+1);
            }
            int start=mesh.currentVertCount;
            for(int i=0;i<=segments;i++) {
                float a=2*Mathf.PI*i/segments;var n=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                mesh.AddVert(centre+n*radius,Color.white,Vector2.zero);
                mesh.AddVert(centre+n*Mathf.Max(0,radius-1.5f),Color.white,Vector2.zero);
                if(i>0) {int p=start+2*i;mesh.AddTriangle(p-2,p-1,p);mesh.AddTriangle(p-1,p+1,p);}
            }
        }
    }
}

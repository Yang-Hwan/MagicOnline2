using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
namespace Assets.Scripts.Sight.Surface.Consist
{
    public sealed class PlayerPageArt:MonoBehaviour
    {
        Texture2D owned;
        public void Load(string key)
        {
            if(string.IsNullOrWhiteSpace(key))return;
            if(!Uri.TryCreate(key,UriKind.Absolute,out var uri)){Fit(Assets.Scripts.Often.DailyCueArt.Load(key));return;}
            if(Application.isPlaying && (uri.Scheme=="https" || uri.Scheme=="http" && uri.IsLoopback))StartCoroutine(Download(key));
        }
        IEnumerator Download(string key)
        {
            using(var request=UnityWebRequestTexture.GetTexture(key)){
                request.timeout=12;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)yield break;
                owned=DownloadHandlerTexture.GetContent(request);Fit(owned);
            }
        }
        void Fit(Texture2D texture)
        {
            if(!texture)return;var image=GetComponent<RawImage>();var rect=image.rectTransform;var box=rect.sizeDelta;
            float scale=Mathf.Min(box.x/texture.width,box.y/texture.height);var size=new Vector2(texture.width,texture.height)*scale;
            rect.anchoredPosition+=Vector2.Scale((box-size)*.5f,new Vector2(1,-1));rect.sizeDelta=size;image.texture=texture;image.color=Color.white;
        }
        void OnDestroy(){if(owned){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerPencilGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;var c=r.center;float size=Mathf.Min(r.width,r.height);
            Vector2 a=c+new Vector2(-.2f,-.2f)*size,b=c+new Vector2(.22f,.22f)*size,n=new Vector2(-.07f,.07f)*size;
            void V(Vector2 p){var v=UIVertex.simpleVert;v.position=p;v.color=color;vh.AddVert(v);}
            V(a-n);V(b-n);V(b+n);V(a+n);vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
            V(a-n);V(a+n);V(c+new Vector2(-.34f,-.34f)*size);vh.AddTriangle(4,5,6);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerBrushGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Vector2[] shape={new Vector2(0,.13f),new Vector2(.08f,.38f),new Vector2(.02f,.42f),new Vector2(.13f,.88f),new Vector2(.24f,.78f),new Vector2(.29f,.98f),new Vector2(.66f,.88f),new Vector2(.91f,1),new Vector2(.85f,.77f),new Vector2(1,.8f),new Vector2(.92f,.56f),new Vector2(.98f,.51f),new Vector2(.81f,.28f),new Vector2(.91f,.18f),new Vector2(.61f,.12f),new Vector2(.29f,0),new Vector2(.3f,.14f)};
            var v=UIVertex.simpleVert;v.color=color;v.position=r.center;vh.AddVert(v);
            foreach(var p in shape){v.position=new Vector2(r.xMin+p.x*r.width,r.yMin+p.y*r.height);vh.AddVert(v);}
            for(int i=0;i<shape.Length;i++)vh.AddTriangle(0,i+1,(i+1)%shape.Length+1);
        }
    }
}

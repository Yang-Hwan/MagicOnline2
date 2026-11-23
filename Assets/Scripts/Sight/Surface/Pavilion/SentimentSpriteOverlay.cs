using System;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Projects the hammer's existing animation into a non-interactive HUD overlay.
    public sealed class SentimentSpriteOverlay : MonoBehaviour
    {
        struct Entry {public SpriteRenderer source;public Image image;public bool clipped;}
        Entry[] entries;
        RectTransform screen,clip;
        Transform cloth;
        Camera camera;
        public Canvas Canvas {get;private set;}
        public void SetCamera(Camera value){camera=value;}
        public void Initialize(Transform table,SpriteRenderer[] sources)
        {
            cloth=table;camera=Camera.main;
            var go=new GameObject("Hammer above HUD",typeof(RectTransform),typeof(Canvas));go.layer=table.gameObject.layer;go.transform.SetParent(transform,false);
            Canvas=go.GetComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceOverlay;Canvas.overrideSorting=true;Canvas.sortingOrder=32767;screen=(RectTransform)go.transform;
            var region=new GameObject("Cloth region",typeof(RectTransform),typeof(RectMask2D));region.layer=table.gameObject.layer;region.transform.SetParent(screen,false);clip=(RectTransform)region.transform;
            Array.Sort(sources,(a,b)=>a.sortingOrder.CompareTo(b.sortingOrder));entries=new Entry[sources.Length];
            for(int i=0;i<sources.Length;i++){
                var source=sources[i];bool masked=source.maskInteraction!=SpriteMaskInteraction.None;
                var imageGo=new GameObject(source.name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));imageGo.layer=table.gameObject.layer;imageGo.transform.SetParent(masked?clip:screen,false);
                var image=imageGo.GetComponent<Image>();image.raycastTarget=false;image.enabled=false;source.forceRenderingOff=true;
                entries[i]=new Entry{source=source,image=image,clipped=masked};
            }
        }
        public void Sync()
        {
            if(!camera)camera=Camera.main;if(!camera||entries==null)return;
            Vector2 extent=screen.rect.size,min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<4;i++){
                var p=camera.WorldToViewportPoint(cloth.TransformPoint(new Vector3(i%2==0?-.5f:.5f,0,i<2?-.5f:.5f)));
                Vector2 pixel=Vector2.Scale(new Vector2(p.x-.5f,p.y-.5f),extent);min=Vector2.Min(min,pixel);max=Vector2.Max(max,pixel);
            }
            clip.anchoredPosition=(min+max)*.5f;clip.sizeDelta=max-min;
            foreach(var entry in entries){
                var r=entry.source;var image=entry.image;image.enabled=r&&r.enabled&&r.gameObject.activeInHierarchy&&r.sprite&&r.color.a>.001f;if(!image.enabled)continue;
                var bounds=r.sprite.bounds;var t=r.transform;
                var center=camera.WorldToViewportPoint(t.TransformPoint(bounds.center));if(center.z<=0){image.enabled=false;continue;}
                var dx=camera.WorldToViewportPoint(t.TransformPoint(bounds.center+Vector3.right*bounds.extents.x))-camera.WorldToViewportPoint(t.TransformPoint(bounds.center-Vector3.right*bounds.extents.x));
                var dy=camera.WorldToViewportPoint(t.TransformPoint(bounds.center+Vector3.up*bounds.extents.y))-camera.WorldToViewportPoint(t.TransformPoint(bounds.center-Vector3.up*bounds.extents.y));
                Vector2 x=Vector2.Scale(new Vector2(dx.x,dx.y),extent),y=Vector2.Scale(new Vector2(dy.x,dy.y),extent);
                var rect=image.rectTransform;rect.anchoredPosition=Vector2.Scale(new Vector2(center.x-.5f,center.y-.5f),extent)-(entry.clipped?clip.anchoredPosition:Vector2.zero);
                rect.sizeDelta=new Vector2(x.magnitude,y.magnitude);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(x.y,x.x)*Mathf.Rad2Deg);
                image.sprite=r.sprite;image.color=r.color;
            }
        }
    }
}

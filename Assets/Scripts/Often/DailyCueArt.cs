using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Generated cutouts retain their original transparent canvas on disk.
    // Cache a tightly framed display texture, shared by shop, locker and match rendering.
    public static class DailyCueArt
    {
        public const string Prefix="Gameplay/DailyCues20261126/";
        static readonly Dictionary<string,Texture2D> images=new Dictionary<string,Texture2D>();
        public static bool Contains(string key)=>key!=null&&key.StartsWith(Prefix,StringComparison.Ordinal);
        public static bool TryGetCached(string key,out Texture2D image)
        {
            image=null;
            return key!=null && images.TryGetValue(key,out image) && image;
        }
        public static Texture2D Load(string key)
        {
            if(!Contains(key))return Resources.Load<Texture2D>(key);
            if(images.TryGetValue(key,out var cached)&&cached)return cached;
            var source=Resources.Load<Texture2D>(key);if(!source)return null;
            var pixels=source.GetPixels32();int left=source.width,bottom=source.height,right=-1,top=-1;
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
                if(pixels[y*source.width+x].a>12){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
            if(right<left)return source;
            // Keep the striking tip on the centerline, including asymmetric ornament silhouettes.
            int tip=0,count=0;for(int y=bottom;y<=top;y++)for(int x=Math.Max(left,right-18);x<=right;x++)
                if(pixels[y*source.width+x].a>120){tip+=y;count++;}
            int center=count>0?tip/count:(top+bottom)/2;
            int half=Math.Max(top-center,center-bottom)+5,w=right-left+9,h=half*2+1;
            var output=new Color32[w*h];
            for(int y=0;y<h;y++){int sy=center-half+y;if(sy<0||sy>=source.height)continue;
                for(int x=0;x<w;x++){int sx=left-4+x;if(sx>=0&&sx<source.width)output[y*w+x]=pixels[sy*source.width+sx];}}
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name=key,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels32(output);texture.Apply(false,true);images[key]=texture;Resources.UnloadAsset(source);return texture;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){foreach(var image in images.Values)if(image){if(Application.isPlaying)UnityEngine.Object.Destroy(image);else UnityEngine.Object.DestroyImmediate(image);}images.Clear();}
    }
}

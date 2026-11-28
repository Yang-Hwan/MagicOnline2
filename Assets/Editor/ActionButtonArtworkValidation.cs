using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;
public static class ActionButtonArtworkValidation
{
    [MenuItem("Tools/Gameplay/Validate Action Button Artwork")]
    public static void Run(){
        RenderTexture rt=null;Texture2D sheet=null;GameObject host=null;var previous=RenderTexture.active;
        try {
            sheet=new Texture2D(486,158,TextureFormat.RGBA32,false);
            for(int i=0;i<3;i++){
                host=new GameObject("Artwork validation",typeof(RectTransform),typeof(Button));
                var image=GameplayActionButton.Apply(host,i);var sprite=image.sprite;
                if(!sprite||sprite.rect.width<=0)throw new Exception("Missing button "+i);
                var texture=sprite.texture;
                rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
                if(!image.material||ShaderUtil.ShaderHasError(image.material.shader))throw new Exception("Rounded icon shader error");
                RenderTexture.active=rt;GL.Clear(true,true,Color.clear);
                Graphics.Blit(texture,rt,image.material);RenderTexture.active=rt;
                var crop=new Texture2D((int)sprite.rect.width,(int)sprite.rect.height,TextureFormat.RGBA32,false);
                crop.ReadPixels(sprite.rect,0,0);crop.Apply();
                if(crop.GetPixel(0,0).a>.01f||crop.GetPixel(crop.width-1,crop.height-1).a>.01f||crop.GetPixel(crop.width/2,crop.height/2).a<.9f)throw new Exception("Button corner alpha mask failed: "+i);
                // Fixed source import dimensions are part of the artwork contract.
                if(crop.width!=162||crop.height!=158)throw new Exception("Unexpected imported atlas dimensions: "+texture.width+"x"+texture.height);
                sheet.SetPixels(i*162,0,162,158,crop.GetPixels());
                UnityEngine.Object.DestroyImmediate(crop);RenderTexture.ReleaseTemporary(rt);rt=null;
                UnityEngine.Object.DestroyImmediate(host);host=null;
            }
            sheet.Apply();File.WriteAllBytes("Logs/action-buttons-artwork.png",sheet.EncodeToPNG());
            File.WriteAllText("Logs/action-buttons-artwork-result.txt","PASS: all three button corner alpha masks, opaque centers, shader compilation and imported sprite coordinates");
        }catch(Exception e){File.WriteAllText("Logs/action-buttons-artwork-result.txt",e.ToString());Debug.LogException(e);}
        finally{RenderTexture.active=previous;if(rt)RenderTexture.ReleaseTemporary(rt);if(sheet)UnityEngine.Object.DestroyImmediate(sheet);if(host)UnityEngine.Object.DestroyImmediate(host);}
    }
}

Shader "MagicCushion/PremiumCue"
{
 Properties { _MainTex("Artwork",2D)="white"{} [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=10 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend SrcAlpha [_DstBlend]
  ZWrite Off
  Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct vary {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   vary vert(app v){vary o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(vary i):SV_Target{return tex2D(_MainTex,i.uv)*i.color;}
   ENDCG
  }
 }
}

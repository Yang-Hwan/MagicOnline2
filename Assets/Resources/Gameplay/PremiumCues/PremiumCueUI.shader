Shader "MagicCushion/PremiumCueUI"
{
 Properties
 {
  [PerRendererData] _MainTex("Texture",2D)="white"{}
  _StencilComp("Stencil comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil operation",Float)=0
  _StencilWriteMask("Stencil write mask",Float)=255
  _StencilReadMask("Stencil read mask",Float)=255
  _ColorMask("Color mask",Float)=15
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha One
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct app {float4 vertex:POSITION;fixed4 color:COLOR;};
   struct vary {float4 vertex:SV_POSITION;fixed4 color:COLOR;float4 world:TEXCOORD0;};
   float4 _ClipRect;
   vary vert(app v){vary o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;return o;}
   fixed4 frag(vary i):SV_Target
   {
    fixed4 color=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    color.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    return color;
   }
   ENDCG
  }
 }
}

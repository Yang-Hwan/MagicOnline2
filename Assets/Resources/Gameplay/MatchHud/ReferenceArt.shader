Shader "MagicCushion/MatchHudReferenceArt"
{
 Properties
 {
  [PerRendererData] _MainTex("Original concept art",2D)="white"{}
  _StencilComp("Stencil comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil operation",Float)=0
  _StencilWriteMask("Stencil write mask",Float)=255
  _StencilReadMask("Stencil read mask",Float)=255
  _ColorMask("Color mask",Float)=15
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct app {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 shape:TEXCOORD1;};
   struct vary {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 shape:TEXCOORD1;float4 world:TEXCOORD2;};
   sampler2D _MainTex;float4 _ClipRect;
   vary vert(app v){vary o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.shape=v.shape;return o;}
   fixed4 frag(vary i):SV_Target
   {
    fixed4 tex=tex2D(_MainTex,i.uv);
    // Key only the dark concept-sheet backdrop; retain the exact original gold/chrome.
    float peak=max(tex.r,max(tex.g,tex.b));
    tex.a*=smoothstep(.11,.24,peak);
    if(i.shape.z>.5)
    {
     float d=length((i.shape.xy-.5)*2);
     float aa=max(fwidth(d),.015);
     tex.a*=1-smoothstep(1-aa,1+aa,d);
    }
    tex*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    tex.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    return tex;
   }
   ENDCG
  }
 }
}

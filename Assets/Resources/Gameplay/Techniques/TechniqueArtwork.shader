Shader "UI/Three Cushion Technique Artwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Reveal ("Brush reveal", Range(0,1)) = 1
        _Erase ("Brush erase", Range(0,1)) = 0
        _Blur ("Softness in pixels", Range(0,12)) = 0
        _UVBounds ("Sprite UV bounds", Vector) = (0,0,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;};
            sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;float4 _ClipRect,_UVBounds;
            float _Reveal,_Erase,_Blur;
            v2f vert(appdata v){v2f o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            fixed4 Sample(float2 uv)
            {
                fixed4 c=tex2D(_MainTex,clamp(uv,_UVBounds.xy,_UVBounds.zw));
                c.rgb*=c.a;return c;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 d=_MainTex_TexelSize.xy*_Blur;
                fixed4 c=Sample(i.uv)*.28;
                c+=(Sample(i.uv+float2(d.x,0))+Sample(i.uv-float2(d.x,0))+Sample(i.uv+float2(0,d.y))+Sample(i.uv-float2(0,d.y)))*.12;
                c+=(Sample(i.uv+d)+Sample(i.uv-d)+Sample(i.uv+float2(d.x,-d.y))+Sample(i.uv+float2(-d.x,d.y)))*.06;
                c.rgb/=max(.0001,c.a);
                float u=(i.uv.x-_UVBounds.x)/max(.0001,_UVBounds.z-_UVBounds.x);
                c*=i.color;c.a*=saturate((_Reveal-u)*90+1)*saturate((u-_Erase)*90+1);
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}

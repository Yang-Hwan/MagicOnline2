Shader "UI/SentimentHaloMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UVRect ("Atlas Cell", Vector) = (0,0,1,1)
        _InnerRadius ("Clear Center", Float) = 0.05
        _InnerFeather ("Inner Fade", Float) = 0.055
        _OuterRadius ("Outer Radius", Float) = 0.48
        _OuterFeather ("Outer Fade", Float) = 0.10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Output {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;fixed4 _Color;float4 _UVRect;
            float _InnerRadius,_InnerFeather,_OuterRadius,_OuterFeather;
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
            fixed4 frag(Output i):SV_Target
            {
                fixed4 color=tex2D(_MainTex,i.uv)*i.color;
                float2 local=(i.uv-_UVRect.xy)/_UVRect.zw;
                float radius=length(local-0.5);
                color.a*=smoothstep(_InnerRadius,_InnerRadius+_InnerFeather,radius)*(1-smoothstep(_OuterRadius-_OuterFeather,_OuterRadius,radius));
                return color;
            }
            ENDCG
        }
    }
}

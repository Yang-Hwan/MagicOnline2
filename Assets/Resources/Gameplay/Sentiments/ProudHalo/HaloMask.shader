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
        _RayVariation ("Ray Length Variation", Range(0,1)) = 0
        _CenterCompression ("Artwork Center Compression", Range(0,0.3)) = 0
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
            float _InnerRadius,_InnerFeather,_OuterRadius,_OuterFeather,_RayVariation,_CenterCompression;
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
            fixed4 frag(Output i):SV_Target
            {
                float2 local=(i.uv-_UVRect.xy)/_UVRect.zw;
                float2 offset=local-0.5;
                float angle=atan2(offset.y,offset.x);
                // Periodic angular variation keeps the seam closed and the rays stable while rotating.
                float wave=0.5+0.24*sin(angle*5+0.7)+0.16*sin(angle*9-1.2)+0.10*cos(angle*13+2.1);
                float scale=lerp(1,0.28+0.72*saturate(wave),_RayVariation);
                float radius=length(offset);
                // Compress the artwork's own empty center independently of the outer ray reach.
                float sampleRadius=radius/scale;
                sampleRadius+=_CenterCompression*saturate(1-sampleRadius/_OuterRadius);
                float2 sampleLocal=0.5+offset/max(radius,0.00001)*sampleRadius;
                fixed4 color=tex2D(_MainTex,(_UVRect.xy+saturate(sampleLocal)*_UVRect.zw))*i.color;
                color.a*=smoothstep(_InnerRadius,_InnerRadius+_InnerFeather,radius)*(1-smoothstep(_OuterRadius-_OuterFeather,_OuterRadius,radius/scale));
                return color;
            }
            ENDCG
        }
    }
}

Shader "MagicCushion/DoubleTapCue"
{
    Properties
    {
        _MainTex ("Cue artwork", 2D) = "white" {}
        _Charge ("Pull strength", Range(0,1)) = 0
        _FlashStart ("Impact time", Float) = -100
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        AlphaToMask On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float _Charge, _FlashStart;
            Varyings vert(Vertex input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 ink = tex2D(_MainTex, input.uv);
                clip(ink.a - 0.25);
                float u = input.uv.x; // Butt at 1, striking tip at 0.
                float amber = saturate((ink.r - ink.b) * 4) * smoothstep(0.38, 0.52, u);
                float age = (_Time.y - _FlashStart) / 0.22;
                float beam = (1 - smoothstep(0.015, 0.14, abs(u - (1 - age)))) * step(0, age) * step(age, 1.15);
                ink.rgb += amber * _Charge * half3(1.1, 0.57, 0.12) + beam * half3(1.25, 0.92, 0.52);
                return ink;
            }
            ENDCG
        }
    }
}

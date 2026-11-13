Shader "MagicCushion/ShopCue"
{
    Properties {
        _MainTex ("Cue illustration", 2D) = "white" {}
        _CueAxis ("Mesh Z origin and length", Vector) = (0,1,0,0)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _CueAxis;
            struct Varyings { float4 pos : SV_POSITION; float u : TEXCOORD0; float3 normal : TEXCOORD1; };
            Varyings vert(appdata_base v) {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.u = saturate((v.vertex.z - _CueAxis.x) / _CueAxis.y);
                o.normal = v.normal;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target {
                // Read the illustrated shaft along the length of the original tapered mesh.
                float3 n = normalize(i.normal);
                float halfWidth = lerp(.035,.19,smoothstep(.03,.83,i.u));
                float2 uv = float2(lerp(.007,.981,i.u), .5 + n.x * halfWidth);
                return fixed4(tex2D(_MainTex,uv).rgb * (.88 + .12*n.y),1);
            }
            ENDCG
        }
    }
}

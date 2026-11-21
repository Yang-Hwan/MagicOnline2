Shader "Magic/ElasticControlSurface" {
 Properties { _MainTex("Texture",2D)="white"{} _Rubber("Rubber",Float)=0 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"} Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 float _Rubber;
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 fixed4 frag(v2f i):SV_Target {
 float2 uv=i.uv;float grain=hash(floor(uv*float2(140,850)))-.5;
 float edge=pow(saturate(1-abs(uv.x*2-1)),.35);
 float ridge=pow(saturate(sin(uv.x*38+sin(uv.y*12)*.7)),12);
 float sheen=exp(-pow((uv.x-.23)/.085,2));
 float lighting=lerp(.75+edge*.22+grain*.12,.48+edge*.42+ridge*.12+sheen*.26+grain*.09,_Rubber);
 return fixed4(i.color.rgb*lighting,i.color.a);
 }
 ENDCG }
 }
}

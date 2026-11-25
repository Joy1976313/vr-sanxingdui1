Shader "VR/GodRay"
{
    Properties
    {
        _Color ("Tint", Color) = (1.0, 0.88, 0.70, 1.0)
        _Intensity ("Intensity", Range(0, 2)) = 0.30
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 wn : TEXCOORD0;
                float3 vd : TEXCOORD1;
            };

            float4 _Color;
            float _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(wp, _WorldSpaceCameraPos);
                o.color.a *= saturate((d - 0.6) / 2.2);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.vd = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float soft = pow(saturate(abs(dot(normalize(i.wn), normalize(i.vd)))), 1.6);
                return fixed4(_Color.rgb * i.color.a * _Intensity * soft, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}

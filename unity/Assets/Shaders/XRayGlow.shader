// Through-wall glow for X-ray highlighted objects (NPCs, props).
Shader "LastSon/XRayGlow"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 0.5, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" }
        ZTest Always
        ZWrite Off
        Blend SrcAlpha One
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float rim = 1.0 - saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                float pulse = 0.85 + 0.15 * sin(_Time.y * 6.0);
                return fixed4(_Color.rgb * (0.45 + rim) * pulse, saturate(0.35 + 0.65 * rim));
            }
            ENDCG
        }
    }
}

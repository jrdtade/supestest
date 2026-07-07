// Translucent shell for buildings while X-ray vision is active.
Shader "LastSon/XRayBuilding"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.6, 1, 0.1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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
                return fixed4(_Color.rgb + rim * 0.25, _Color.a + rim * 0.22);
            }
            ENDCG
        }
    }
}

Shader "RadVis/a_diamond_glow"
{
    Properties
    {
        _Color ("Diamond Color", Color) = (0.30, 0.72, 1.0, 1.0)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.22
        _OutlineWidth ("Outline Width", Range(0.005, 0.4)) = 0.07
        _OutlineIntensity ("Outline Intensity", Range(0, 4)) = 1.6
        _GlowWidth ("Glow Width", Range(0.01, 1)) = 0.45
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1.1
        _GlowFalloff ("Glow Falloff", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        // Additive, because the glow is a bloom stand-in. On the black optical render
        // target additive is what makes overlapping glow read as brightness.
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _FillAlpha;
            float _OutlineWidth;
            float _OutlineIntensity;
            float _GlowWidth;
            float _GlowIntensity;
            float _GlowFalloff;

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = (input.uv - 0.5) * 2.0;
                float distanceField = abs(centered.x) + abs(centered.y);

                float edgeSoftness = max(fwidth(distanceField), 0.0001);

                float fillMask = 1.0 - smoothstep(1.0 - edgeSoftness, 1.0 + edgeSoftness, distanceField);

                float outlineInner = 1.0 - _OutlineWidth;
                float outline =
                    smoothstep(outlineInner - edgeSoftness, outlineInner + edgeSoftness, distanceField) *
                    (1.0 - smoothstep(1.0 - edgeSoftness, 1.0 + edgeSoftness, distanceField));

                float glowDistance = saturate((distanceField - 1.0) / _GlowWidth);
                float glow = pow(1.0 - glowDistance, _GlowFalloff) * step(1.0, distanceField);

                float intensity =
                    fillMask * _FillAlpha +
                    outline * _OutlineIntensity +
                    glow * _GlowIntensity;

                return fixed4(_Color.rgb, saturate(intensity * _Color.a));
            }
            ENDCG
        }
    }

    FallBack Off
}

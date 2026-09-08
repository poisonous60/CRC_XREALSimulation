Shader "RadVis/a_11_source_cloud"
{
    Properties
    {
        _Level ("Level", Range(0, 1)) = 0.5
        _StopA ("Ramp Stop 1 (low)", Color) = (0.18, 0.85, 0.30, 1)
        _StopB ("Ramp Stop 2", Color) = (0.85, 0.95, 0.15, 1)
        _StopC ("Ramp Stop 3", Color) = (0.98, 0.60, 0.10, 1)
        _StopD ("Ramp Stop 4 (high)", Color) = (0.75, 0.10, 0.05, 1)
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.34
        _CoreRadius ("Core Radius", Range(0, 1)) = 0.18
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.85
        _EdgeRing ("Edge Ring", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
            float _Level;
            half4 _StopA;
            half4 _StopB;
            half4 _StopC;
            half4 _StopD;
            float _CoreAlpha;
            float _CoreRadius;
            float _EdgeSoftness;
            float _EdgeRing;
            CBUFFER_END

            half3 RampColor(float level)
            {
                float scaled = saturate(level) * 3.0;
                half3 lowPair = lerp(_StopA.rgb, _StopB.rgb, saturate(scaled));
                half3 midPair = lerp(lowPair, _StopC.rgb, saturate(scaled - 1.0));
                return lerp(midPair, _StopD.rgb, saturate(scaled - 2.0));
            }

            Varyings Vert(AppData input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float radius = length((input.uv - 0.5) * 2.0);

                // The blob's size stands for how poorly four readings pin the source down,
                // so the edge is a long fade rather than a boundary anyone could measure.
                float falloff = 1.0 - smoothstep(_CoreRadius, _CoreRadius + _EdgeSoftness, radius);
                float body = pow(saturate(falloff), 1.6);

                float ring = _EdgeRing *
                    (1.0 - smoothstep(0.0, 0.06, abs(radius - 1.0 + 0.06)));

                half3 rampColor = RampColor(_Level);

                float opacity = saturate(body * _CoreAlpha + ring * _CoreAlpha);
                return half4(rampColor, opacity * step(radius, 1.0));
            }
            ENDHLSL
        }
    }

    FallBack Off
}

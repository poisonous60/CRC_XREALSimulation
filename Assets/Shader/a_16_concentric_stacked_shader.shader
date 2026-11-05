Shader "RadVis/a_16_concentric_stacked"
{
    Properties
    {
        _Color ("Fade (alpha only)", Color) = (1, 1, 1, 1)
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_RINGS 8

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
            half4 _Color;
            CBUFFER_END

            // Set every frame through a property block by ConcentricRingMarkerVisual.
            // Slots 0-7 hold the incoming band, 8-15 the outgoing one, each sorted small to large,
            // radii in quad units where 1 is the quad edge.
            float _RingRadius[MAX_RINGS * 2];
            float4 _RingColor[MAX_RINGS * 2];
            float4 _RingCounts;
            float4 _SetWeights;

            Varyings Vert(AppData input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.uv = input.uv;
                return output;
            }

            float Coverage(float radius, float distanceField, float softness)
            {
                return 1.0 - smoothstep(radius - softness, radius + softness, distanceField);
            }

            float4 CompositeSet(int offset, float count, float distanceField, float softness)
            {
                float4 result = 0;

                [unroll]
                for (int i = MAX_RINGS - 1; i >= 0; i--)
                {
                    if (i < count)
                    {
                        float4 ringColor = _RingColor[offset + i];
                        float alpha = ringColor.a * Coverage(_RingRadius[offset + i], distanceField, softness);
                        result.rgb = ringColor.rgb * alpha + result.rgb * (1.0 - alpha);
                        result.a = alpha + result.a * (1.0 - alpha);
                    }
                }

                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = (input.uv - 0.5) * 2.0;
                float distanceField = length(centered);

                // Softens each edge by one pixel. An antialias, not an outline.
                float softness = max(fwidth(distanceField), 0.0001);

                float4 incoming = CompositeSet(0, _RingCounts.x, distanceField, softness) * _SetWeights.x;
                float4 outgoing = CompositeSet(MAX_RINGS, _RingCounts.y, distanceField, softness) * _SetWeights.y;
                float4 color = incoming + outgoing * (1.0 - incoming.a);

                // The manager fades the marker in and out through _Color's alpha; its hue is the
                // CPS band color, which this look replaces with the rule's own colors.
                return half4(color * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

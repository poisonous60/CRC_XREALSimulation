Shader "RadVis/a_13_circle"
{
    Properties
    {
        _Color ("Circle Color", Color) = (0.30, 0.72, 1.0, 1.0)
        _BloomBoost ("Bloom Boost", Range(1, 8)) = 1
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
            half4 _Color;
            float _BloomBoost;
            CBUFFER_END

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

                float2 centered = (input.uv - 0.5) * 2.0;
                float distanceField = length(centered);

                // Softens the rim by one pixel. An antialias, not an outline.
                float edgeSoftness = max(fwidth(distanceField), 0.0001);
                float circle = 1.0 - smoothstep(1.0 - edgeSoftness, 1.0 + edgeSoftness, distanceField);

                // The halo is URP Bloom's job now, so the disc only has to write a color
                // above the Bloom threshold. Drawing the glow here clipped it to the quad.
                return half4(_Color.rgb * _BloomBoost, circle * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

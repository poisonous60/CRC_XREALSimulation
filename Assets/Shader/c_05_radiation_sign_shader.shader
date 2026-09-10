Shader "RadVis/c_05_radiation_sign"
{
    Properties
    {
        _Color ("Sign Color", Color) = (1.0, 0.85, 0.1, 1.0)
        _BloomBoost ("Bloom Boost", Range(1, 8)) = 1
        _RotationDegrees ("Rotation (deg)", Range(0, 120)) = 0
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

            // ISO 361 proportions with the blade tip normalised to 1: the centre dot is one
            // fifth of the tip radius and the blades start at one and a half dot radii.
            #define SIGN_DOT_RADIUS 0.2
            #define SIGN_BLADE_INNER_RADIUS 0.3
            #define SIGN_BLADE_HALF_ANGLE 0.5235988
            #define SIGN_SECTOR 2.0943951

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
            float _RotationDegrees;
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
                float radius = length(centered);

                float angle = atan2(centered.y, centered.x) - radians(90.0 + _RotationDegrees);
                angle -= SIGN_SECTOR * round(angle / SIGN_SECTOR);

                // The angular test becomes an arc length so its antialias width matches the radial one.
                float bladeSides = (abs(angle) - SIGN_BLADE_HALF_ANGLE) * radius;
                float bladeEnds = max(SIGN_BLADE_INNER_RADIUS - radius, radius - 1.0);
                float blade = max(bladeSides, bladeEnds);
                float centreDot = radius - SIGN_DOT_RADIUS;
                float field = min(blade, centreDot);

                float softness = max(fwidth(field), 0.0001);
                float mask = 1.0 - smoothstep(-softness, softness, field);

                return half4(_Color.rgb * _BloomBoost, mask * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

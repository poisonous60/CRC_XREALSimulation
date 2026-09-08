Shader "RadVis/DetectorTransparent"
{
    Properties
    {
        _Color ("Detector Volume Color", Color) = (0.65, 0.65, 0.65, 0.18)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
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
        Cull [_Cull]
        ZWrite Off
        ZTest LEqual
        // Match the XREAL SDK's transparent-plane convention. On the black
        // optical render target, lower alpha becomes lower emitted brightness.
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
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Cull;
            CBUFFER_END

            Varyings Vert(AppData input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.worldNormal = TransformObjectToWorldNormal(input.normal);
                output.worldPosition = TransformObjectToWorld(input.vertex.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normal = normalize(input.worldNormal);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.worldPosition));
                float normalFacing = saturate(abs(dot(normal, viewDirection)));

                // A single soft volume with no contour, grid, or silhouette line.
                // The center is slightly denser than the edge so its size remains
                // readable while large falloff shells stay out of the user's way.
                float volumeShape = lerp(0.38, 1.0, smoothstep(0.0, 1.0, normalFacing));
                float opacity = saturate(_Color.a) * volumeShape;
                return half4(_Color.rgb, opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

Shader "RadVis/a_07_plane_aligned_bands"
{
    Properties
    {
        _Color ("Detector Response Color", Color) = (0.65, 0.65, 0.65, 0.9)
        _SurfaceBrightness ("Surface Fill Brightness", Range(0.02, 0.35)) = 0.12
        _PrimaryBandWidth ("Placement Plane Band Half Width", Range(0.003, 0.05)) = 0.012
        _CalibrationBandWidth ("Calibration Band Half Width", Range(0.002, 0.04)) = 0.006
        _CalibrationBandBrightness ("Calibration Band Brightness", Range(0.1, 1.0)) = 0.62
        _RimWidth ("Silhouette Line Width", Range(0.01, 0.15)) = 0.032
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
        Cull Back
        ZWrite Off
        ZTest LEqual
        // XREAL optical displays express transparency most reliably through
        // emitted brightness. Keep explicit replacement blending, then draw the
        // shell dimly and the measurement contours brightly in the detector color.
        Blend One Zero

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
                float3 localNormal : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _SurfaceBrightness;
            float _PrimaryBandWidth;
            float _CalibrationBandWidth;
            float _CalibrationBandBrightness;
            float _RimWidth;
            CBUFFER_END

            float BandCoverage(float distanceToBand, float halfWidth)
            {
                float antiAliasWidth = max(fwidth(distanceToBand) * 0.75, 0.0001);
                return 1.0 - smoothstep(
                    max(halfWidth - antiAliasWidth, 0.0),
                    halfWidth + antiAliasWidth,
                    distanceToBand);
            }

            Varyings Vert(AppData input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.localNormal = normalize(input.normal);
                output.worldNormal = TransformObjectToWorldNormal(input.normal);
                output.worldPosition = TransformObjectToWorld(input.vertex.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normal = normalize(input.worldNormal);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.worldPosition));
                float localPlaneAxis = normalize(input.localNormal).y;

                // Local Y is fixed to the detected plane normal at placement.
                // These parallel response bands therefore preserve the original
                // horizontal/vertical placement orientation instead of following
                // the viewer. The broad equator represents the placement plane;
                // the unequal calibration latitudes make its normal axis readable.
                float primaryBand = BandCoverage(abs(localPlaneAxis), _PrimaryBandWidth);
                float nearCalibrationBands = BandCoverage(
                    abs(abs(localPlaneAxis) - 0.34),
                    _CalibrationBandWidth);
                float polarCalibrationBands = BandCoverage(
                    abs(abs(localPlaneAxis) - 0.68),
                    _CalibrationBandWidth * 0.82);
                float orientationBands = max(
                    primaryBand,
                    max(nearCalibrationBands, polarCalibrationBands) *
                        saturate(_CalibrationBandBrightness));

                // The silhouette is view-dependent only to keep the sphere's
                // physical diameter legible; the directional bands above are not.
                float normalFacing = saturate(abs(dot(normal, viewDirection)));
                float rimAntiAlias = max(fwidth(normalFacing), 0.0001);
                float silhouette = 1.0 - smoothstep(
                    max(_RimWidth - rimAntiAlias, 0.0),
                    _RimWidth + rimAntiAlias,
                    normalFacing);

                float lineCoverage = saturate(max(orientationBands, silhouette));

                // The subdued solid shell gives the detector volume while the
                // fixed response bands provide a restrained instrument-like scale.
                float shellShape = lerp(0.68, 1.0, normalFacing);
                float surfaceBrightness = saturate(_SurfaceBrightness) * shellShape;
                float lineBrightness = max(surfaceBrightness, saturate(_Color.a));
                float brightness = lerp(surfaceBrightness, lineBrightness, lineCoverage);
                return half4(_Color.rgb * brightness, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

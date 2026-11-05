Shader "RadVis/a_17_concentric_spheres"
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
        // Back faces cover the sphere's disc from outside and the whole view from inside, so every
        // pixel that can see one of the spheres runs once, whichever side of them the head is on.
        Cull Front
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

            #define MAX_SPHERES 8

            struct AppData
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centerWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            // Set every frame through a property block by ConcentricSphereMarkerVisual.
            // Slots 0-7 hold the incoming band, 8-15 the outgoing one, each sorted small to large,
            // radii in world meters. Inside is 1 for a sphere the head stands in; grid weight is the
            // share of that sphere's inside grid shown. _Grid is latitude steps, longitude steps,
            // line width in pixels and near fade meters.
            float _SphereRadius[MAX_SPHERES * 2];
            float4 _SphereColor[MAX_SPHERES * 2];
            float4 _SphereInsideColor[MAX_SPHERES * 2];
            float _SphereInside[MAX_SPHERES * 2];
            float _SphereGridWeight[MAX_SPHERES * 2];
            float4 _SphereCounts;
            float4 _SetWeights;
            float4 _Grid;

            Varyings Vert(AppData input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.positionWS = TransformObjectToWorld(input.vertex.xyz);
                output.centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                return output;
            }

            float Lines(float coordinate, float unitsPerPixel)
            {
                float distancePixels = abs(frac(coordinate + 0.5) - 0.5) / max(unitsPerPixel, 0.00001);
                return saturate(_Grid.z * 0.5 + 0.5 - distancePixels);
            }

            float Grid(float3 direction)
            {
                float latitude = (asin(clamp(direction.y, -1.0, 1.0)) / PI + 0.5) * _Grid.x;

                // atan2 has no answer exactly at a pole.
                float2 around = dot(direction.xz, direction.xz) > 0.0 ? direction.xz : float2(1.0, 0.0);
                float turns = atan2(around.y, around.x) / TWO_PI;
                float longitude = turns * _Grid.y;

                // atan2 jumps by a full turn on one meridian, so the width is also taken from a copy
                // that jumps on the opposite one, and the smaller of the two is the real one.
                float longitudeWidth = min(fwidth(longitude), fwidth(frac(turns) * _Grid.y));

                // Meridians crowd together toward the poles; they fade out before they merge into a solid cap.
                float cellPixels = 1.0 / max(longitudeWidth, 0.00001);
                float meridians = Lines(longitude, longitudeWidth) * saturate(cellPixels / (_Grid.z * 4.0) - 1.0);

                return max(Lines(latitude, fwidth(latitude)), meridians);
            }

            float4 CompositeSet(int offset, float count, float3 cameraWS, float3 centerWS, float3 ray,
                float along, float missSq, float miss, float softness)
            {
                float3 color = 0;
                float alpha = 0;
                float innerCoverage = 0;

                [unroll]
                for (int i = 0; i < MAX_SPHERES; i++)
                {
                    if (i < count)
                    {
                        float radius = _SphereRadius[offset + i];
                        float halfChord = sqrt(max(radius * radius - missSq, 0.0));

                        [branch]
                        if (_SphereInside[offset + i] < 0.5)
                        {
                            // Seen from outside, a sphere paints only the band between its edge and the
                            // next smaller sphere, so a smaller one keeps its own color inside or out.
                            float coverage = (1.0 - smoothstep(radius - softness, radius + softness, miss)) * step(along, 0.0);
                            float enter = -along - halfChord;
                            float nearFade = _Grid.w > 0.0 ? saturate(enter / _Grid.w) : 1.0;
                            float4 sphereColor = _SphereColor[offset + i];
                            float band = saturate(coverage - innerCoverage) * sphereColor.a * nearFade;
                            color += sphereColor.rgb * band;
                            alpha += band;
                            innerCoverage = max(innerCoverage, coverage);
                        }
                        else if (_SphereGridWeight[offset + i] > 0.001)
                        {
                            float3 exitPoint = cameraWS + ray * (halfChord - along);
                            float4 insideColor = _SphereInsideColor[offset + i];
                            float lines = Grid((exitPoint - centerWS) / max(radius, 0.0001))
                                * insideColor.a * _SphereGridWeight[offset + i] * (1.0 - innerCoverage);
                            color += insideColor.rgb * lines;
                            alpha += lines;
                        }
                    }
                }

                return float4(color, saturate(alpha));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 cameraWS = GetCameraPositionWS();
                float3 toCamera = cameraWS - input.centerWS;
                float3 ray = normalize(input.positionWS - cameraWS);
                float along = dot(ray, toCamera);
                float missSq = max(dot(toCamera, toCamera) - along * along, 0.0);

                // How close the view ray passes to the center. Each sphere's edge is where this equals its radius.
                float miss = sqrt(missSq);

                // Softens each edge by one pixel. An antialias, not an outline.
                float softness = max(fwidth(miss), 0.0001);

                float4 incoming = CompositeSet(0, _SphereCounts.x, cameraWS, input.centerWS, ray,
                    along, missSq, miss, softness) * _SetWeights.x;
                float4 outgoing = CompositeSet(MAX_SPHERES, _SphereCounts.y, cameraWS, input.centerWS, ray,
                    along, missSq, miss, softness) * _SetWeights.y;
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

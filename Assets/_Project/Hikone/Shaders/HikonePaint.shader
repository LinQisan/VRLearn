// The browser port's painted look (hikone-traffic-sim src/style.js) for the Quest: each material
// paints with one pigment and lays short brush dabs of a warm and a cool pigment over it in world
// space (anchored in the scene, so walking never slides the paint), lit by Lambert light; dark
// tones turn cool and a fine bristle grain shows up close and fades with distance (no shimmer in
// stereo). The brush texture is HK_T_Brush (HikonePaintAssets). Forward, single-pass instanced
// stereo, SRP Batcher and GPU instancing compatible; main-light shadows, per-pixel extra lights
// (the night lamps), fog.
Shader "VRLearn/HikonePaint"
{
    Properties
    {
        _BaseColor("Pigment", Color) = (0.73, 0.70, 0.67, 1)
        _Warm("Warm dabs", Color) = (0.76, 0.64, 0.49, 1)
        _Cool("Cool dabs", Color) = (0.45, 0.55, 0.72, 1)
        _DabStrength("Dab strength", Range(0, 1)) = 0.65
        [NoScaleOffset] _BrushTex("Brush (dab mask, dab pigment, grain)", 2D) = "gray" {}
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        _Paint("Paint finish", Range(0, 1)) = 1
        [Toggle] _VertexPigment("Use mesh pigments", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Warm;
            half4 _Cool;
            half _DabStrength;
            half4 _EmissionColor;
            half _Paint;
            half _VertexPigment;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BrushTex);
            SAMPLER(sampler_BrushTex);
            // global switch (0 unless set): flat pigments for the top-down scenario map
            half _HikonePaintOff;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 pigment : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 ambient : TEXCOORD2;
                half fog : TEXCOORD3;
                half3 pigment : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.ambient = SampleSH(o.normalWS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.pigment = lerp(half3(1, 1, 1), v.pigment.rgb, _VertexPigment);
                return o;
            }

            // the world plane facing the surface most: ground and roofs paint in xz, walls in xy / zy
            float2 PaintPlane(float3 positionWS, half3 n)
            {
                half3 a = abs(n);
                return a.y > max(a.x, a.z) ? positionWS.xz : (a.x > a.z ? positionWS.zy : positionWS.xy);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS);
                float2 p = PaintPlane(i.positionWS, n);
                half paint = _Paint * (1.0 - _HikonePaintOff);

                // dabs: warm/cool pigment strokes over the base pigment (style.js DAB_FRAGMENT)
                float2 q = float2(0.86 * p.x + 0.51 * p.y, -0.51 * p.x + 0.86 * p.y);
                float scale = abs(n.y) > 0.7 ? 2.3 : 3.7;
                half2 dab = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, q * float2(scale, scale * 1.35) / 32.0).rg;
                half3 pigment = lerp(_Cool.rgb, _Warm.rgb, dab.y);
                pigment = lerp(pigment, half3(0.71, 0.43, 0.39), smoothstep(0.84, 1.0, dab.y) * 0.20);
                half3 albedo = lerp(_BaseColor.rgb * i.pigment, pigment, dab.x * _DabStrength * paint);

                // Lambert light
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                half3 light = sun.color * (saturate(dot(n, sun.direction)) * sun.shadowAttenuation * sun.distanceAttenuation) + i.ambient;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light extra = GetAdditionalLight(lightIndex, i.positionWS);
                    light += extra.color * (saturate(dot(n, extra.direction)) * extra.distanceAttenuation * extra.shadowAttenuation);
                LIGHT_LOOP_END
                #endif
                half3 c = albedo * light + _EmissionColor.rgb;

                // finish: cool dark tones, bristle grain close up (style.js injectPaint)
                float2 g = float2(0.91 * p.x + 0.41 * p.y, -0.41 * p.x + 0.91 * p.y);
                half fibre = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, g * float2(34.0, 170.0) / 32.0).b - 0.5;
                half detail = 1.0 - smoothstep(0.008, 0.045, max(length(ddx(p)), length(ddy(p))));
                half value = dot(c, half3(0.2126, 0.7152, 0.0722));
                half3 cool = c * half3(0.88, 0.94, 1.10) + half3(0.022, 0.021, 0.042);
                half3 painted = lerp(c, cool, 1.0 - smoothstep(0.06, 0.36, value));
                painted *= 1.0 + fibre * 0.10 * detail;
                c = lerp(c, painted, paint);

                c = MixFog(c, i.fog);
                return half4(c, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 toLight = normalize(_LightPosition - positionWS);
                #else
                float3 toLight = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, toLight));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

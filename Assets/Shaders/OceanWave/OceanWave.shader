Shader "Custom/URP/OceanWave"
{
    Properties
    {
        // 그룹 구분은 OceanWaveShaderGUI.cs(CustomEditor)가 접고 펼 수 있는 폴드아웃으로 대체함.
        // 각 프로퍼티의 기능 설명(툴팁)도 그쪽에 정의되어 있음 — ShaderLab Properties는 [Tooltip]을 지원하지 않음.
        _WaveSpeed ("Global Wave Speed", Float) = 1.0
        _WaveRoughness ("Wave Distortion (Roughness)", Range(0, 2)) = 0.6
        _WaveHeightScale ("Wave Height Scale", Range(0, 5)) = 1.0
        _WaveSteepnessScale ("Wave Sharpness (Steepness)", Range(0, 1.0)) = 0.5
        [IntRange] _ActiveWaveCount ("Active Wave Count", Range(0, 6)) = 6

        _LocalWaveScale ("Local Wave Frequency Scale", Range(0.1, 5.0)) = 1.0
        _LocalHeightCorrection ("Local Height Correction", Range(0.01, 2.0)) = 0.2
        _NoiseSpaceScale ("Noise Mapping Tiling Bias", Range(0.1, 10.0)) = 1.0

        _ShallowColor ("Shallow Color", Color) = (0.1, 0.6, 0.7, 0.24)
        _DeepColor ("Deep Color", Color) = (0.05, 0.15, 0.3, 0.8)
        _DepthFadeDistance ("Depth Fade Distance", Float) = 5.0
        _DepthAlphaFade ("Depth Alpha Fade Power", Range(0.1, 5)) = 1.0

        _WallTopColor ("Wall Top Color", Color) = (0.1, 0.45, 0.55, 1.0)
        _WallBottomColor ("Wall Bottom Color", Color) = (0.02, 0.08, 0.18, 1.0)
        _WallDepthPower ("Wall Depth Gradient Power", Range(0.2, 4)) = 1.4
        _WallAlpha ("Wall Alpha (Outside)", Range(0, 1)) = 0.92
        _WallInnerAlpha ("Wall Inner Alpha (Inside)", Range(0, 1)) = 0.6

        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Shoreline Foam Distance", Float) = 1.5
        _CrestFoamThreshold ("Crest Foam Height Threshold", Range(-1, 3)) = 0.6
        _CrestFoamRange ("Crest Foam Smooth Range", Range(0.05, 1)) = 0.3

        [Toggle(_CAST_SHADOWS_ON)] _CastShadows ("Cast Shadows", Float) = 1

        _Smoothness ("Water Smoothness", Range(0, 1)) = 0.8
        _SpecularSharpness ("Specular Highlight Sharpness", Range(2, 20)) = 13.0
        _SpecularIntensity ("Specular Highlight Intensity", Range(0, 2)) = 0.3
        _FresnelPower ("Fresnel Power (Edge Reflection)", Range(1, 7)) = 5.0
        _NormalMap ("Detail Micro Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Bump Strength", Range(0, 1)) = 0.4
    }

    CustomEditor "OceanWaveShaderGUI"

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // ============================================================
        // Pass 1: ForwardLit — 수면 렌더링
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 screenUV   : TEXCOORD2;
                float  baseHeight : TEXCOORD3;
                float2 sampleXZ   : TEXCOORD4;
                float2 uv         : TEXCOORD5;
                float  waveMask   : TEXCOORD6;
            };

            #include "OceanProperties.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            #include "GerstnerWave.hlsl"
            #include "OceanSurface.hlsl"

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float waveMask = IN.color.r;
                float correctedHeightScale = _WaveHeightScale * _LocalHeightCorrection;

                float3 localPos, localNormal;
                ComputeOceanWavedVertex(
                    IN.positionOS.xyz, IN.normalOS, waveMask, _LocalWaveScale,
                    _Time.y * _WaveSpeed, _WaveRoughness, correctedHeightScale, _WaveSteepnessScale, (int)_ActiveWaveCount,
                    localPos, localNormal);

                float3 positionWS = TransformObjectToWorld(localPos);
                OUT.positionWS  = positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(localNormal);
                OUT.baseHeight  = IN.positionOS.y;
                OUT.sampleXZ    = IN.positionOS.xz * _LocalWaveScale;
                OUT.uv          = IN.uv;
                OUT.waveMask    = waveMask;

                float4 clipPos   = TransformWorldToHClip(positionWS);
                OUT.positionCS   = clipPos;
                float4 screenPos = ComputeScreenPos(clipPos);
                OUT.screenUV     = screenPos.xy / max(0.0001, screenPos.w);

                return OUT;
            }

            half4 frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                return OceanSurfaceFrag(IN, isFrontFace);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 2: ShadowCaster
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster
            #pragma shader_feature _CAST_SHADOWS_ON
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GerstnerWave.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            #include "OceanProperties.hlsl"

            // 이 패스에서는 노멀맵을 샘플링하지 않으므로 TEXTURE2D(_NormalMap) 바인딩을 두지 않음
            // (ForwardLit 패스에만 필요 — 여기 선언해봐야 쓰이지 않는 리소스 바인딩만 늘어남)

            float3 _LightDirection;
            float3 _LightPosition;
            float2 _ShadowBias;

            float3 OceanApplyShadowBias(float3 positionWS, float3 normalWS)
            {
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float invNdotL = 1.0 - saturate(dot(lightDir, normalWS));
                float scale    = invNdotL * _ShadowBias.y;
                positionWS = lightDir  * _ShadowBias.xxx + positionWS;
                positionWS = normalWS  * scale.xxx       + positionWS;
                return positionWS;
            }

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float waveMask = IN.color.r;
                float correctedHeightScale = _WaveHeightScale * _LocalHeightCorrection;

                float3 localPos, localNormal;
                ComputeOceanWavedVertex(
                    IN.positionOS.xyz, IN.normalOS, waveMask, _LocalWaveScale,
                    _Time.y * _WaveSpeed, _WaveRoughness, correctedHeightScale, _WaveSteepnessScale, (int)_ActiveWaveCount,
                    localPos, localNormal);

                float3 positionWS = TransformObjectToWorld(localPos);
                float3 normalWS   = TransformObjectToWorldNormal(localNormal);

                // 유니티 섀도우 바이어스 오프셋 적용
                positionWS = OceanApplyShadowBias(positionWS, normalWS);

                float4 positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                #ifndef _CAST_SHADOWS_ON
                clip(-1);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }
}

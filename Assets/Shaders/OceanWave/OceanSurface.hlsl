#ifndef OCEAN_SURFACE_INCLUDED
#define OCEAN_SURFACE_INCLUDED

// OceanWave.shader의 frag 로직을 분리한 파일.
// CBUFFER, TEXTURE2D, Varyings 등은 셰이더 측에서 먼저 선언되어야 함.
// (이 파일은 HLSLPROGRAM 블록 안에서 include할 것)

#include "GerstnerWave.hlsl"
#include "OceanLighting.hlsl"

float InlineFresnelEffect(float3 normal, float3 viewDir, float power)
{
    return pow(1.0 - saturate(dot(normal, viewDir)), power);
}

half4 OceanSurfaceFrag(Varyings IN, bool isFrontFace)
{
    // ────────────────────────────────────────────────────────
    // 측벽(단면) 처리
    // ────────────────────────────────────────────────────────
    if (IN.waveMask < 0.999)
    {
        float depth01 = saturate(1.0 - IN.uv.y);
        depth01 = pow(depth01, _WallDepthPower);

        float3 wallCol = lerp(_WallTopColor.rgb, _WallBottomColor.rgb, depth01);

        Light wallLight = GetMainLight();
        float wallNdotL = saturate(dot(normalize(IN.normalWS), wallLight.direction));
        float3 wallLit  = wallCol * (wallLight.color.rgb * (wallNdotL * 0.35 + 0.65));

        float wallAlpha = isFrontFace ? _WallAlpha : _WallInnerAlpha;
        return half4(wallLit, wallAlpha);
    }

    // ────────────────────────────────────────────────────────
    // 수면(윗면) 처리
    // ────────────────────────────────────────────────────────
    float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);
    float3 normalWS  = normalize(IN.normalWS);

    // 마이크로 노멀맵 두 레이어 블렌드
    // 타일링/오프셋은 텍스처의 기본 Tiling/Offset 필드(_NormalMap_ST)로 제어.
    // 오프셋은 두 레이어에 동일하게 적용되도록 타일링 배율(1.7)과 분리해서 더함
    // (그냥 (uv*ST.xy+ST.zw)*1.7을 하면 오프셋까지 1.7배 되어 레이어별로 어긋남)
    float2 normalTiledUV = IN.uv * _NormalMap_ST.xy * _NoiseSpaceScale;
    float2 normalOffset  = _NormalMap_ST.zw;
    float2 speedA = float2(_Time.x * 0.5,  _Time.x * 0.3);
    float2 speedB = float2(-_Time.x * 0.3, _Time.x * 0.4);
    float3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalTiledUV + normalOffset + speedA));
    float3 nB = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalTiledUV * 1.7 + normalOffset + speedB));
    float3 detailNormal = normalize(nA + nB);
    normalWS = normalize(lerp(normalWS, BlendNormalRNM(normalWS, detailNormal), _NormalStrength));

    bool isUnderwater = !isFrontFace || (GetCameraPositionWS().y < IN.positionWS.y);
    if (isUnderwater) normalWS = -normalWS;

    // ── 수심 ─────────────────────────────────────
    float linearEyeDepth = -TransformWorldToView(IN.positionWS).z;
    float depthDiff = CalculateWaterDepthDifference(IN.screenUV, linearEyeDepth);

    float3 depthColor;
    float  shoreFoam;
    ApplyDepthColorAndFoam(
        depthDiff, _ShallowColor.rgb, _DeepColor.rgb, _DepthFadeDistance,
        _FoamDistance, depthColor, shoreFoam);

    // ── 폼 마스크 ────────────────────────────────
    float correctedHeightScale = _WaveHeightScale * _LocalHeightCorrection;
    float perPixelHeight = GerstnerHeightOnly(
        IN.sampleXZ, _Time.y * _WaveSpeed, _WaveRoughness,
        correctedHeightScale, (int)_ActiveWaveCount, IN.baseHeight);

    float slope01   = 1.0 - saturate(normalWS.y);
    float crestFoam = CrestFoamMask(perPixelHeight, slope01, _CrestFoamThreshold, _CrestFoamRange);
    if (isUnderwater) crestFoam *= 0.3;

    float foamMask  = max(shoreFoam, crestFoam);
    float foamBlend = pow(foamMask, 1.3);

    // ── 조명 ─────────────────────────────────────
    Light  mainLight = GetMainLight();
    float3 halfDir   = normalize(mainLight.direction + viewDirWS);
    float  NdotH     = saturate(dot(normalWS, halfDir));
    float  NdotL     = saturate(dot(normalWS, mainLight.direction));
    float  specularExponent = exp2(_SpecularSharpness * _Smoothness + 1.0); // _SpecularSharpness를 키우면 더 작고 쨍해지고, 줄이면 더 크고 부드러워짐
    float  specularNorm     = (specularExponent + 2.0) / (2.0 * PI); // 에너지 보존 정규화 - 피크가 밝아지는 만큼 가장자리도 같이 밝아져서 보이는 영역이 넓어지므로, 지수를 확 키워 가장자리 감쇠를 그만큼 빠르게 만들어 상쇄함
    float  specular  = pow(NdotH, specularExponent) * specularNorm * _Smoothness * _SpecularIntensity;
    float  fresnel   = InlineFresnelEffect(normalWS, viewDirWS, _FresnelPower);

    float  diffuseTerm  = isUnderwater ? (NdotL * 0.15 + 0.95) : (NdotL * 0.5 + 0.5);
    float3 diffuseLight = mainLight.color.rgb * diffuseTerm;
    float3 litColor     = depthColor * diffuseLight;

    if (isUnderwater)
    {
        litColor += specular * mainLight.color.rgb * 0.2;
        litColor  = lerp(litColor, _DeepColor.rgb * 1.2, fresnel * 0.3);
    }
    else
    {
        litColor += specular * mainLight.color.rgb;
        litColor  = lerp(litColor, mainLight.color.rgb, fresnel * 0.4 * _Smoothness);
    }

    // ── 폼 블렌드 ────────────────────────────────
    float3 foamLit = _FoamColor.rgb * (mainLight.color.rgb * NdotL * 0.6 + 0.4);
    litColor = lerp(litColor, foamLit, foamBlend);

    // ── 알파 ─────────────────────────────────────
    float depthAlpha      = pow(saturate(depthDiff / max(0.001, _DepthFadeDistance)), _DepthAlphaFade);
    float finalAlpha      = isUnderwater
                          ? max(_DeepColor.a, 0.85)
                          : lerp(_ShallowColor.a, _DeepColor.a, depthAlpha);
    finalAlpha = max(finalAlpha, foamBlend);

    return half4(litColor, finalAlpha);
}

#endif

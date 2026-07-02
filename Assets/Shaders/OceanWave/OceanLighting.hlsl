#ifndef OCEAN_LIGHTING_INCLUDED
#define OCEAN_LIGHTING_INCLUDED

// URP 단일 샘플 기반 수심 차이 계산
float CalculateWaterDepthDifference(float2 screenUV, float linearEyeDepth)
{
    float sceneDepth = SampleSceneDepth(screenUV);
    float linearSceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
    return max(0.0, linearSceneDepth - linearEyeDepth);
}

// 얕은물, 깊은물 색상 및 연안 거품 마스크 계산
void ApplyDepthColorAndFoam(
    float depthDiff, float3 shallowColor, float3 deepColor, float depthFade,
    float foamDistance, out float3 finalColor, out float foamMask)
{
    float depthT = saturate(depthDiff / max(0.001, depthFade));
    finalColor = lerp(shallowColor, deepColor, depthT);

    if (foamDistance > 0.01)
    {
        foamMask = 1.0 - saturate(depthDiff / foamDistance);
        foamMask = smoothstep(0.0, 1.0, foamMask);
    }
    else
    {
        foamMask = 0.0;
    }
}

// Crest foam: 파도 마루의 가파른 사면에 생기는 거품 마스크
float CrestFoamMask(float waveHeight, float slope01, float threshold, float smoothRange)
{
    float heightTerm = smoothstep(threshold, threshold + max(smoothRange, 0.05), waveHeight);
    float slopeTerm  = smoothstep(0.01, 0.2, slope01);
    return saturate(heightTerm * slopeTerm);
}

#endif

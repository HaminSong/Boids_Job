#ifndef FISHSWIM_SHADOW_PASS_INCLUDED
#define FISHSWIM_SHADOW_PASS_INCLUDED

#include "FishSwimInput.hlsl"
#include "FishSwimDeform.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// URP가 ShadowCaster 드로우콜마다 세팅하는 변수
float3 _LightDirectionShadow; // 라이트 방향 (월드 스페이스)
float4 _ShadowBias;           // x: depth bias, y: normal bias

// 섀도우 바이어스 적용 후 클립 스페이스 위치 반환
float4 ApplyShadowBiasAndTransform(float3 posWS, float3 normalWS)
{
    // Normal Bias : 그레이징 앵글일수록 더 크게 적용
    float invNdotL = 1.0 - saturate(dot(_LightDirectionShadow, normalWS));
    posWS += normalWS * (invNdotL * _ShadowBias.y);

    // Depth Bias : 플랫폼별 깊이 방향 대응
    float4 posCS = mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
#if UNITY_REVERSED_Z
    posCS.z = min(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    posCS.z = max(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    return posCS;
}

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vertShadow(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 worldPos = mul(UNITY_MATRIX_M, float4(0,0,0,1)).xyz;

    float sinR, cosR;
    float3 deformed       = SwimDeform(IN.positionOS.xyz, worldPos, sinR, cosR);
    float3 deformedNormal = RotateY(IN.normalOS, sinR, cosR);

    float3 posWS    = mul(UNITY_MATRIX_M, float4(deformed, 1.0)).xyz;
    float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_M, deformedNormal));

    OUT.positionHCS = ApplyShadowBiasAndTransform(posWS, normalWS);
    return OUT;
}

// 깊이만 기록, 색상 출력 없음
half4 fragShadow(Varyings IN) : SV_Target { return 0; }

#endif

#ifndef FISHSWIM_SHADOW_PASS_INCLUDED
#define FISHSWIM_SHADOW_PASS_INCLUDED

#include "FishSwimInput.hlsl"
#include "FishSwimDeform.hlsl"

float3 _LightDirection;

struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings   { float4 positionHCS : SV_POSITION; };

Varyings vertShadow(Attributes IN)
{
    UNITY_SETUP_INSTANCE_ID(IN);
    Varyings OUT;
    float3 worldPos = mul(UNITY_MATRIX_M, float4(0,0,0,1)).xyz;

    float rad;
    float3 deformed       = SwimDeform(IN.positionOS.xyz, worldPos, rad);
    float3 deformedNormal  = RotateY(IN.normalOS, rad);

    float3 posWS    = mul(UNITY_MATRIX_M, float4(deformed, 1.0)).xyz;
    float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_M, deformedNormal));

    float  scale    = (1.0 - saturate(dot(_LightDirection, normalWS))) * 0.001;
    posWS          += normalWS * scale;

    OUT.positionHCS = mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
    return OUT;
}

half4 fragShadow(Varyings IN) : SV_Target { return 0; }

#endif

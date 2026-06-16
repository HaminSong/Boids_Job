#ifndef FISHSWIM_FORWARD_PASS_INCLUDED
#define FISHSWIM_FORWARD_PASS_INCLUDED

#include "FishSwimInput.hlsl"
#include "FishSwimDeform.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;   // w: 바이탄젠트 방향 부호
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv          : TEXCOORD0;
    float3 positionWS  : TEXCOORD1;
    float3 normalWS    : TEXCOORD2;
    float4 shadowCoord : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    // 피벗 월드 위치 (SwimDeform 위상 계산용)
    float3 worldPos = mul(UNITY_MATRIX_M, float4(0,0,0,1)).xyz;

    // 유영 변형 적용, sinR/cosR을 법선·탄젠트에 재사용
    float sinR, cosR;
    float3 deformedPos     = SwimDeform(IN.positionOS.xyz, worldPos, sinR, cosR);
    float3 deformedNormal  = RotateY(IN.normalOS,       sinR, cosR);
    float3 deformedTangent = RotateY(IN.tangentOS.xyz,  sinR, cosR);

    VertexPositionInputs posInputs = GetVertexPositionInputs(deformedPos);
    VertexNormalInputs   nrmInputs = GetVertexNormalInputs(deformedNormal, float4(deformedTangent, IN.tangentOS.w));

    OUT.positionHCS = posInputs.positionCS;
    OUT.positionWS  = posInputs.positionWS;
    OUT.normalWS    = nrmInputs.normalWS;
    OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
    OUT.shadowCoord = GetShadowCoord(posInputs);

    return OUT;
}

half4 frag(Varyings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap,     sampler_BaseMap,     IN.uv) * _BaseColor;
    half  metallic  = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, IN.uv).r * _MetallicScale;

    InputData inputData = (InputData)0;
    inputData.positionWS              = IN.positionWS;
    inputData.normalWS                = normalize(IN.normalWS);
    inputData.viewDirectionWS         = normalize(GetWorldSpaceViewDir(IN.positionWS));
    inputData.shadowCoord             = IN.shadowCoord;
    inputData.bakedGI                 = SampleSH(normalize(IN.normalWS));
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS); // Forward+ 타일 조회용

    SurfaceData surface = (SurfaceData)0;
    surface.albedo     = baseColor.rgb;
    surface.alpha      = 1.0;
    surface.metallic   = metallic;
    surface.smoothness = _Smoothness;
    surface.occlusion  = 1.0;
    surface.normalTS   = half3(0, 0, 1); // 노말맵 없음

    return UniversalFragmentPBR(inputData, surface);
}

#endif

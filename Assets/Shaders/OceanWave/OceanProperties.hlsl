#ifndef OCEAN_PROPERTIES_INCLUDED
#define OCEAN_PROPERTIES_INCLUDED

// ForwardLit / ShadowCaster 두 패스가 동일하게 include.
// SRP Batcher 호환을 위해 CBUFFER 레이아웃(변수 순서/타입)이 두 패스에서 반드시 일치해야 하는데,
// 각 패스에 따로 선언하면 한쪽만 수정했을 때 어긋나기 쉬워서 파일 하나로 통합함.
CBUFFER_START(UnityPerMaterial)
    float _WaveSpeed;
    float _WaveRoughness;
    float _WaveHeightScale;
    float _WaveSteepnessScale;
    float _ActiveWaveCount;

    float _LocalWaveScale;
    float _LocalHeightCorrection;
    float _NoiseSpaceScale;

    float4 _ShallowColor;
    float4 _DeepColor;
    float _DepthFadeDistance;
    float _DepthAlphaFade;

    float4 _WallTopColor;
    float4 _WallBottomColor;
    float _WallDepthPower;
    float _WallAlpha;
    float _WallInnerAlpha;

    float4 _FoamColor;
    float _FoamDistance;
    float _CrestFoamThreshold;
    float _CrestFoamRange;

    float _Smoothness;
    float _SpecularSharpness;
    float _SpecularIntensity;
    float _FresnelPower;
    float4 _NormalMap_ST;   // _NormalMap의 Tiling(xy)/Offset(zw) — TRANSFORM_TEX 매크로가 참조
    float _NormalStrength;
CBUFFER_END

#endif

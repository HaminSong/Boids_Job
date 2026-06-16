#ifndef FISHSWIM_INPUT_INCLUDED
#define FISHSWIM_INPUT_INCLUDED

TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;       // tiling/offset (xy: tiling, zw: offset)
    float4 _BaseColor;        // 색상 틴트
    float  _MetallicScale;    // 메탈릭 강도
    float  _Smoothness;       // 표면 거칠기
    float  _MaxSwingAngle;    // 최대 회전각 (도)
    float  _Frequency;        // 진동수 (Hz)
    float  _WaveSpeed;        // 머리→꼬리 파동 전파 속도
    float  _HeadInfluence;    // 머리 구역 흔들림 가중치
    float  _MidInfluence;     // 중간 구역 흔들림 가중치
    float  _TailInfluence;    // 꼬리 구역 흔들림 가중치
    float  _XMin;             // 메시 로컬 X 최솟값 (머리)
    float  _XMax;             // 메시 로컬 X 최댓값 (꼬리)
    float  _PhaseScale;       // 개체별 위상 오프셋 배율
CBUFFER_END

#endif

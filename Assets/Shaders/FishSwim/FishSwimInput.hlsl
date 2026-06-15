#ifndef FISHSWIM_INPUT_INCLUDED
#define FISHSWIM_INPUT_INCLUDED

TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _MetallicMap_ST;
    float4 _BaseColor;
    float  _MetallicScale;
    float  _Smoothness;
    float  _MaxSwingAngle;
    float  _Frequency;
    float  _WaveSpeed;
    float  _HeadInfluence;
    float  _MidInfluence;
    float  _TailInfluence;
    float  _XMin;
    float  _XMax;
    float  _PhaseScale;
CBUFFER_END

#endif

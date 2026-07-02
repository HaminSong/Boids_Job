#ifndef GERSTNER_WAVE_INCLUDED
#define GERSTNER_WAVE_INCLUDED

#include "SimplexNoise.hlsl"

struct GerstnerWaveData
{
    float2 direction;
    float steepness;
    float k;             // 사전계산된 2*PI/wavelength (매 정점/픽셀 sqrt·나눗셈 재계산 방지)
    float c;              // 사전계산된 sqrt(9.8/k) — 파도 위상 속도
    float phase;        // 파도별 위상 오프셋 — 체커보드 격자 파괴 핵심
};

#define MAX_WAVE_COUNT 6

// ─────────────────────────────────────────────────────────────────────────────
// 파도 파라미터 정의 (ApplyGerstnerWaves / GerstnerHeightOnly 공용 — 단일 소스)
//
// [방향] 기본 47° 등간격 배치에 파도별로 ±5~13° 랜덤 지터를 얹음 (축 정렬/등간격 배치 방지).
//
// [진폭 감쇠] 진폭(∝steepness/k)을 4개 파도 사이에 비슷하게 맞추면(평탄화) 오히려
//        규칙적인 간섭(beat) 패턴이 생겨 격자가 더 또렷해짐 — 크고 작은 파도가 확실히
//        차이 나야 작은 파도가 "텍스처"처럼 자연스럽게 묻힘.
//
// [phase] 황금비(φ) × π 누적.
// ─────────────────────────────────────────────────────────────────────────────
//
//  i  angle(°)   dir                  steep   wl      k          c          phase
//  0    8        ( 0.990,  0.139)     0.43    50.1    0.125413   8.839790   0.000
//  1   41        ( 0.755,  0.656)     0.47    27.2    0.230999   6.513397   2.399
//  2  105        (-0.259,  0.966)     0.28    19.5    0.322215   5.514935   4.798
//  3  132        (-0.669,  0.743)     0.27    10.1    0.622098   3.969025   1.197
//  4  201        (-0.934, -0.358)     0.19     5.5    1.142397   2.928899   3.596   (비활성)
//  5  224        (-0.719, -0.695)     0.15     2.0    3.141593   1.766193   5.995   (비활성)
// ─────────────────────────────────────────────────────────────────────────────
#define OCEAN_WAVE_TABLE { \
    { float2( 0.990,  0.139), 0.43, 0.125413, 8.839790, 0.000 }, \
    { float2( 0.755,  0.656), 0.47, 0.230999, 6.513397, 2.399 }, \
    { float2(-0.259,  0.966), 0.28, 0.322215, 5.514935, 4.798 }, \
    { float2(-0.669,  0.743), 0.27, 0.622098, 3.969025, 1.197 }, \
    { float2(-0.934, -0.358), 0.19, 1.142397, 2.928899, 3.596 }, \
    { float2(-0.719, -0.695), 0.15, 3.141593, 1.766193, 5.995 }, \
}

static const float OCEAN_NOISE_SCALE = 0.04;
static const float OCEAN_NOISE_SPEED = 0.2;

// 2단계 도메인 워프 — ApplyGerstnerWaves / GerstnerHeightOnly 공용
// 1단계: 저주파 워프(너울 레벨 큰 굴곡) -> 2단계: warp1로 비튼 좌표에서 고주파 추가 샘플링
float2 ApplyOceanDomainWarp(float2 worldXZ, float time, float roughness)
{
    float2 warp1 = float2(
        SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE       + float2(time * OCEAN_NOISE_SPEED,        time * OCEAN_NOISE_SPEED * 0.3)),
        SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE * 2.1 + float2(-time * OCEAN_NOISE_SPEED * 0.2, time * OCEAN_NOISE_SPEED))
    ) * roughness * 3.5;

    float2 warp2 = float2(
        SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE * 4.3 + warp1 * 0.4 + float2(17.3, 5.1)),
        SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE * 4.3 + warp1 * 0.4 + float2(3.7, 43.1))
    ) * roughness * 1.2;

    return worldXZ + warp1 + warp2;
}

float3 GerstnerWave(
    float2 pos, float2 dir, float steepness, float k, float c, float phase,
    float time, float heightScale, float steepnessScale, inout float3 tangent, inout float3 binormal)
{
    float2 d = normalize(dir);
    float f = k * (dot(d, pos) - c * time) + phase;

    float a = (steepness / k) * heightScale;
    float s = steepness * steepnessScale;

    tangent += float3(
        -d.x * d.x * (s * sin(f)),
        d.x * (s * cos(f)),
        -d.x * d.y * (s * sin(f))
    );
    binormal += float3(
        -d.x * d.y * (s * sin(f)),
        d.y * (s * cos(f)),
        -d.y * d.y * (s * sin(f))
    );

    return float3(
        d.x * (s / k * cos(f)),
        a * sin(f),
        d.y * (s / k * cos(f))
    );
}

void ApplyGerstnerWaves(
    float3 originalPos, float time, float roughness, float heightScale, float steepnessScale, int activeWaveCount,
    out float3 outPosition, out float3 outNormal)
{
    if (activeWaveCount <= 0)
    {
        outPosition = originalPos;
        outNormal = float3(0, 1, 0);
        return;
    }

    GerstnerWaveData waves[MAX_WAVE_COUNT] = OCEAN_WAVE_TABLE;

    float2 worldXZ = ApplyOceanDomainWarp(originalPos.xz, time, roughness);

    float3 displacement = float3(0, 0, 0);
    float3 tangent  = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);

    for (int i = 0; i < MAX_WAVE_COUNT; i++)
    {
        if (i >= activeWaveCount)
            break;
        displacement += GerstnerWave(
            worldXZ,
            waves[i].direction, waves[i].steepness, waves[i].k, waves[i].c, waves[i].phase,
            time, heightScale, steepnessScale,
            tangent, binormal);
    }

    outPosition = float3(originalPos.x + displacement.x, originalPos.y + displacement.y, originalPos.z + displacement.z);

    float3 normal = normalize(cross(binormal, tangent));

    float2 noiseGradient = float2(
        SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE * 3.0 + float2(time * OCEAN_NOISE_SPEED * 1.2, 0)),
        SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE * 3.0 + float2(0, time * OCEAN_NOISE_SPEED * 1.2))
    );

    float3 noiseNormal = normalize(float3(
        -noiseGradient.x * (roughness * 0.4 * heightScale),
        1.0,
        -noiseGradient.y * (roughness * 0.4 * heightScale)));
    outNormal = normalize(normal + (noiseNormal - float3(0, 1, 0)));
}

float GerstnerHeightOnly(float2 worldXZ, float time, float roughness, float heightScale, int activeWaveCount, float baseHeight)
{
    if (activeWaveCount <= 0)
    {
        return baseHeight;
    }

    GerstnerWaveData waves[MAX_WAVE_COUNT] = OCEAN_WAVE_TABLE;

    worldXZ = ApplyOceanDomainWarp(worldXZ, time, roughness);

    float height = 0.0;

    for (int i = 0; i < MAX_WAVE_COUNT; i++)
    {
        if (i >= activeWaveCount)
            break;
        float2 d = normalize(waves[i].direction);
        float f = waves[i].k * (dot(d, worldXZ) - waves[i].c * time) + waves[i].phase;
        height += ((waves[i].steepness / waves[i].k) * heightScale) * sin(f);
    }

    return baseHeight + height + (SimplexNoise2D(worldXZ * OCEAN_NOISE_SCALE * 2.0) * roughness * 0.5 * heightScale);
}

// 정점 단계 파도 변위 계산 — ForwardLit / ShadowCaster 패스 vert()에서 공통으로 사용.
// 버텍스 컬러 R채널(waveMask)로 원본 위치/노멀과 블렌드하고, Local 변환 스케일 보정까지 포함.
void ComputeOceanWavedVertex(
    float3 positionOS, float3 normalOS, float waveMask, float localWaveScale,
    float time, float roughness, float heightScale, float steepnessScale, int activeWaveCount,
    out float3 outPositionOS, out float3 outNormalOS)
{
    float3 originalLocalPos = positionOS;
    float3 localPos = positionOS;
    localPos.xz *= localWaveScale;

    float3 wavedPos = localPos;
    float3 wavedNormal = float3(0, 1, 0);
    ApplyGerstnerWaves(
        wavedPos, time, roughness, heightScale, steepnessScale, activeWaveCount,
        wavedPos, wavedNormal);
    wavedPos.xz /= localWaveScale;

    outPositionOS = lerp(originalLocalPos, wavedPos, waveMask);
    outNormalOS   = lerp(normalOS, wavedNormal, waveMask);
}

#endif

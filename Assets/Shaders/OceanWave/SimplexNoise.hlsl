#ifndef SIMPLEX_NOISE_INCLUDED
#define SIMPLEX_NOISE_INCLUDED

// 절차적 2D Simplex Noise (텍스처 샘플링 없음, GitHub 공개 프로젝트용)
// 참고: Ashima Arts / Stefan Gustavson 공개 구현을 URP HLSL용으로 정리

float2 mod289(float2 x) { return x - floor(x / 289.0) * 289.0; }
float3 mod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
float3 permute(float3 x) { return mod289(((x * 34.0) + 1.0) * x); }

// 단일 2D simplex noise, 결과 범위 약 [-1, 1]
float SimplexNoise2D(float2 v)
{
    const float4 C = float4(0.211324865405187, 0.366025403784439,
                            -0.577350269189626, 0.024390243902439);

    float2 i  = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);

    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    float2 x1 = x0.xy + C.xx - i1;
    float2 x2 = x0.xy + C.zz;

    i = mod289(i);
    float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0), dot(x1, x1), dot(x2, x2)), 0.0);
    m = m * m;
    m = m * m;

    float3 x = 2.0 * frac(p * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * float2(x1.x, x2.x) + h.yz * float2(x1.y, x2.y);

    return 130.0 * dot(m, g);
}

// 여러 옥타브 누적 (FBM) - 디테일 레이어를 더 자연스럽게
// octaves: 누적 횟수, lacunarity: 옥타브마다 주파수 배율, gain: 옥타브마다 강도 배율
float SimplexNoiseFBM(float2 v, int octaves, float lacunarity, float gain)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;

    for (int i = 0; i < octaves; i++)
    {
        sum += SimplexNoise2D(v * freq) * amp;
        freq *= lacunarity;
        amp *= gain;
    }

    return sum;
}

// 변위용 도메인 워프 noise: 좌표 자체를 noise로 한번 비틀고 그 결과로 다시 noise 샘플링
// -> Gerstner 같은 규칙적 파형의 주기성을 깨는 용도
// 주의: 이 함수는 height(변위) 용도로만 쓸 것. gradient(미분)를 finite difference로 추정하면
// 워프 단계에서 작은 위치 변화가 비선형적으로 증폭되어 기울기가 국소적으로 폭주(spike)함
// -> normal이 순간적으로 과도하게 꺾이면서 화면에 밝은 실선/얼룩(caustic처럼 보이는 무늬)이 생김
float DomainWarpedNoise(float2 pos, float2 time, float warpStrength, float scale)
{
    float2 p = pos * scale;
    float2 warp = float2(
        SimplexNoise2D(p + time),
        SimplexNoise2D(p + time + float2(37.2, 91.1))
    ) * warpStrength;

    return SimplexNoise2D(p + warp);
}

// normal 계산(gradient)용 전용 noise: domain warp 없이 일반 simplex만 사용
// -> finite difference로 미분해도 기울기가 noise 자체의 매끈한 변화율로 제한되어 spike가 없음
// warpedNoise와 동일한 좌표/스케일을 쓰되, 시각적으로는 거의 구분 안 되면서 안정적임
float GradientSafeNoise(float2 pos, float2 time, float scale)
{
    float2 p = pos * scale + time;
    return SimplexNoise2D(p);
}

#endif

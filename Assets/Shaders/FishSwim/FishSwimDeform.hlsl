#ifndef FISHSWIM_DEFORM_INCLUDED
#define FISHSWIM_DEFORM_INCLUDED

#define TWO_PI     6.28318530  // 2π
#define DEG_TO_RAD 0.01745329  // π / 180

// 유영 변형 함수
// posOS    : 오브젝트 스페이스 정점 위치
// worldPos : 피벗 월드 위치 (개체별 위상 계산용)
// sinR/cosR: 회전각의 sin/cos (법선·탄젠트 회전 재사용용)
float3 SwimDeform(float3 posOS, float3 worldPos, out float sinR, out float cosR)
{
    sinR = 0.0;
    cosR = 1.0;

    float xRange = _XMax - _XMin;
    if (xRange <= 0) return posOS;

    // 정점 X 위치를 [0, 1]로 정규화 (0=머리, 1=꼬리)
    float tPos = saturate((posOS.x - _XMin) / xRange);

    // 구간별 흔들림 가중치 보간
    float influence;
    if (tPos < 0.33)
        influence = lerp(_HeadInfluence, _MidInfluence, tPos / 0.33);
    else
        influence = lerp(_MidInfluence, _TailInfluence, (tPos - 0.33) / 0.67);

    // 월드 위치 기반 개체별 위상 오프셋
    float phase = dot(worldPos, float3(1.3, 2.7, 0.9)) * _PhaseScale;

    // 유영 각도 = 시간 진동 - 꼬리 위상 지연 - 개체 오프셋
    float angle = sin(_Frequency * TWO_PI * _Time.y - tPos * _WaveSpeed - phase)
                  * _MaxSwingAngle * influence;

    float rad = angle * DEG_TO_RAD;
    sinR = sin(rad);
    cosR = cos(rad);

    // 메시 X 중앙 기준 Y축 회전
    float xCenter = (_XMin + _XMax) * 0.5;
    float dx = posOS.x - xCenter;
    float dz = posOS.z;

    return float3(
        xCenter + dx * cosR - dz * sinR,
        posOS.y,
        dx * sinR + dz * cosR
    );
}

// Y축 벡터 회전 (SwimDeform의 sinR/cosR 재사용)
float3 RotateY(float3 v, float sinR, float cosR)
{
    return float3(v.x * cosR - v.z * sinR, v.y, v.x * sinR + v.z * cosR);
}

#endif

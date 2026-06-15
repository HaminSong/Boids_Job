#ifndef FISHSWIM_DEFORM_INCLUDED
#define FISHSWIM_DEFORM_INCLUDED

float3 SwimDeform(float3 posOS, float3 worldPos, out float rad)
{
    rad = 0.0;
    float xRange = _XMax - _XMin;
    if (xRange <= 0) return posOS;

    float tPos = saturate((posOS.x - _XMin) / xRange);

    float influence;
    if (tPos < 0.33)
        influence = lerp(_HeadInfluence, _MidInfluence, tPos / 0.33);
    else
        influence = lerp(_MidInfluence, _TailInfluence, (tPos - 0.33) / 0.67);

    float phase = dot(worldPos, float3(1.3, 2.7, 0.9)) * _PhaseScale;

    float angle = sin(_Frequency * 6.28318 * _Time.y - tPos * _WaveSpeed - phase)
                  * _MaxSwingAngle * influence;
    rad = angle * 0.01745329;

    float xCenter = (_XMin + _XMax) * 0.5;
    float dx = posOS.x - xCenter;
    float dz = posOS.z;

    return float3(
        xCenter + dx * cos(rad) - dz * sin(rad),
        posOS.y,
        dx * sin(rad) + dz * cos(rad)
    );
}

float3 RotateY(float3 v, float rad)
{
    float s = sin(rad);
    float c = cos(rad);
    return float3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
}

#endif

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

[BurstCompile]
public struct UpdatePositionImprovedJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float3> forces;
    public NativeArray<float3> velocities;
    public NativeArray<float3> positions;

    public float deltaTime;
    public float minSpeed;
    public float maxSpeed;
    public float3 boundsCenter;
    public float3 boundsHalfSize;
    public float boundsSoftZone;

    public void Execute(int i, TransformAccess transform)
    {
        float3 vel = velocities[i] + forces[i] * deltaTime;

        // transform.position을 한 번만 읽어 감속 계산과 위치 계산에 재사용
        float3 posI = (float3)transform.position;

        // boundsCenter 기준 로컬 좌표로 변환해 경계 거리를 계산한다.
        float3 localPos = posI - boundsCenter;

        // softZone 진입 시 maxSpeed를 minSpeed까지 서서히 낮춤
        float dxMin = math.min(boundsHalfSize.x - math.abs(localPos.x), boundsSoftZone);
        float dyMin = math.min(boundsHalfSize.y - math.abs(localPos.y), boundsSoftZone);
        float dzMin = math.min(boundsHalfSize.z - math.abs(localPos.z), boundsSoftZone);
        float minDist = math.min(math.min(dxMin, dyMin), dzMin);
        float t = math.saturate(minDist / boundsSoftZone);
        float dynMax = math.lerp(minSpeed, maxSpeed, t);

        float speed = math.length(vel);
        if (speed > 0f)
        {
            float targetSpeed = math.clamp(math.min(speed, dynMax), minSpeed, maxSpeed);
            float smoothSpeed = math.lerp(speed, targetSpeed, 5f * deltaTime);
            vel = math.normalize(vel) * math.clamp(smoothSpeed, minSpeed, maxSpeed);
        }

        // posI 재사용
        float3 pos = posI + vel * deltaTime;
        float3 localNextPos = pos - boundsCenter;

        if (localNextPos.x >  boundsHalfSize.x) { pos.x = boundsCenter.x +  boundsHalfSize.x; vel.x = math.min(vel.x, 0f); }
        if (localNextPos.x < -boundsHalfSize.x) { pos.x = boundsCenter.x - boundsHalfSize.x; vel.x = math.max(vel.x, 0f); }
        if (localNextPos.y >  boundsHalfSize.y) { pos.y = boundsCenter.y +  boundsHalfSize.y; vel.y = math.min(vel.y, 0f); }
        if (localNextPos.y < -boundsHalfSize.y) { pos.y = boundsCenter.y - boundsHalfSize.y; vel.y = math.max(vel.y, 0f); }
        if (localNextPos.z >  boundsHalfSize.z) { pos.z = boundsCenter.z +  boundsHalfSize.z; vel.z = math.min(vel.z, 0f); }
        if (localNextPos.z < -boundsHalfSize.z) { pos.z = boundsCenter.z - boundsHalfSize.z; vel.z = math.max(vel.z, 0f); }

        velocities[i] = vel;
        positions[i]  = pos;
        transform.position = pos;

        if (!vel.Equals(float3.zero))
            transform.rotation = quaternion.LookRotationSafe(vel, math.up());
    }
}

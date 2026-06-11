using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// SpherecastCommand.ScheduleBatch 완료 후 실행.
/// hit.point(충돌 지점) 기반으로 밀어내는 방향을 계산한다.
/// 법선 70% + 충돌점 탈출 방향 30% 혼합.
/// </summary>
[BurstCompile]
public struct ObstacleAvoidJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<RaycastHit> rayHits;
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> velocities;

    [WriteOnly] public NativeArray<float3> avoidForces;

    public float rayDistance;
    public float avoidWeight;

    public void Execute(int i)
    {
        RaycastHit hit = rayHits[i];

        if (hit.distance <= 0f)
        {
            avoidForces[i] = float3.zero;
            return;
        }

        float3 posI = positions[i];

        // hit.point: 충돌 지점 → Boid 방향으로 탈출
        float3 hitPoint = (float3)hit.point;
        float3 fromPoint = posI - hitPoint;
        float distFromPoint = math.length(fromPoint);

        float3 escapeDir = distFromPoint > 0.001f
            ? fromPoint / distFromPoint
            : (float3)hit.normal;

        // 법선 방향
        float3 normalDir = math.lengthsq((float3)hit.normal) > 0.001f
            ? math.normalize((float3)hit.normal)
            : escapeDir;

        // 법선 70% + 충돌점 탈출 방향 30% 혼합
        float3 avoid = math.normalize(math.lerp(normalDir, escapeDir, 0.3f));

        // 선형 감쇠
        float proximity = 1f - math.saturate(hit.distance / rayDistance);

        avoidForces[i] = avoid * avoidWeight * proximity;
    }
}
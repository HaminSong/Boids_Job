using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 각 Boid 위치·속도를 읽어 전방 SpherecastCommand 배열을 생성한다.
/// 선형 레이보다 감지 범위가 넓어 측면 장애물도 감지 가능.
/// </summary>
[BurstCompile]
public struct PrepareRaycastJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> velocities;

    [WriteOnly] public NativeArray<SpherecastCommand> rayCommands;

    public float sphereRadius;
    public float rayDistance;
    public int layerMask;

    public void Execute(int i)
    {
        float3 pos = positions[i];
        float3 vel = velocities[i];

        float3 dir = math.lengthsq(vel) > 0f
            ? math.normalize(vel)
            : new float3(0f, 0f, 1f);

        var queryParams = new QueryParameters(
            layerMask,
            false,
            QueryTriggerInteraction.Ignore,
            false
        );

        rayCommands[i] = new SpherecastCommand(
            pos,
            sphereRadius,
            dir,
            queryParams,
            rayDistance
        );
    }
}
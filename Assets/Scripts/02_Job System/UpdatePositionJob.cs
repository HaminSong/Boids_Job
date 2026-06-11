using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

/// <summary>
/// 힘 계산이 끝난 뒤, 각 Boid의 transform.position / rotation 을 병렬로 반영.
/// TransformAccessArray를 쓰므로 Main Thread 없이도 transform 접근 가능.
/// </summary>
[BurstCompile]
public struct UpdatePositionJob : IJobParallelForTransform
{
    // BoidForceJob 결과
    [ReadOnly] public NativeArray<float3> forces;

    // 속도는 읽고 쓰기 모두 필요 → [ReadOnly] 없음
    public NativeArray<float3> velocities;

    public float deltaTime;
    public float minSpeed;
    public float maxSpeed;

    // NativeArray<float3>는 Job 안에서 직접 쓸 수 있음
    public NativeArray<float3> positions;

    // ────────────────────────────────────────────────────────────
    public void Execute(int i, TransformAccess transform)
    {
        // 힘 → 속도
        float3 vel = velocities[i] + forces[i] * deltaTime;

        // 속도 크기 제한
        float speed = math.length(vel);
        if (speed > 0f)
            vel = math.normalize(vel) * math.clamp(speed, minSpeed, maxSpeed);

        velocities[i] = vel;

        // 위치 업데이트
        float3 pos = (float3)transform.position + vel * deltaTime;
        positions[i]       = pos;
        transform.position = pos;

        // 이동 방향으로 회전
        if (!vel.Equals(float3.zero))
            transform.rotation = quaternion.LookRotationSafe(vel, math.up());
    }
}

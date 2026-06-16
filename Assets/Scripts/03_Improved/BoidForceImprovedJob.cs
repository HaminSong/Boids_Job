using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Separation + Alignment + Cohesion + Boundary + Wander + Leveling.
/// BoidForceJob(02_Job Scene) 대비 개선 사항:
///   - cohesionRadius 분리: Cohesion 전용 반경으로 과도한 뭉침 방지
///   - maxNeighbors: 이웃 탐색 상한으로 군집 분산 유지
///   - 경계 처리: 구형 → AABB + Soft Zone + 조향 힘
///   - Wander: Simplex noise 기반 개체별 방향 노이즈
///   - Leveling: 수직 방향 급상승 억제
/// </summary>
[BurstCompile]
public struct BoidForceImprovedJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> velocities;

    [WriteOnly] public NativeArray<float3> forces;

    public float perceptionRadius;
    public float separationRadius;
    public float separationWeight;
    public float alignmentWeight;
    public float cohesionWeight;
    public float3 boundsHalfSize;
    public float boundsWeight;
    public float boundsSoftZone;
    public float wanderStrength;
    public float time;
    public float levelingStrength;
    public float cohesionRadius;
    public int maxNeighbors;
    public int count;

    public void Execute(int i)
    {
        float3 posI = positions[i];

        // ── 이웃 탐색 ────────────────────────────────────────────
        float3 sep    = float3.zero;
        float3 ali    = float3.zero;
        float3 cohSum = float3.zero;
        int neighborCount = 0;
        int cohCount      = 0;

        float perR2 = perceptionRadius * perceptionRadius;
        float cohR2 = cohesionRadius   * cohesionRadius;

        for (int j = 0; j < count; j++)
        {
            if (i == j) continue;
            if (neighborCount >= maxNeighbors) break;

            float3 offset  = posI - positions[j];
            float  sqrDist = math.lengthsq(offset);

            if (sqrDist < perR2 && sqrDist > 0f)
            {
                float dist = math.sqrt(sqrDist);

                ali += velocities[j];

                if (dist < separationRadius)
                    sep += offset / dist;

                if (sqrDist < cohR2)
                {
                    cohSum += positions[j];
                    cohCount++;
                }

                neighborCount++;
            }
        }

        float3 force = float3.zero;

        if (neighborCount > 0)
        {
            ali /= neighborCount;
            force += ali * alignmentWeight;
            force += sep * separationWeight;
        }

        if (cohCount > 0)
        {
            float3 coh = (cohSum / cohCount) - posI;
            force += coh * cohesionWeight;
        }

        // ── 경계 처리 (AABB + Soft Zone) ─────────────────────────
        {
            float3 push = float3.zero;

            float dxPos = boundsHalfSize.x - posI.x;
            float dxNeg = boundsHalfSize.x + posI.x;
            if (dxPos < boundsSoftZone) push.x -= dxPos < 0f ? 1f + (-dxPos / boundsSoftZone) * 3f : math.smoothstep(1f, 0f, dxPos / boundsSoftZone);
            if (dxNeg < boundsSoftZone) push.x += dxNeg < 0f ? 1f + (-dxNeg / boundsSoftZone) * 3f : math.smoothstep(1f, 0f, dxNeg / boundsSoftZone);

            float dyPos = boundsHalfSize.y - posI.y;
            float dyNeg = boundsHalfSize.y + posI.y;
            if (dyPos < boundsSoftZone) push.y -= dyPos < 0f ? 1f + (-dyPos / boundsSoftZone) * 3f : math.smoothstep(1f, 0f, dyPos / boundsSoftZone);
            if (dyNeg < boundsSoftZone) push.y += dyNeg < 0f ? 1f + (-dyNeg / boundsSoftZone) * 3f : math.smoothstep(1f, 0f, dyNeg / boundsSoftZone);

            float dzPos = boundsHalfSize.z - posI.z;
            float dzNeg = boundsHalfSize.z + posI.z;
            if (dzPos < boundsSoftZone) push.z -= dzPos < 0f ? 1f + (-dzPos / boundsSoftZone) * 3f : math.smoothstep(1f, 0f, dzPos / boundsSoftZone);
            if (dzNeg < boundsSoftZone) push.z += dzNeg < 0f ? 1f + (-dzNeg / boundsSoftZone) * 3f : math.smoothstep(1f, 0f, dzNeg / boundsSoftZone);

            force += push * boundsWeight;

            // 외적 조향: 경계 방향으로 정면 돌진 시 옆으로 꺾음
            float3 vel = velocities[i];
            float pushLen = math.length(push);
            if (pushLen > 0.001f && math.lengthsq(vel) > 0.001f)
            {
                float3 pushNorm  = push / pushLen;
                float3 velNorm   = math.normalize(vel);
                float3 steerDir  = math.cross(velNorm, pushNorm);
                float  steerScale = math.saturate(-math.dot(velNorm, pushNorm));
                force += steerDir * pushLen * boundsWeight * steerScale;
            }

            // ── Leveling: 수직 방향 억제 ─────────────────────────
            float speed = math.length(vel);
            if (speed > 0f)
            {
                float  verticalRatio = math.abs(vel.y) / speed;
                float  levelScale    = verticalRatio * verticalRatio;
                float3 xzVel         = new float3(vel.x, 0f, vel.z);
                float3 levelDir      = math.lengthsq(xzVel) > 0.001f
                    ? math.normalize(xzVel)
                    : new float3(1f, 0f, 0f);
                force += levelDir * levelScale * levelingStrength;
            }
        }

        // ── Wander: Simplex noise 기반 개체별 방향 노이즈 ────────
        float wanderOffset = i * 1.7f;
        float wx = noise.snoise(new float2(time * 0.3f + wanderOffset,         0f));
        float wy = noise.snoise(new float2(time * 0.3f + wanderOffset + 31.4f, 0f));
        float wz = noise.snoise(new float2(time * 0.3f + wanderOffset + 62.8f, 0f));
        force += new float3(wx, wy, wz) * wanderStrength;

        forces[i] = force;
    }
}

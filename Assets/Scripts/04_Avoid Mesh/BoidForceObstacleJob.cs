using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Separation + Alignment + Cohesion + Boundary + Wander + Leveling + 장애물 회피.
/// </summary>
[BurstCompile]
public struct BoidForceObstacleJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> velocities;
    [ReadOnly] public NativeArray<float3> avoidForces;

    [WriteOnly] public NativeArray<float3> forces;

    public float perceptionRadius;
    public float separationRadius;
    public float separationWeight;
    public float alignmentWeight;
    public float cohesionWeight;
    public float3 boundsCenter;
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
        float3 sep = float3.zero;
        float3 ali = float3.zero;
        float3 cohSum = float3.zero;
        int neighborCount = 0;
        int cohCount = 0;

        float perR2 = perceptionRadius * perceptionRadius;
        float cohR2 = cohesionRadius * cohesionRadius;

        for (int j = 0; j < count; j++)
        {
            if (i == j) continue;
            if (neighborCount >= maxNeighbors) break;

            float3 offset = posI - positions[j];
            float sqrDist = math.lengthsq(offset);

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

        // ── 경계 처리 ────────────────────────────────────────────
        {
            // boundsCenter 기준 로컬 좌표로 변환해 계산한다.
            float3 localPos = posI - boundsCenter;

            float3 push = float3.zero;

            float dxPos = boundsHalfSize.x - localPos.x;
            float dxNeg = boundsHalfSize.x + localPos.x;
            if (dxPos < boundsSoftZone) push.x -= GetBoundaryPush(dxPos);
            if (dxNeg < boundsSoftZone) push.x += GetBoundaryPush(dxNeg);

            float dyPos = boundsHalfSize.y - localPos.y;
            float dyNeg = boundsHalfSize.y + localPos.y;
            if (dyPos < boundsSoftZone) push.y -= GetBoundaryPush(dyPos);
            if (dyNeg < boundsSoftZone) push.y += GetBoundaryPush(dyNeg);

            float dzPos = boundsHalfSize.z - localPos.z;
            float dzNeg = boundsHalfSize.z + localPos.z;
            if (dzPos < boundsSoftZone) push.z -= GetBoundaryPush(dzPos);
            if (dzNeg < boundsSoftZone) push.z += GetBoundaryPush(dzNeg);

            force += push * boundsWeight;

            // Soft Zone 진입 시 중심 방향으로도 동일하게 끌어당겨,
            // 벽을 따라 미끄러지지 않고 안쪽으로 들어오도록 한다.
            float pushLen = math.length(push);
            if (pushLen > 0.001f)
            {
                float3 towardCenter = -math.normalize(localPos);
                force += towardCenter * boundsWeight;
            }

            // 외적 조향: 직진 돌진 시 수직 방향으로 꺾음
            // velocities[i]를 여기서 한 번만 읽어 이후 수평 복귀에도 재사용
            float3 vel = velocities[i];
            if (pushLen > 0.001f && math.lengthsq(vel) > 0.001f)
            {
                float3 pushNorm = push / pushLen;
                float3 velNorm = math.normalize(vel);
                float3 steerDir = math.cross(velNorm, pushNorm);
                float steerScale = math.saturate(-math.dot(velNorm, pushNorm));
                force += steerDir * pushLen * boundsWeight * steerScale;
            }

            // ── 수평 복귀 힘 ─────────────────────────────────────
            // vel을 위에서 이미 읽었으므로 재사용
            float speed = math.length(vel);
            if (speed > 0f)
            {
                float verticalRatio = math.abs(vel.y) / speed;
                float3 xzVel = new float3(vel.x, 0f, vel.z);
                float3 levelDir = math.lengthsq(xzVel) > 0.001f
                    ? math.normalize(xzVel)
                    : new float3(1f, 0f, 0f);
                force += levelDir * verticalRatio * levelingStrength;
            }
        }

        // ── Wander ───────────────────────────────────────────────
        float wanderOffset = i * 1.7f;
        float wx = noise.snoise(new float2(time * 0.3f + wanderOffset, 0f));
        float wy = noise.snoise(new float2(time * 0.3f + wanderOffset + 31.4f, 0f));
        float wz = noise.snoise(new float2(time * 0.3f + wanderOffset + 62.8f, 0f));
        force += new float3(wx, wy, wz) * wanderStrength;

        // ── 장애물 회피 ──────────────────────────────────────────
        force += avoidForces[i];

        forces[i] = force;
    }

    // ────────────────────────────────────────────────────────────
    // 경계 복귀 힘 계산.
    //   - 경계 밖(dist < 0): 기본 1배 + 깊이에 비례해 최대 3배 추가, 4배로 상한 (발산 방지)
    //   - Soft Zone 안(0 <= dist < boundsSoftZone): 진입 즉시 BaseForce를 적용하고,
    //     안쪽으로 갈수록 선형으로 증가해 경계에서 1배에 도달
    // ────────────────────────────────────────────────────────────
    const float BaseForce = 0.1f;

    float GetBoundaryPush(float dist)
    {
        if (dist < 0f)
            return 1f + math.min(-dist / boundsSoftZone, 1f) * 3f;

        float t = math.saturate(dist / boundsSoftZone); // 0(경계) ~ 1(SoftZone 시작)
        float oneMinusT = 1f - t;
        return BaseForce + (1f - BaseForce) * oneMinusT;
    }
}
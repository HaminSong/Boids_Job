using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 각 Boid의 Separation + Alignment + Cohesion + Boundary 힘을 병렬 계산.
/// [BurstCompile] → SIMD 자동 벡터화, 관리 힙 접근 없음.
/// </summary>
[BurstCompile]
public struct BoidForceJob : IJobParallelFor
{
    // ── 입력 (읽기 전용, 모든 스레드 공유) ──────────────────────
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> velocities;

    // ── 출력 (인덱스별로 독립 쓰기 → 경합 없음) ─────────────────
    [WriteOnly] public NativeArray<float3> forces;

    // ── 파라미터 ────────────────────────────────────────────────
    public float perceptionRadius;
    public float separationRadius;
    public float separationWeight;
    public float alignmentWeight;
    public float cohesionWeight;
    public float boundsRadius;
    public float boundsWeight;
    public int   count;

    // ────────────────────────────────────────────────────────────
    public void Execute(int i)
    {
        float3 posI = positions[i];
        float3 sep  = float3.zero;
        float3 ali  = float3.zero;
        float3 coh  = float3.zero;
        int    neighborCount = 0;

        float perR2 = perceptionRadius * perceptionRadius;
        float sepR  = separationRadius;

        for (int j = 0; j < count; j++)
        {
            if (i == j) continue;

            float3 offset  = posI - positions[j];
            float  sqrDist = math.lengthsq(offset);

            if (sqrDist < perR2 && sqrDist > 0f)
            {
                float dist = math.sqrt(sqrDist);

                coh += positions[j];
                ali += velocities[j];

                if (dist < sepR)
                    sep += offset / dist;   // 가까울수록 강하게 밀어냄

                neighborCount++;
            }
        }

        float3 force = float3.zero;

        if (neighborCount > 0)
        {
            // 응집: 이웃 평균 위치 → 나
            coh = (coh / neighborCount) - posI;

            // 정렬: 이웃 평균 속도
            ali /= neighborCount;

            force = sep * separationWeight
                  + ali * alignmentWeight
                  + coh * cohesionWeight;
        }

        // 경계: 범위 밖이면 중심 방향으로
        float posMag = math.length(posI);
        if (posMag > boundsRadius)
            force += -math.normalize(posI) * boundsWeight;

        forces[i] = force;
    }
}

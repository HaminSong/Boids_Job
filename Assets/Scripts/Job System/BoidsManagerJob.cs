using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

/// <summary>
/// Job System + Burst 최적화 버전 BoidsManager.
/// 
/// 변경 요약:
///   - positions / velocities / forces → NativeArray<float3> (비관리 메모리)
///   - 힘 계산  : BoidForceJob      (IJobParallelFor   + Burst)
///   - transform 반영 : UpdatePositionJob (IJobParallelForTransform + Burst)
///   - Boid MonoBehaviour의 UpdateBoid()는 더 이상 호출하지 않음
///     (컴포넌트는 Prefab 호환을 위해 남겨 둠)
/// </summary>
public class BoidsManagerJob : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject boidPrefab;

    [Header("Spawn")]
    public int boidCount = 500;
    public float spawnDir = 10f;

    [Header("Speed")]
    public float minSpeed = 5f;
    public float maxSpeed = 10f;

    [Header("Perception")]
    public float perceptionRadius = 5f;
    public float separationRadius = 2f;

    [Header("Weights")]
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;

    [Header("Boundary")]
    public float boundsRadius = 20f;
    public float boundsWeight = 5.0f;

    [Header("Job Tuning")]
    [Tooltip("IJobParallelFor 배치 크기. 32~64 권장 (캐시 라인 효율)")]
    public int innerBatchCount = 32;

    // ── NativeArray (Persistent: 매 프레임 재할당 없음) ─────────
    NativeArray<float3> positions;
    NativeArray<float3> velocities;
    NativeArray<float3> forces;
    TransformAccessArray transformAccessArray;

    // ────────────────────────────────────────────────────────────
    void Start()
    {
        positions = new NativeArray<float3>(boidCount, Allocator.Persistent);
        velocities = new NativeArray<float3>(boidCount, Allocator.Persistent);
        forces = new NativeArray<float3>(boidCount, Allocator.Persistent);

        SpawnBoids();
    }

    void SpawnBoids()
    {
        Transform[] transforms = new Transform[boidCount];

        for (int i = 0; i < boidCount; i++)
        {
            Vector3 spawnPos = new Vector3(
                UnityEngine.Random.Range(-spawnDir * 0.5f, spawnDir * 0.5f),
                UnityEngine.Random.Range(-spawnDir * 0.5f, spawnDir * 0.5f),
                UnityEngine.Random.Range(-spawnDir * 0.5f, spawnDir * 0.5f)
            );

            GameObject go = Instantiate(boidPrefab, spawnPos, UnityEngine.Random.rotation);
            transforms[i] = go.transform;

            // NativeArray 초기화
            Vector3 randSphere = UnityEngine.Random.onUnitSphere;
            float3 initVel = math.normalize(
                new float3(randSphere.x,
                           randSphere.y,
                           randSphere.z)) * minSpeed;

            positions[i] = (float3)spawnPos;
            velocities[i] = initVel;
        }

        // TransformAccessArray: Job 안에서 transform 읽기/쓰기 허용
        transformAccessArray = new TransformAccessArray(transforms);
    }

    // ────────────────────────────────────────────────────────────
    void Update()
    {
        // ── 1) 힘 계산 Job ─────────────────────────────────────
        var forceJob = new BoidForceJob
        {
            positions = positions,
            velocities = velocities,
            forces = forces,
            perceptionRadius = perceptionRadius,
            separationRadius = separationRadius,
            separationWeight = separationWeight,
            alignmentWeight = alignmentWeight,
            cohesionWeight = cohesionWeight,
            boundsRadius = boundsRadius,
            boundsWeight = boundsWeight,
            count = boidCount,
        };

        // innerBatchCount: 스레드당 처리 boid 수
        JobHandle forceHandle = forceJob.Schedule(boidCount, innerBatchCount);

        // ── 2) 위치/회전 업데이트 Job (forceJob 완료 후 실행) ───
        var updateJob = new UpdatePositionJob
        {
            forces = forces,
            velocities = velocities,
            positions = positions,
            deltaTime = Time.deltaTime,
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
        };

        // forceHandle을 dependency로 전달 → 순서 보장
        JobHandle updateHandle = updateJob.Schedule(transformAccessArray, forceHandle);

        // 이 프레임 안에 결과가 필요하므로 Complete() 호출
        updateHandle.Complete();
    }

    // ────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        // NativeArray / TransformAccessArray는 반드시 수동 해제
        if (positions.IsCreated) positions.Dispose();
        if (velocities.IsCreated) velocities.Dispose();
        if (forces.IsCreated) forces.Dispose();
        if (transformAccessArray.isCreated) transformAccessArray.Dispose();
    }

    // ── 기즈모 ──────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Vector3.zero, boundsRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * spawnDir);
    }
}
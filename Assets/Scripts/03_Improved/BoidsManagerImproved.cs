using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

/// <summary>
/// Boids 로직 개선 버전 (03_Improved Scene).
/// BoidsManagerJob(02_Job Scene) 대비 변경 사항:
///   - BoidForceImprovedJob 사용 (Cohesion 분리, maxNeighbors, Wander, Leveling, AABB 경계)
///   - UpdatePositionObstacleJob 재사용 (Soft Zone 속도 감쇠 + 경계 클램핑)
/// </summary>
public class BoidsManagerImproved : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject boidPrefab;

    [Header("Spawn")]
    [Tooltip("스폰할 Boid 수")]
    public int boidCount = 5000;
    [Tooltip("스폰 영역 크기 (XYZ)")]
    public Vector3 spawnSize = new Vector3(400f, 150f, 400f);

    [Header("Speed")]
    [Tooltip("최소 속도")]
    public float minSpeed = 5f;
    [Tooltip("최대 속도")]
    public float maxSpeed = 15f;

    [Header("Perception")]
    [Tooltip("이웃으로 인식하는 반경")]
    public float perceptionRadius = 7.5f;
    [Tooltip("이 거리 이하면 밀어냄")]
    public float separationRadius = 2f;
    [Tooltip("Cohesion 전용 반경. perceptionRadius보다 좁게 설정.")]
    public float cohesionRadius = 4f;
    [Tooltip("최대 이웃 수. 너무 크면 전체가 뭉침.")]
    public int maxNeighbors = 25;

    [Header("Weights")]
    [Tooltip("분리 힘 가중치")]
    public float separationWeight = 1.5f;
    [Tooltip("정렬 힘 가중치")]
    public float alignmentWeight = 1.0f;
    [Tooltip("응집 힘 가중치")]
    public float cohesionWeight = 1.0f;

    [Header("Boundary")]
    [Tooltip("바운더리 중심 오프셋. 비워두면(true) 이 오브젝트의 transform.position을 중심으로 사용한다.")]
    public bool useTransformAsCenter = true;
    [Tooltip("useTransformAsCenter가 false일 때 사용할 바운더리 중심 좌표")]
    public Vector3 boundsCenter = Vector3.zero;
    [Tooltip("박스 경계 크기 (XYZ)")]
    public Vector3 boundsSize = new Vector3(600f, 225f, 600f);
    [Tooltip("경계 안쪽 감지 시작 거리")]
    public float boundsSoftZone = 25f;
    [Tooltip("경계 복귀 힘 세기")]
    public float boundsWeight = 15f;

    [Header("Wander")]
    [Tooltip("장기 수렴 방지 노이즈 강도")]
    public float wanderStrength = 3f;
    [Tooltip("수직으로 꺾일수록 수평으로 되돌리는 힘")]
    public float levelingStrength = 5f;

    [Header("Job Tuning")]
    [Tooltip("IJobParallelFor 배치 크기. 32~64 권장.")]
    public int innerBatchCount = 64;

    NativeArray<float3> positions;
    NativeArray<float3> velocities;
    NativeArray<float3> forces;
    TransformAccessArray transformAccessArray;

    Vector3 GetBoundsCenter()
    {
        return useTransformAsCenter ? transform.position : boundsCenter;
    }

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
        Vector3 center = GetBoundsCenter();

        for (int i = 0; i < boidCount; i++)
        {
            Vector3 spawnPos = center + new Vector3(
                UnityEngine.Random.Range(-spawnSize.x * 0.5f, spawnSize.x * 0.5f),
                UnityEngine.Random.Range(-spawnSize.y * 0.5f, spawnSize.y * 0.5f),
                UnityEngine.Random.Range(-spawnSize.z * 0.5f, spawnSize.z * 0.5f)
            );

            GameObject go = Instantiate(boidPrefab, spawnPos, UnityEngine.Random.rotation);
            transforms[i] = go.transform;

            Vector3 randDir = UnityEngine.Random.onUnitSphere;
            float3 initVel = new float3(randDir.x, randDir.y, randDir.z) * minSpeed;

            positions[i] = (float3)spawnPos;
            velocities[i] = initVel;
        }

        transformAccessArray = new TransformAccessArray(transforms);
    }

    void Update()
    {
        float3 center = (float3)GetBoundsCenter();
        float3 halfSize = new float3(
            boundsSize.x * 0.5f,
            boundsSize.y * 0.5f,
            boundsSize.z * 0.5f
        );

        // ── 1) 힘 계산 ──────────────────────────────────────────
        var forceJob = new BoidForceImprovedJob
        {
            positions = positions,
            velocities = velocities,
            forces = forces,
            perceptionRadius = perceptionRadius,
            separationRadius = separationRadius,
            cohesionRadius = cohesionRadius,
            maxNeighbors = maxNeighbors,
            separationWeight = separationWeight,
            alignmentWeight = alignmentWeight,
            cohesionWeight = cohesionWeight,
            boundsCenter = center,
            boundsHalfSize = halfSize,
            boundsSoftZone = boundsSoftZone,
            boundsWeight = boundsWeight,
            wanderStrength = wanderStrength,
            time = Time.time,
            levelingStrength = levelingStrength,
            count = boidCount,
        };
        JobHandle forceHandle = forceJob.Schedule(boidCount, innerBatchCount);

        // ── 2) 위치·회전 업데이트 ───────────────────────────────
        var updateJob = new UpdatePositionImprovedJob
        {
            forces = forces,
            velocities = velocities,
            positions = positions,
            deltaTime = Time.deltaTime,
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
            boundsCenter = center,
            boundsHalfSize = halfSize,
            boundsSoftZone = boundsSoftZone,
        };
        JobHandle updateHandle = updateJob.Schedule(transformAccessArray, forceHandle);

        updateHandle.Complete();
    }

    void OnDestroy()
    {
        if (positions.IsCreated) positions.Dispose();
        if (velocities.IsCreated) velocities.Dispose();
        if (forces.IsCreated) forces.Dispose();
        if (transformAccessArray.isCreated) transformAccessArray.Dispose();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = GetBoundsCenter();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, boundsSize);

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube(center, boundsSize - Vector3.one * boundsSoftZone * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, spawnSize);
    }
}
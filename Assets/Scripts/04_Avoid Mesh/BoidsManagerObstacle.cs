using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

/// <summary>
/// 장애물 회피 Boids 매니저.
/// </summary>
public class BoidsManagerObstacle : MonoBehaviour
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

    [Header("Obstacle Avoidance")]
    [Tooltip("장애물로 인식할 레이어. Boid 레이어는 반드시 제외할 것.")]
    public LayerMask obstacleLayerMask;
    [Tooltip("전방 탐지 거리")]
    public float rayDistance = 20f;
    [Tooltip("Spherecast 반경. Boid 모델 크기에 맞게 조절.")]
    public float sphereRadius = 0.8f;
    [Tooltip("장애물 회피 힘 가중치")]
    public float avoidWeight = 50f;

    [Header("Avoidance Debug")]
    [Tooltip("장애물에 히트된 Boid의 회피 레이를 기즈모로 가시화한다.")]
    public bool showAvoidGizmos = false;

    [Header("Job Tuning")]
    [Tooltip("IJobParallelFor 배치 크기")]
    public int innerBatchCount = 64;

    NativeArray<float3> positions;
    NativeArray<float3> velocities;
    NativeArray<float3> forces;
    NativeArray<float3> avoidForces;
    NativeArray<SpherecastCommand> rayCommands;
    NativeArray<RaycastHit> rayHits;
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
        avoidForces = new NativeArray<float3>(boidCount, Allocator.Persistent);
        rayCommands = new NativeArray<SpherecastCommand>(boidCount, Allocator.Persistent);
        rayHits = new NativeArray<RaycastHit>(boidCount, Allocator.Persistent);

        SpawnBoids();
    }

    void SpawnBoids()
    {
        Transform[] transforms = new Transform[boidCount];
        Vector3 center = GetBoundsCenter();

        for (int i = 0; i < boidCount; i++)
        {
            // 장애물 콜라이더 안에서 스폰되지 않도록 위치 재시도
            Vector3 spawnPos;
            int attempts = 0;
            do
            {
                spawnPos = center + new Vector3(
                    UnityEngine.Random.Range(-spawnSize.x * 0.5f, spawnSize.x * 0.5f),
                    UnityEngine.Random.Range(-spawnSize.y * 0.5f, spawnSize.y * 0.5f),
                    UnityEngine.Random.Range(-spawnSize.z * 0.5f, spawnSize.z * 0.5f)
                );
                attempts++;
            }
            while (Physics.CheckSphere(spawnPos, sphereRadius * 2f, obstacleLayerMask) && attempts < 20);

            GameObject go = Instantiate(boidPrefab, spawnPos, UnityEngine.Random.rotation, transform);
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

        // 1) SpherecastCommand 명령 생성
        var prepareJob = new PrepareRaycastJob
        {
            positions = positions,
            velocities = velocities,
            rayCommands = rayCommands,
            sphereRadius = sphereRadius,
            rayDistance = rayDistance,
            layerMask = obstacleLayerMask,
        };
        JobHandle prepareHandle = prepareJob.Schedule(boidCount, innerBatchCount);

        // 2) Physics 일괄 Spherecast
        JobHandle rayHandle = SpherecastCommand.ScheduleBatch(
            rayCommands, rayHits, innerBatchCount, maxHits: 1, dependsOn: prepareHandle
        );

        // 3) 회피 힘 계산
        var avoidJob = new ObstacleAvoidJob
        {
            rayHits = rayHits,
            positions = positions,
            velocities = velocities,
            avoidForces = avoidForces,
            rayDistance = rayDistance,
            avoidWeight = avoidWeight,
        };
        JobHandle avoidHandle = avoidJob.Schedule(boidCount, innerBatchCount, rayHandle);

        // 4) Boids 힘 계산
        float3 halfSize = new Unity.Mathematics.float3(boundsSize.x * 0.5f, boundsSize.y * 0.5f, boundsSize.z * 0.5f);
        var forceJob = new BoidForceObstacleJob
        {
            positions = positions,
            velocities = velocities,
            avoidForces = avoidForces,
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
        JobHandle forceHandle = forceJob.Schedule(boidCount, innerBatchCount, avoidHandle);

        // 5) 위치·회전 업데이트 + Wrap-around
        var updateJob = new UpdatePositionObstacleJob
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
        if (avoidForces.IsCreated) avoidForces.Dispose();
        if (rayCommands.IsCreated) rayCommands.Dispose();
        if (rayHits.IsCreated) rayHits.Dispose();
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

    // 장애물에 히트된 Boid만 시각화
    void OnDrawGizmos()
    {
        if (!showAvoidGizmos) return;
        if (!positions.IsCreated || !velocities.IsCreated || !rayHits.IsCreated) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < boidCount; i++)
        {
            RaycastHit hit = rayHits[i];
            if (hit.distance <= 0f) continue;

            Vector3 pos = positions[i];
            Vector3 vel = velocities[i];
            Vector3 dir = vel.magnitude > 0f ? vel.normalized : Vector3.forward;

            Gizmos.DrawRay(pos, dir * hit.distance);
            Gizmos.DrawWireSphere(pos + dir * hit.distance, sphereRadius);
        }
    }
}
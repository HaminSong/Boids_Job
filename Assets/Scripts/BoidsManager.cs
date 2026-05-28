using UnityEngine;

/// <summary>
/// Boids 시뮬레이션
/// Separation + Alignment + Cohesion + Boundary
/// </summary>
public class BoidsManager : MonoBehaviour
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
    public float perceptionRadius = 5f;  // 이웃으로 인식하는 거리
    public float separationRadius = 2f;  // 이 거리 이하면 밀어냄

    [Header("Weights")]
    [Tooltip("Separation(분리)")]
    public float separationWeight = 1.5f;
    [Tooltip("Alignment(정렬)")]
    public float alignmentWeight  = 1.0f;
    [Tooltip("Cohesion(응집)")]
    public float cohesionWeight   = 1.0f;

    [Header("Boundary")]
    public float boundsRadius = 20f;     // 이 범위를 벗어나면 중심으로 되돌아옴
    public float boundsWeight = 5.0f;

    // -------------------------------------------------------

    private Boid[] boids;

    void Start()
    {
        SpawnBoids();
    }

    void SpawnBoids()
    {
        boids = new Boid[boidCount];

        for (int i = 0; i < boidCount; i++)
        {
            // 큐브 범위 안에서 랜덤 스폰
            Vector3 spawnPos = new Vector3(
                Random.Range(-spawnDir * 0.5f, spawnDir * 0.5f),
                Random.Range(-spawnDir * 0.5f, spawnDir * 0.5f),
                Random.Range(-spawnDir * 0.5f, spawnDir * 0.5f)
            );
            GameObject go    = Instantiate(boidPrefab, spawnPos, Random.rotation);
            Boid boid        = go.GetComponent<Boid>();
            boid.velocity    = Random.onUnitSphere * minSpeed;
            boids[i]         = boid;
        }
    }

    // -------------------------------------------------------
    // 매 프레임 업데이트 — O(N²) 이중 for문
    // -------------------------------------------------------
    void Update()
    {
        int count = boids.Length;
        Vector3[] forces = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 sep = Vector3.zero; //Separation(분리)
            Vector3 ali = Vector3.zero; //Alignment (정렬)
            Vector3 coh = Vector3.zero; //Cohesion  (응집)
            int neighborCount = 0;

            Vector3 posI = boids[i].transform.position;

            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;

                Vector3 posJ  = boids[j].transform.position;
                Vector3 offset = posI - posJ;
                float sqrDist  = offset.sqrMagnitude; // sqrt 연산 회피

                // perceptionRadius² 와 비교해서 sqrt 연산 최소화
                if (sqrDist < perceptionRadius * perceptionRadius && sqrDist > 0f)
                {
                    float dist = Mathf.Sqrt(sqrDist);

                    coh += posJ;
                    ali += boids[j].velocity;

                    if (dist < separationRadius)    //가까우면 밀어냄
                        sep += offset / dist;

                    neighborCount++;
                }
            }

            Vector3 force = Vector3.zero;

            if (neighborCount > 0)
            {
                // 응집: 이웃 평균 위치 방향
                coh = (coh / neighborCount) - posI;

                // 정렬: 이웃 평균 속도
                ali /= neighborCount;

                force = sep * separationWeight
                      + ali * alignmentWeight
                      + coh * cohesionWeight;
            }

            // 경계 처리
            force += GetBoundsForce(posI);

            forces[i] = force;
        }

        // 힘 전부 계산 후 위치 업데이트
        for (int i = 0; i < count; i++)
            boids[i].UpdateBoid(forces[i], minSpeed, maxSpeed);
    }

    // -------------------------------------------------------
    // 경계 처리: 범위 밖이면 중심 방향으로 힘 추가
    // -------------------------------------------------------
    Vector3 GetBoundsForce(Vector3 position)
    {
        if (position.magnitude > boundsRadius)
            return -position.normalized * boundsWeight;

        return Vector3.zero;
    }

    // -------------------------------------------------------
    // 기즈모로 범위 시각화
    // -------------------------------------------------------
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Vector3.zero, boundsRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * spawnDir);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poisson Disk Sampling으로 위치를 뽑고, Perlin Noise로 밀도(채택 확률)를 조절해서
/// 너무 균일하거나 너무 무작위하지 않은 "자연스러운" 분포로 프리팹을 배치한다.
///
/// 사용법:
/// 1. 빈 GameObject에 이 컴포넌트를 붙인다.
/// 2. Prefabs 배열에 배치할 프리팹들을 등록한다.
/// 3. AreaSize / Origin으로 배치 영역(XZ 평면 사각형)을 정한다.
/// 4. 인스펙터의 컨텍스트 메뉴(컴포넌트 우클릭) 또는 ScatterPlacer 커스텀 에디터 버튼으로
///    "Scatter" / "Clear"를 실행한다. (런타임에서 Start()에 직접 호출해도 됨)
/// </summary>
public class ScatterPlacer : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("배치할 프리팹 목록. 매 위치마다 이 중 하나를 무작위로 선택한다.")]
    public GameObject[] prefabs;

    [Header("Area")]
    [Tooltip("배치 영역의 중심(월드 좌표). Y는 무시되고 레이캐스트로 지면 높이를 구한다.")]
    public Vector3 origin = Vector3.zero;
    [Tooltip("배치 영역의 가로(X) x 깊이(Z) 크기")]
    public Vector2 areaSize = new Vector2(20f, 20f);

    [Header("Poisson Disk Sampling")]
    [Tooltip("샘플 간 최소 거리. 값이 클수록 듬성듬성, 작을수록 빽빽해진다.")]
    public float minDistance = 1.5f;
    [Tooltip("한 점 주변에서 새 후보점을 찾기 위한 시도 횟수. 30 권장 (Bridson's algorithm 기본값)")]
    public int samplesPerPoint = 30;

    [Header("Count Limit")]
    [Tooltip("개수 상한을 둘지 여부. 끄면 Min Distance/Noise로 걸러진 만큼 전부 배치한다.")]
    public bool useMaxCount = false;
    [Tooltip("최종적으로 배치할 최대 개수. 후보가 더 많으면 영역 전체에서 균등하게 이 개수만큼만 무작위로 골라낸다.")]
    public int maxCount = 30;

    [Header("Density Noise (클러스터링)")]
    [Tooltip("Perlin Noise로 밀도를 조절해서 한쪽에 몰리고 한쪽은 비는 자연스러운 군집을 만든다.")]
    public bool useNoiseDensity = true;
    [Tooltip("Noise 스케일. 작을수록 큰 덩어리로 뭉치고, 클수록 잘게 나뉜다.")]
    public float noiseScale = 0.15f;
    [Tooltip("Noise 값이 이 값보다 낮으면 해당 위치는 배치하지 않는다 (0~1). 높이면 빈 공간이 늘어난다.")]
    [Range(0f, 1f)]
    public float noiseThreshold = 0.35f;
    [Tooltip("랜덤 시드와 무관하게 Noise 패턴을 바꾸고 싶을 때 오프셋을 변경")]
    public Vector2 noiseOffset = Vector2.zero;

    [Header("Attractors (특정 구역에 몰아서 배치)")]
    [Tooltip("켜면 아래 Attractor Points 주변에만 몰아서 배치한다. Noise와 함께 켜면 그 중심 주변에서도 자연스러운 빈틈이 생긴다.")]
    public bool useAttractors = false;
    [Tooltip("끌어당기는 중심점들. Origin 기준 로컬 오프셋(X,Z)으로 입력. 여러 개 등록하면 그 중 가장 가까운 점을 기준으로 계산한다.")]
    public Vector2[] attractorPoints = new Vector2[] { Vector2.zero };
    [Tooltip("각 중심점의 영향 반경. 이 거리 안쪽은 채택 확률이 높고, 멀어질수록 Gaussian 형태로 급격히 줄어든다.")]
    public float attractorRadius = 5f;
    [Tooltip("반경 밖에서도 약하게 배치가 새어나가는 정도 (0 = 반경 밖 완전히 배치 안 됨, 1 = 거리 영향 없음)")]
    [Range(0f, 1f)]
    public float attractorFalloffSoftness = 0.15f;

    [Header("Random Seed")]
    public bool useRandomSeed = true;
    public int seed = 0;

    [Header("Transform Variation - Position")]
    [Tooltip("샘플 위치에 추가로 더할 수직(Y) 오프셋 범위 (지면 묻힘/들림 등 미세 조정용)")]
    [MinMaxSlider(-2f, 2f)]
    public Vector2 yOffsetRange = new Vector2(-0.05f, 0.05f);

    [Header("Transform Variation - Rotation")]
    [Tooltip("Y축(Up) 회전 범위. 보통 0~360 전체 허용")]
    [MinMaxSlider(0f, 360f)]
    public Vector2 yRotationRange = new Vector2(0f, 360f);
    [Tooltip("바위가 지면에 비스듬히 박힌 느낌을 주는 X/Z 틸트 최대 각도")]
    public float maxTiltAngle = 8f;

    [Header("Transform Variation - Scale")]
    [Tooltip("균일 스케일 배율 범위 (X/Y/Z 동일하게 적용). 슬라이더 한계를 넘는 값이 필요하면 숫자 필드를 직접 클릭해서 입력 가능.")]
    [MinMaxSlider(0f, 10f)]
    public Vector2 uniformScaleRange = new Vector2(0.8f, 1.4f);
    [Tooltip("스케일에 약간의 비균일(찌그러짐)을 추가할지 여부")]
    public bool addNonUniformVariation = false;
    [Tooltip("비균일 변형 강도 (0이면 완전 균일, 0.2면 ±20% 추가 변형)")]
    [Range(0f, 0.3f)]
    public float nonUniformStrength = 0.1f;

    [Header("Ground Snap")]
    [Tooltip("배치 후 지면(Terrain/Collider)에 레이캐스트로 스냅할지 여부")]
    public bool snapToGround = true;
    [Tooltip("레이캐스트로 인식할 레이어. 보통 Terrain/Ground 레이어만 체크")]
    public LayerMask groundLayer = ~0;
    [Tooltip("레이캐스트 시작 높이 (origin.y 기준 위쪽으로 이 만큼에서 아래로 쏜다)")]
    public float raycastHeight = 100f;
    [Tooltip("지면의 노멀(기울기)을 회전에 반영할지 여부 (경사면에 바위가 눕는 느낌)")]
    public bool alignToGroundNormal = true;
    [Range(0f, 1f)]
    [Tooltip("지면 노멀 정렬 강도. 1이면 완전히 지면을 따라가고, 0이면 항상 수직")]
    public float groundAlignStrength = 0.6f;

    [Header("Output")]
    [Tooltip("생성된 오브젝트들을 모아둘 부모. 비워두면 이 GameObject가 부모가 된다.")]
    public Transform container;

    private readonly List<GameObject> spawned = new List<GameObject>();

    [ContextMenu("Scatter")]
    public void Scatter()
    {
        Clear();

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("[ScatterPlacer] Prefabs가 비어있습니다.");
            return;
        }

        var rng = useRandomSeed ? new System.Random() : new System.Random(seed);

        Vector2 noiseSeedOffset = noiseOffset;
        if (useRandomSeed)
        {
            // 시드를 랜덤으로 쓸 때 Perlin Noise도 매번 다른 패턴이 되도록 오프셋을 흔든다.
            noiseSeedOffset += new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        }

        List<Vector2> rawPoints = PoissonDiskSampling.Generate(
            areaSize, minDistance, samplesPerPoint, rng);

        // Noise 밀도 필터를 통과한 후보 위치(월드 X/Z)만 모아둔다.
        List<Vector2> candidates = new List<Vector2>(rawPoints.Count);
        foreach (var p in rawPoints)
        {
            float worldX = origin.x - areaSize.x * 0.5f + p.x;
            float worldZ = origin.z - areaSize.y * 0.5f + p.y;

            if (useNoiseDensity)
            {
                float n = Mathf.PerlinNoise(
                    (worldX * noiseScale) + noiseSeedOffset.x,
                    (worldZ * noiseScale) + noiseSeedOffset.y);

                if (n < noiseThreshold)
                    continue; // 밀도가 낮은 영역은 스킵 -> 빈 공간/군집 형성
            }

            if (useAttractors && attractorPoints != null && attractorPoints.Length > 0)
            {
                float acceptChance = AttractorAcceptChance(worldX, worldZ);
                if (rng.NextDouble() > acceptChance)
                    continue; // 가장 가까운 중심점에서 멀수록 채택될 확률이 낮아진다
            }

            candidates.Add(new Vector2(worldX, worldZ));
        }

        // 개수 상한이 켜져 있고 후보가 더 많으면, 영역 전체에서 균등하게 무작위로 골라낸다.
        // (앞에서부터 자르면 한쪽 구역만 채워지므로 반드시 셔플 후 자른다)
        if (useMaxCount && candidates.Count > maxCount)
        {
            Shuffle(candidates, rng);
            candidates.RemoveRange(maxCount, candidates.Count - maxCount);
        }

        Transform parent = container != null ? container : transform;

        foreach (var c in candidates)
        {
            PlaceOne(c.x, c.y, rng, parent);
        }

        Debug.Log($"[ScatterPlacer] {spawned.Count}개 배치 완료 (Poisson 후보 {rawPoints.Count}개 -> Noise 필터 후 {candidates.Count}개 채택).");
    }

    /// <summary>
    /// 등록된 attractorPoints 중 가장 가까운 점까지의 거리를 기준으로 채택 확률(0~1)을 계산한다.
    /// attractorRadius 이내 -> 1.0 (항상 채택)
    /// attractorRadius 밖 -> Gaussian 형태로 점점 줄어들고, attractorFalloffSoftness만큼의 바닥값은 유지
    ///   (완전히 0으로 끊으면 군집 경계가 너무 칼같이 잘려서 부자연스럽기 때문)
    /// </summary>
    private float AttractorAcceptChance(float worldX, float worldZ)
    {
        float closestDistSq = float.MaxValue;
        foreach (var local in attractorPoints)
        {
            float ax = origin.x + local.x;
            float az = origin.z + local.y;
            float dx = worldX - ax;
            float dz = worldZ - az;
            float distSq = dx * dx + dz * dz;
            if (distSq < closestDistSq)
                closestDistSq = distSq;
        }

        float radiusSq = attractorRadius * attractorRadius;
        if (closestDistSq <= radiusSq)
            return 1f;

        // 반경 밖: 거리에 따라 Gaussian 감쇠. sigma는 반경과 비례하게 잡아서
        // attractorRadius 자체가 "확실히 채워지는 핵심 구역" 크기로 직관적으로 느껴지게 한다.
        float dist = Mathf.Sqrt(closestDistSq);
        float over = dist - attractorRadius;
        float sigma = Mathf.Max(0.01f, attractorRadius * 0.5f);
        float gaussian = Mathf.Exp(-(over * over) / (2f * sigma * sigma));

        return Mathf.Lerp(0f, 1f, gaussian) * (1f - attractorFalloffSoftness) + attractorFalloffSoftness * gaussian;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        // Fisher-Yates shuffle
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void PlaceOne(float worldX, float worldZ, System.Random rng, Transform parent)
    {
        float startY = origin.y + raycastHeight;
        Vector3 rayOrigin = new Vector3(worldX, startY, worldZ);

        Vector3 position = new Vector3(worldX, origin.y, worldZ);
        Vector3 groundNormal = Vector3.up;
        bool grounded = false;

        if (snapToGround)
        {
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayer))
            {
                position = hit.point;
                groundNormal = hit.normal;
                grounded = true;
            }
        }

        float yOffset = Lerp(rng, yOffsetRange.x, yOffsetRange.y);
        position.y += yOffset;

        GameObject prefab = prefabs[rng.Next(0, prefabs.Length)];
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, parent);
        instance.name = prefab.name + "_scattered";

        // --- 회전 ---
        float yaw = Lerp(rng, yRotationRange.x, yRotationRange.y);
        float tiltX = Lerp(rng, -maxTiltAngle, maxTiltAngle);
        float tiltZ = Lerp(rng, -maxTiltAngle, maxTiltAngle);

        Quaternion randomTilt = Quaternion.Euler(tiltX, yaw, tiltZ);

        if (grounded && alignToGroundNormal)
        {
            Quaternion groundRot = Quaternion.FromToRotation(Vector3.up, groundNormal);
            Quaternion blended = Quaternion.Slerp(Quaternion.identity, groundRot, groundAlignStrength);
            instance.transform.rotation = blended * randomTilt;
        }
        else
        {
            instance.transform.rotation = randomTilt;
        }

        // --- 스케일 ---
        float baseScale = Lerp(rng, uniformScaleRange.x, uniformScaleRange.y);
        Vector3 scale = Vector3.one * baseScale;

        if (addNonUniformVariation)
        {
            scale.x *= 1f + Lerp(rng, -nonUniformStrength, nonUniformStrength);
            scale.y *= 1f + Lerp(rng, -nonUniformStrength, nonUniformStrength);
            scale.z *= 1f + Lerp(rng, -nonUniformStrength, nonUniformStrength);
        }

        instance.transform.localScale = scale;

        spawned.Add(instance);
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(spawned[i]);
                else
                    Destroy(spawned[i]);
#else
                Destroy(spawned[i]);
#endif
            }
        }
        spawned.Clear();
    }

    private static float Lerp(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.5f);
        Vector3 center = new Vector3(origin.x, origin.y, origin.z);
        Gizmos.DrawWireCube(center, new Vector3(areaSize.x, 0.1f, areaSize.y));

        if (useAttractors && attractorPoints != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            foreach (var local in attractorPoints)
            {
                Vector3 worldPos = new Vector3(origin.x + local.x, origin.y, origin.z + local.y);
                Gizmos.DrawWireSphere(worldPos, attractorRadius);
                Gizmos.DrawSphere(worldPos, attractorRadius * 0.05f);
            }
        }
    }
}

/// <summary>
/// Robert Bridson's Algorithm for Poisson-Disk Sampling.
/// 최소 거리(minDistance)를 보장하면서 영역을 채우는 점들을 생성한다.
/// 결과는 격자처럼 균일하지도, 순수 랜덤처럼 뭉치거나 비지도 않는
/// "자연계에서 흔히 보이는" 분포가 된다.
/// </summary>
public static class PoissonDiskSampling
{
    public static List<Vector2> Generate(Vector2 areaSize, float minDistance, int samplesPerPoint, System.Random rng)
    {
        float cellSize = minDistance / Mathf.Sqrt(2f);
        int gridWidth = Mathf.CeilToInt(areaSize.x / cellSize);
        int gridHeight = Mathf.CeilToInt(areaSize.y / cellSize);

        // 각 그리드 셀에 점의 인덱스를 저장 (-1 = 없음). 주변 탐색을 O(1)에 가깝게 만들기 위함.
        int[,] grid = new int[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                grid[x, y] = -1;

        List<Vector2> points = new List<Vector2>();
        List<Vector2> activeList = new List<Vector2>();

        Vector2 firstPoint = new Vector2(
            (float)rng.NextDouble() * areaSize.x,
            (float)rng.NextDouble() * areaSize.y);

        points.Add(firstPoint);
        activeList.Add(firstPoint);
        SetGrid(grid, firstPoint, cellSize, 0);

        while (activeList.Count > 0)
        {
            int idx = rng.Next(0, activeList.Count);
            Vector2 point = activeList[idx];
            bool found = false;

            for (int i = 0; i < samplesPerPoint; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                // 새 점은 [minDistance, 2*minDistance] 사이 거리에 생성 (Bridson 표준)
                float radius = minDistance * (1f + (float)rng.NextDouble());

                Vector2 candidate = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (candidate.x < 0 || candidate.x >= areaSize.x ||
                    candidate.y < 0 || candidate.y >= areaSize.y)
                    continue;

                if (IsFarEnough(candidate, points, grid, gridWidth, gridHeight, cellSize, minDistance))
                {
                    points.Add(candidate);
                    activeList.Add(candidate);
                    SetGrid(grid, candidate, cellSize, points.Count - 1);
                    found = true;
                    break;
                }
            }

            if (!found)
                activeList.RemoveAt(idx);
        }

        return points;
    }

    private static void SetGrid(int[,] grid, Vector2 point, float cellSize, int index)
    {
        int gx = Mathf.FloorToInt(point.x / cellSize);
        int gy = Mathf.FloorToInt(point.y / cellSize);
        if (gx >= 0 && gx < grid.GetLength(0) && gy >= 0 && gy < grid.GetLength(1))
            grid[gx, gy] = index;
    }

    private static bool IsFarEnough(Vector2 candidate, List<Vector2> points, int[,] grid,
        int gridWidth, int gridHeight, float cellSize, float minDistance)
    {
        int gx = Mathf.FloorToInt(candidate.x / cellSize);
        int gy = Mathf.FloorToInt(candidate.y / cellSize);

        int searchRadius = 2; // minDistance가 cellSize*sqrt2 이므로 2칸이면 충분
        for (int x = Mathf.Max(0, gx - searchRadius); x <= Mathf.Min(gridWidth - 1, gx + searchRadius); x++)
        {
            for (int y = Mathf.Max(0, gy - searchRadius); y <= Mathf.Min(gridHeight - 1, gy + searchRadius); y++)
            {
                int idx = grid[x, y];
                if (idx == -1) continue;

                float dist = Vector2.Distance(candidate, points[idx]);
                if (dist < minDistance)
                    return false;
            }
        }
        return true;
    }
}
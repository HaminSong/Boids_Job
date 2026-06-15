using UnityEngine;

/// <summary>
/// 메시 버텍스를 CPU에서 매 프레임 변형해 물고기 유영을 구현하는 컴포넌트.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class FishSwimMesh : MonoBehaviour
{
    [Header("Swim Settings")]
    public float maxSwingAngle = 50.0f;
    public float frequency = 1.5f;
    public float waveSpeed = 4f;

    [Header("Region Weights")]
    [Range(0f, 1f)] public float headInfluence = 0.05f;
    [Range(0f, 1f)] public float midInfluence = 0.05f;
    [Range(0f, 1f)] public float tailInfluence = 0.2f;

    [Header("Mesh Range")]
    public float xMin = -4.0f;
    public float xMax = 3.0f;

    private Mesh _mesh;
    private Vector3[] _baseVerts;
    private float _xCenter;

    /// <summary>
    /// 원본 메시를 복제해 런타임 수정용 인스턴스를 만들고 초기 데이터를 캐싱한다.
    /// </summary>
    void Start()
    {
        var mf = GetComponent<MeshFilter>();

        // 원본 sharedMesh를 직접 수정하면 모든 인스턴스에 영향이 가므로 복제본을 사용
        _mesh = Instantiate(mf.sharedMesh);
        _mesh.name = "FishSwim_Runtime";
        mf.mesh = _mesh;

        // 매 프레임 변형의 기준점이 될 원본 버텍스 배열 저장
        _baseVerts = _mesh.vertices;

        // 회전 기준점: 메시 중앙
        _xCenter = (xMin + xMax) * 0.5f;
    }

    void Update()
    {
        DeformMesh(Time.time);
    }

    /// <summary>
    /// 각 버텍스를 X 위치 기반 사인파로 회전 변형해 유영 모션을 적용한다.
    /// </summary>
    void DeformMesh(float t)
    {
        float xRange = xMax - xMin;
        if (xRange <= 0f) return;

        var verts = new Vector3[_baseVerts.Length];
        float omega = frequency * Mathf.PI * 2f;

        for (int i = 0; i < _baseVerts.Length; i++)
        {
            Vector3 v = _baseVerts[i];

            // X 위치를 0(머리) ~ 1(꼬리)로 정규화
            float tPos = (v.x - xMin) / xRange;

            // 구역별 흔들림 강도 보간 (머리 → 중간 → 꼬리)
            float influence;
            if (tPos < 0.33f)
                influence = Mathf.Lerp(headInfluence, midInfluence, tPos / 0.33f);
            else
                influence = Mathf.Lerp(midInfluence, tailInfluence, (tPos - 0.33f) / 0.67f);

            // 꼬리쪽 위상이 앞서는 사인파로 회전 각도 계산
            float angle = Mathf.Sin(omega * t - tPos * waveSpeed) * maxSwingAngle * influence;
            float rad = angle * Mathf.Deg2Rad;
            float sinA = Mathf.Sin(rad);
            float cosA = Mathf.Cos(rad);

            // 메시 중앙을 기준으로 XZ 평면 회전
            float dx = v.x - _xCenter;
            float dz = v.z;

            verts[i] = new Vector3(
                _xCenter + dx * cosA - dz * sinA,
                v.y,
                dx * sinA + dz * cosA
            );
        }

        _mesh.vertices = verts;
        _mesh.RecalculateNormals();
    }

    /// <summary>
    /// 복제된 메시 인스턴스를 메모리에서 해제한다.
    /// </summary>
    void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }
}
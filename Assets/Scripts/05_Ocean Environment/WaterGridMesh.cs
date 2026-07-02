using UnityEngine;

// 분할된(subdivided) Plane 메쉬를 런타임/에디터에서 생성.
// 기본 Unity Plane(10x10 분할)보다 훨씬 촘촘한 그리드를 만들어
// 버텍스 셰이더 기반 파도(WaterSurfaceSimple.shader)가 부드럽게 보이도록 함.
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterGridMesh : MonoBehaviour
{
    [Header("Grid Size")]
    [Tooltip("가로(X) 방향 전체 크기")]
    public float width = 50f;
    [Tooltip("세로(Z) 방향 전체 크기")]
    public float length = 50f;

    [Header("Subdivisions")]
    [Tooltip("가로 분할 수 (많을수록 부드럽지만 무거워짐)")]
    [Range(2, 1000)]
    public int widthSegments = 100;
    [Tooltip("세로 분할 수")]
    [Range(2, 1000)]
    public int lengthSegments = 100;

    [Header("Auto")]
    [Tooltip("Inspector 값 바뀔 때 자동으로 다시 생성")]
    public bool rebuildOnValidate = true;

    private Mesh _mesh;

    void Awake()
    {
        Generate();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!rebuildOnValidate) return;
        // 에디터에서 값 바꿀 때 즉시 반영 (Play 중이 아닐 때만)
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Generate();
            };
        }
    }
#endif

    [ContextMenu("Regenerate Mesh")]
    public void Generate()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "WaterGridMesh";
        }
        else
        {
            _mesh.Clear();
        }

        int wSeg = Mathf.Max(1, widthSegments);
        int lSeg = Mathf.Max(1, lengthSegments);

        int vertCountX = wSeg + 1;
        int vertCountZ = lSeg + 1;

        Vector3[] vertices = new Vector3[vertCountX * vertCountZ];
        Vector2[] uvs = new Vector2[vertices.Length];
        Vector3[] normals = new Vector3[vertices.Length];

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        for (int z = 0; z < vertCountZ; z++)
        {
            float tz = (float)z / lSeg;
            float posZ = Mathf.Lerp(-halfL, halfL, tz);

            for (int x = 0; x < vertCountX; x++)
            {
                float tx = (float)x / wSeg;
                float posX = Mathf.Lerp(-halfW, halfW, tx);

                int index = z * vertCountX + x;
                vertices[index] = new Vector3(posX, 0f, posZ);
                uvs[index] = new Vector2(tx, tz);
                normals[index] = Vector3.up;
            }
        }

        // 분할 수가 많으면 인덱스가 16비트(65535) 넘을 수 있어서 32비트 인덱스 사용
        _mesh.indexFormat = (vertices.Length > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        int[] triangles = new int[wSeg * lSeg * 6];
        int triIndex = 0;

        for (int z = 0; z < lSeg; z++)
        {
            for (int x = 0; x < wSeg; x++)
            {
                int i0 = z * vertCountX + x;
                int i1 = i0 + 1;
                int i2 = i0 + vertCountX;
                int i3 = i2 + 1;

                // 위/아래 둘 다 보이게 할 거라 와인딩 순서는 셰이더의 Cull Off가 처리해줌
                triangles[triIndex++] = i0;
                triangles[triIndex++] = i2;
                triangles[triIndex++] = i1;

                triangles[triIndex++] = i1;
                triangles[triIndex++] = i2;
                triangles[triIndex++] = i3;
            }
        }

        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.normals = normals;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        // RecalculateNormals는 평면이라 의미 없음 (버텍스 셰이더에서 변위 후 노멀맵으로 디테일 처리)

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }
}

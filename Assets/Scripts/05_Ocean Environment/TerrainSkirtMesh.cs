using System.Collections.Generic;
using UnityEngine;

// 격자로 붙은 여러 Terrain의 "전체 바깥 둘레"에만 흙/모래 단면(절벽)을 두름.
// - 내부 타일 경계는 무시. 전체 묶음의 외곽 사각형 4변만 벽 생성.
// - 윗변은 각 지점을 덮는 Terrain의 SampleHeight로 굴곡을 그대로 따라감.
// - 바깥 면이 보이도록 변마다 와인딩/노멀 정렬.
//
// 사용:
//   1. 빈 GameObject 생성 → 이 컴포넌트 추가.
//   2. terrains 배열에 타일 Terrain 들을 전부 드래그 (비우면 씬 전체 자동 수집).
//   3. skirtMaterial 에 모래/흙 머티리얼 지정.
//   4. depth / edgeSegments 조정 후 Regenerate.
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TerrainSkirtMesh : MonoBehaviour
{
    [Header("Target Terrains (격자 타일)")]
    [Tooltip("둘러쌀 Terrain 들. 비우면 씬의 모든 활성 Terrain 자동 수집")]
    public Terrain[] terrains;

    [Header("Material")]
    [Tooltip("모래/흙 단면 머티리얼 (불투명 Lit)")]
    public Material skirtMaterial;

    [Header("Skirt Shape")]
    [Tooltip("외곽 한 변당 가로 분할 수 (높을수록 굴곡 매끄러움)")]
    [Range(2, 2048)] public int edgeSegments = 400;
    [Tooltip("단면을 아래로 내리는 깊이 (월드 단위)")]
    public float depth = 15f;
    [Tooltip("단면 세로 분할 수")]
    [Range(1, 64)] public int depthSegments = 6;
    [Tooltip("바닥 밑판 생성 (아래에서 올려다볼 때 막힘)")]
    public bool generateBottom = true;

    [Header("UV (모래/흙 텍스처)")]
    public float uvTilingHorizontal = 0.1f;
    public float uvTilingVertical = 0.1f;

    [Header("Auto")]
    public bool rebuildOnValidate = true;

    private Mesh _mesh;

    void Awake() { Generate(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!rebuildOnValidate) return;
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
        var list = new List<Terrain>();
        if (terrains != null && terrains.Length > 0)
        {
            foreach (var t in terrains) if (t != null) list.Add(t);
        }
        else
        {
            list.AddRange(Terrain.activeTerrains); // 씬 전체 자동 수집
        }

        if (list.Count == 0)
        {
            Debug.LogWarning("[TerrainSkirtMesh] Terrain이 없습니다.");
            return;
        }

        // ---------- 전체 외곽(min/max XZ) 계산 ----------
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var t in list)
        {
            Vector3 o = t.transform.position;
            Vector3 s = t.terrainData.size;
            minX = Mathf.Min(minX, o.x);
            maxX = Mathf.Max(maxX, o.x + s.x);
            minZ = Mathf.Min(minZ, o.z);
            maxZ = Mathf.Max(maxZ, o.z + s.z);
        }

        if (_mesh == null) { _mesh = new Mesh { name = "TerrainSkirtMesh" }; }
        else { _mesh.Clear(); }

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        float bottomY = -Mathf.Abs(depth);
        Vector3 selfPos = transform.position;

        // 월드 (x,z)를 덮는 Terrain을 찾아 표면 높이를 로컬 Y로 반환.
        // 격자라 경계가 딱 맞아서, 가장자리 점은 그 점을 포함하는 타일에서 샘플.
        float SampleLocalY(float worldX, float worldZ)
        {
            Terrain best = null;
            float bestDist = float.MaxValue;
            foreach (var t in list)
            {
                Vector3 o = t.transform.position;
                Vector3 s = t.terrainData.size;
                bool inside = worldX >= o.x - 0.01f && worldX <= o.x + s.x + 0.01f &&
                              worldZ >= o.z - 0.01f && worldZ <= o.z + s.z + 0.01f;
                if (inside) { best = t; break; }
                Vector2 c = new Vector2(o.x + s.x * 0.5f, o.z + s.z * 0.5f);
                float d = Vector2.SqrMagnitude(new Vector2(worldX, worldZ) - c);
                if (d < bestDist) { bestDist = d; best = t; }
            }

            float h = best.SampleHeight(new Vector3(worldX, 0f, worldZ));
            float worldY = best.transform.position.y + h;
            return worldY - selfPos.y;
        }

        // 한 변을 따라 벽 생성.
        float BuildEdge(System.Func<float, Vector2> getWorldXZ, Vector3 outwardNormal, float uOffset, bool flip)
        {
            int n = edgeSegments + 1;
            int ringStart = verts.Count;

            float[] uCoord = new float[n];
            Vector2 prevXZ = getWorldXZ(0f);
            uCoord[0] = uOffset;
            for (int s = 1; s < n; s++)
            {
                Vector2 xz = getWorldXZ((float)s / (n - 1));
                uCoord[s] = uCoord[s - 1] + Vector2.Distance(prevXZ, xz);
                prevXZ = xz;
            }
            float edgeLength = uCoord[n - 1] - uOffset;

            for (int d = 0; d <= depthSegments; d++)
            {
                float td = (float)d / depthSegments; // 0=윗변, 1=바닥
                for (int s = 0; s < n; s++)
                {
                    Vector2 xz = getWorldXZ((float)s / (n - 1));
                    float localX = xz.x - selfPos.x;
                    float localZ = xz.y - selfPos.z;

                    float topY = SampleLocalY(xz.x, xz.y);
                    float y = Mathf.Lerp(topY, bottomY, td);

                    verts.Add(new Vector3(localX, y, localZ));
                    uvs.Add(new Vector2(uCoord[s] * uvTilingHorizontal,
                                        (topY - y) * uvTilingVertical));
                    normals.Add(outwardNormal);
                }
            }

            for (int d = 0; d < depthSegments; d++)
            {
                for (int s = 0; s < n - 1; s++)
                {
                    int a = ringStart + d * n + s;
                    int b = a + 1;
                    int c = a + n;
                    int e = c + 1;

                    if (!flip)
                    {
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(e);
                    }
                    else
                    {
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(e); tris.Add(c);
                    }
                }
            }
            return edgeLength;
        }

        // 외곽 사각형을 시계방향으로 돌면서 바깥면이 보이게.
        // (안쪽이 보이면 네 변의 flip을 전부 반대로 토글)
        float u = 0f;
        u += BuildEdge(t => new Vector2(Mathf.Lerp(minX, maxX, t), minZ), new Vector3(0, 0, -1), u, flip: true);  // Z-
        u += BuildEdge(t => new Vector2(maxX, Mathf.Lerp(minZ, maxZ, t)), new Vector3(1, 0, 0), u, flip: true);   // X+
        u += BuildEdge(t => new Vector2(Mathf.Lerp(maxX, minX, t), maxZ), new Vector3(0, 0, 1), u, flip: true);   // Z+
        u += BuildEdge(t => new Vector2(minX, Mathf.Lerp(maxZ, minZ, t)), new Vector3(-1, 0, 0), u, flip: true);  // X-

        // ---------- 바닥 밑판 ----------
        // 단면 벽 아랫변이 전부 bottomY로 평평하게 끝나므로 quad 한 장이면 딱 맞음.
        if (generateBottom)
        {
            int b0 = verts.Count;
            // 로컬 좌표로 네 모서리
            float lx0 = minX - selfPos.x, lx1 = maxX - selfPos.x;
            float lz0 = minZ - selfPos.z, lz1 = maxZ - selfPos.z;

            verts.Add(new Vector3(lx0, bottomY, lz0));
            verts.Add(new Vector3(lx1, bottomY, lz0));
            verts.Add(new Vector3(lx0, bottomY, lz1));
            verts.Add(new Vector3(lx1, bottomY, lz1));
            for (int i = 0; i < 4; i++)
            {
                normals.Add(Vector3.down);
            }
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2((maxX - minX) * uvTilingHorizontal, 0));
            uvs.Add(new Vector2(0, (maxZ - minZ) * uvTilingHorizontal));
            uvs.Add(new Vector2((maxX - minX) * uvTilingHorizontal, (maxZ - minZ) * uvTilingHorizontal));

            // 아래(-Y)에서 보이도록 와인딩
            tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2);
            tris.Add(b0 + 1); tris.Add(b0 + 3); tris.Add(b0 + 2);
        }

        _mesh.indexFormat = (verts.Count > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetNormals(normals);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;

        var mr = GetComponent<MeshRenderer>();
        if (skirtMaterial != null)
            mr.sharedMaterial = skirtMaterial;
    }
}
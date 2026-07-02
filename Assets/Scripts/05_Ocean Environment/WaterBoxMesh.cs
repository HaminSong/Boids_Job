using System.Collections.Generic;
using UnityEngine;

// 윗면 그리드 + 측벽(skirt)을 가진 "물 박스" 메쉬 생성.
// - 윗면: 촘촘한 그리드. 버텍스 셰이더 Gerstner 파도가 부드럽게 출렁이도록.
// - 측벽: 4면. 윗변은 윗면과 동일하게 파도를 따라가고, 아래로 갈수록 고정.
//   깊이 그라데이션/단면 색을 위해 세로 분할(depthSegments)을 줌.
//
// 셰이더 규약:
//   uv.y == 1  -> 수면(윗변).  셰이더에서 이 정점은 Gerstner 파도 높이를 따라감.
//   uv.y == 0  -> 측벽 바닥.    고정.
//   윗면 정점은 uv.y 를 그리드 V로 쓰되, "수면 마스크"는 color.r 로 따로 전달.
//     color.r == 1 -> 파도 적용(윗면 전체 + 측벽 윗변)
//     color.r == 0 -> 고정(측벽 바닥)
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterBoxMesh : MonoBehaviour
{
    [Header("Grid Size")]
    [Tooltip("가로(X) 방향 전체 크기")]
    public float width = 50f;
    [Tooltip("세로(Z) 방향 전체 크기")]
    public float length = 50f;

    [Header("Subdivisions (윗면)")]
    [Range(2, 1000)] public int widthSegments = 100;
    [Range(2, 1000)] public int lengthSegments = 100;

    [Header("Walls (측벽)")]
    [Tooltip("측벽 생성 여부")]
    public bool generateWalls = true;
    [Tooltip("물 박스 깊이 (윗면 기준 아래로 내려가는 양)")]
    public float depth = 12f;
    [Tooltip("측벽 세로 분할 수 (깊이 그라데이션/곡면 품질)")]
    [Range(1, 64)] public int depthSegments = 8;
    [Tooltip("바닥 면도 생성 (위에서 안 보이면 끄는게 가벼움)")]
    public bool generateBottom = false;

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
        if (_mesh == null) { _mesh = new Mesh { name = "WaterBoxMesh" }; }
        else { _mesh.Clear(); }

        int wSeg = Mathf.Max(1, widthSegments);
        int lSeg = Mathf.Max(1, lengthSegments);

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();   // r = 파도 마스크 (1=수면 따라감, 0=고정)
        var tris = new List<int>();

        int vertCountX = wSeg + 1;
        int vertCountZ = lSeg + 1;

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        // ---------- 1) 윗면 그리드 ----------
        int topStart = verts.Count;
        for (int z = 0; z < vertCountZ; z++)
        {
            float tz = (float)z / lSeg;
            float posZ = Mathf.Lerp(-halfL, halfL, tz);
            for (int x = 0; x < vertCountX; x++)
            {
                float tx = (float)x / wSeg;
                float posX = Mathf.Lerp(-halfW, halfW, tx);

                verts.Add(new Vector3(posX, 0f, posZ));
                uvs.Add(new Vector2(tx, tz));      // 윗면 UV는 그리드 좌표
                normals.Add(Vector3.up);
                colors.Add(new Color(1f, 0f, 0f)); // 윗면 전체 파도 적용
            }
        }

        for (int z = 0; z < lSeg; z++)
        {
            for (int x = 0; x < wSeg; x++)
            {
                int i0 = topStart + z * vertCountX + x;
                int i1 = i0 + 1;
                int i2 = i0 + vertCountX;
                int i3 = i2 + 1;

                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }

        // ---------- 2) 측벽 ----------
        if (generateWalls)
        {
            float bottomY = -Mathf.Abs(depth);

            // 한 변을 따라 측벽 생성. edgePts = 윗변을 따라가는 정점들의 (x,z) 라인.
            // outwardNormal = 벽이 바라보는 바깥 방향. flip = 와인딩 방향 보정.
            void BuildWall(Vector3[] edgePts, Vector3 outwardNormal, bool flip)
            {
                int n = edgePts.Length;
                int ringStart = verts.Count;

                // 정점: (가로 n) x (세로 depthSegments+1)
                for (int d = 0; d <= depthSegments; d++)
                {
                    float td = (float)d / depthSegments; // 0 = 윗변, 1 = 바닥
                    float y = Mathf.Lerp(0f, bottomY, td);
                    float mask = 1f - td;                // 윗변 1 -> 바닥 0
                    for (int s = 0; s < n; s++)
                    {
                        Vector3 p = edgePts[s];
                        verts.Add(new Vector3(p.x, y, p.z));
                        uvs.Add(new Vector2((float)s / (n - 1), mask)); // uv.y = 수면까지 비율
                        normals.Add(outwardNormal);
                        colors.Add(new Color(mask, 0f, 0f));            // 윗변만 파도
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
            }

            // 윗면 가장자리 라인 추출 (윗변과 정점이 정확히 일치해야 틈이 안 생김)
            Vector3[] edgeZmin = new Vector3[vertCountX]; // z = -halfL
            Vector3[] edgeZmax = new Vector3[vertCountX]; // z = +halfL
            for (int x = 0; x < vertCountX; x++)
            {
                edgeZmin[x] = verts[topStart + 0 * vertCountX + x];
                edgeZmax[x] = verts[topStart + lSeg * vertCountX + x];
            }
            Vector3[] edgeXmin = new Vector3[vertCountZ]; // x = -halfW
            Vector3[] edgeXmax = new Vector3[vertCountZ]; // x = +halfW
            for (int z = 0; z < vertCountZ; z++)
            {
                edgeXmin[z] = verts[topStart + z * vertCountX + 0];
                edgeXmax[z] = verts[topStart + z * vertCountX + wSeg];
            }

            BuildWall(edgeZmin, new Vector3(0, 0, -1), flip: true);
            BuildWall(edgeZmax, new Vector3(0, 0, 1), flip: false);
            BuildWall(edgeXmin, new Vector3(-1, 0, 0), flip: false);
            BuildWall(edgeXmax, new Vector3(1, 0, 0), flip: true);

            // ---------- 3) 바닥 ----------
            if (generateBottom)
            {
                int b0 = verts.Count;
                verts.Add(new Vector3(-halfW, bottomY, -halfL));
                verts.Add(new Vector3(halfW, bottomY, -halfL));
                verts.Add(new Vector3(-halfW, bottomY, halfL));
                verts.Add(new Vector3(halfW, bottomY, halfL));
                for (int i = 0; i < 4; i++)
                {
                    uvs.Add(new Vector2(0, 0));
                    normals.Add(Vector3.down);
                    colors.Add(new Color(0f, 0f, 0f)); // 바닥 고정
                }
                tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2);
                tris.Add(b0 + 1); tris.Add(b0 + 3); tris.Add(b0 + 2);
            }
        }

        _mesh.indexFormat = (verts.Count > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetNormals(normals);
        _mesh.SetColors(colors);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }
}

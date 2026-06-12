using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class FishSwimMesh : MonoBehaviour
{
    [Header("Swim Settings")]
    public float maxSwingAngle = 35f;
    public float frequency = 1.5f;
    public float waveSpeed = 4f;

    [Header("Region Weights")]
    [Range(0f, 1f)] public float headInfluence = 0.12f;
    [Range(0f, 1f)] public float midInfluence = 0.1f;
    [Range(0f, 1f)] public float tailInfluence = 0.2f;

    [Header("Mesh Range")]
    public float xMin = -3.75f;
    public float xMax = 2.0f;

    private Mesh _mesh;
    private Vector3[] _baseVerts;
    private float _xCenter;

    void Start()
    {
        var mf = GetComponent<MeshFilter>();
        _mesh = Instantiate(mf.sharedMesh);
        _mesh.name = "FishSwim_Runtime";
        mf.mesh = _mesh;
        _baseVerts = _mesh.vertices;
        _xCenter = (xMin + xMax) * 0.5f;
    }

    void Update()
    {
        DeformMesh(Time.time);
    }

    void DeformMesh(float t)
    {
        float xRange = xMax - xMin;
        if (xRange <= 0f) return;

        var verts = new Vector3[_baseVerts.Length];
        float omega = frequency * Mathf.PI * 2f;

        for (int i = 0; i < _baseVerts.Length; i++)
        {
            Vector3 v = _baseVerts[i];
            float tPos = (v.x - xMin) / xRange;

            float influence;
            if (tPos < 0.33f)
                influence = Mathf.Lerp(headInfluence, midInfluence, tPos / 0.33f);
            else
                influence = Mathf.Lerp(midInfluence, tailInfluence, (tPos - 0.33f) / 0.67f);

            float angle = Mathf.Sin(omega * t - tPos * waveSpeed) * maxSwingAngle * influence;
            float rad = angle * Mathf.Deg2Rad;
            float sinA = Mathf.Sin(rad);
            float cosA = Mathf.Cos(rad);

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

    void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }
}
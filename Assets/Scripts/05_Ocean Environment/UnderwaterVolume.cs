using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UnderwaterVolume : MonoBehaviour
{
    [SerializeField] Volume underwaterVolume;
    [SerializeField] Transform waterSurface;
    [SerializeField] float transitionRange = 0.5f;
    [SerializeField] float offsetY = 0.5f;

    Camera cam;

    void OnEnable()
    {
        cam = Camera.main;
#if UNITY_EDITOR
        EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying) return;
        Tick(SceneView.lastActiveSceneView?.camera);
    }
#endif

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        Tick(cam != null ? cam : Camera.main);
    }

    void Tick(Camera targetCam)
    {
        if (targetCam == null || waterSurface == null || underwaterVolume == null) return;

        float diff = waterSurface.position.y - targetCam.transform.position.y + offsetY;
        underwaterVolume.weight = Mathf.Clamp01(diff / Mathf.Max(0.001f, transitionRange));
    }
}
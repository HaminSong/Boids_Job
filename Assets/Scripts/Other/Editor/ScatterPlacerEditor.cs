using UnityEditor;
using UnityEngine;

/// <summary>
/// ScatterPlacer 인스펙터에 Scatter / Clear 버튼을 추가하고,
/// Scene 뷰에서 Attractor Points를 드래그로 옮기고 반경을 조절할 수 있게 한다.
/// 이 파일은 반드시 "Editor" 폴더 안에 위치해야 한다 (빌드에 포함되지 않음).
/// </summary>
[CustomEditor(typeof(ScatterPlacer))]
public class ScatterPlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ScatterPlacer placer = (ScatterPlacer)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scatter", GUILayout.Height(30)))
            {
                placer.Scatter();
            }

            if (GUILayout.Button("Clear", GUILayout.Height(30)))
            {
                placer.Clear();
            }
        }

        EditorGUILayout.HelpBox(
            "Scatter: 현재 설정으로 새로 배치 (기존 배치는 자동 삭제 후 재생성)\n" +
            "Clear: 배치된 오브젝트만 제거\n" +
            "Use Attractors가 켜져 있으면 Scene 뷰에서 주황 점을 드래그해 위치를, " +
            "바깥 원의 흰 핸들을 드래그해 반경을 조절할 수 있다.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        ScatterPlacer placer = (ScatterPlacer)target;

        if (!placer.useAttractors || placer.attractorPoints == null)
            return;

        Undo.RecordObject(placer, "Move Attractor");

        for (int i = 0; i < placer.attractorPoints.Length; i++)
        {
            Vector2 local = placer.attractorPoints[i];
            Vector3 worldPos = new Vector3(placer.origin.x + local.x, placer.origin.y, placer.origin.z + local.y);

            // --- 위치 드래그 핸들 (자유 이동 핸들, XZ 평면 제약) ---
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(1f, 0.5f, 0f, 1f);
            Vector3 newWorldPos = Handles.FreeMoveHandle(
                worldPos,
                HandleUtility.GetHandleSize(worldPos) * 0.15f,
                Vector3.zero,
                Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                // Y는 항상 origin.y로 고정 (XZ 평면 위에서만 이동)
                newWorldPos.y = placer.origin.y;
                placer.attractorPoints[i] = new Vector2(
                    newWorldPos.x - placer.origin.x,
                    newWorldPos.z - placer.origin.z);
            }

            // --- 반경 조절 핸들 (원 위의 점을 드래그) ---
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(1f, 0.5f, 0f, 0.6f);
            Vector3 radiusHandlePos = worldPos + Vector3.right * placer.attractorRadius;
            Vector3 newRadiusHandlePos = Handles.FreeMoveHandle(
                radiusHandlePos,
                HandleUtility.GetHandleSize(radiusHandlePos) * 0.1f,
                Vector3.zero,
                Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                float newRadius = Vector3.Distance(
                    new Vector3(worldPos.x, 0f, worldPos.z),
                    new Vector3(newRadiusHandlePos.x, 0f, newRadiusHandlePos.z));
                placer.attractorRadius = Mathf.Max(0.1f, newRadius);
            }

            Handles.Label(worldPos + Vector3.up * 0.5f, $"Attractor {i}");
        }

        if (GUI.changed || UnityEditor.Tools.current == Tool.Move)
        {
            EditorUtility.SetDirty(placer);
        }
    }
}
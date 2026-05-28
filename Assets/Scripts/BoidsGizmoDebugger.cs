using UnityEngine;

/// <summary>
/// Boids 동작(Separation / Alignment / Cohesion)을 기즈모 라인으로 시각화.
/// </summary>
public class BoidsGizmoDebugger : MonoBehaviour
{
    // -------------------------------------------------------
    // 열거형: 어떤 행동을 시각화할지 선택
    // -------------------------------------------------------
    public enum GizmoMode
    {
        None,           // 비활성화
        Separation,     // 분리 — 가까운 이웃과의 밀어냄 방향
        Alignment,      // 정렬 — 이웃 평균 속도 방향
        Cohesion,       // 응집 — 이웃 평균 위치(무게중심) 방향
        All             // 세 가지 모두 동시에 표시
    }

    [Header("References")]
    [Tooltip("시각화 대상 BoidsManager를 연결하세요.")]
    public BoidsManager boidsManager;

    [Header("Visualization")]
    [Tooltip("시각화할 Boids 행동을 선택합니다.")]
    public GizmoMode gizmoMode = GizmoMode.Separation;

    [Tooltip("성능 보호: 기즈모를 그릴 최대 Boid 수")]
    [Range(1, 200)]
    public int maxBoidsToVisualize = 50;

    [Tooltip("라인 길이 배율 (값이 클수록 라인이 길어집니다)")]
    public float lineScale = 1.0f;

    [Header("Colors")]
    public Color separationColor = new Color(1f, 0.3f, 0.3f, 1f);   // 빨강
    public Color alignmentColor  = new Color(0.3f, 1f, 0.3f, 1f);   // 초록
    public Color cohesionColor   = new Color(0.3f, 0.5f, 1f, 1f);   // 파랑

    [Header("Perception Circles")]
    [Tooltip("개별 Boid의 perceptionRadius 원을 표시합니다.")]
    public bool showPerceptionRadius = false;
    [Tooltip("개별 Boid의 separationRadius 원을 표시합니다.")]
    public bool showSeparationRadius = false;

    // -------------------------------------------------------
    // 리플렉션 없이 private 필드에 접근하기 위한 캐시
    // (BoidsManager 필드가 public이므로 직접 참조)
    // -------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (boidsManager == null) return;

        // private boids 배열은 SerializeField가 아니므로
        // BoidsManager의 public 프로퍼티(또는 아래 래퍼)로 접근합니다.
        // BoidsManager의 boids 필드가 private이기 때문에
        // FindObjectsByType 으로 씬의 Boid 전체를 가져옵니다.
        Boid[] allBoids = FindObjectsByType<Boid>(FindObjectsSortMode.None);
        if (allBoids == null || allBoids.Length == 0) return;

        int drawCount = Mathf.Min(allBoids.Length, maxBoidsToVisualize);

        float perceptionR  = boidsManager.perceptionRadius;
        float separationR  = boidsManager.separationRadius;
        float perceptionR2 = perceptionR * perceptionR;

        for (int i = 0; i < drawCount; i++)
        {
            Boid boidI = allBoids[i];
            if (boidI == null || !boidI.gameObject.activeInHierarchy) continue;

            Vector3 posI = boidI.transform.position;

            // ── 인식/분리 반경 원 ──────────────────────────
            if (showPerceptionRadius)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
                DrawWireCircle(posI, perceptionR);
            }
            if (showSeparationRadius)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.2f);
                DrawWireCircle(posI, separationR);
            }

            // ── 이웃 계산 ──────────────────────────────────
            Vector3 sep = Vector3.zero;
            Vector3 ali = Vector3.zero;
            Vector3 coh = Vector3.zero;
            int neighborCount = 0;

            for (int j = 0; j < allBoids.Length; j++)
            {
                if (i == j) continue;
                Boid boidJ = allBoids[j];
                if (boidJ == null) continue;

                Vector3 posJ    = boidJ.transform.position;
                Vector3 offset  = posI - posJ;
                float   sqrDist = offset.sqrMagnitude;

                if (sqrDist < perceptionR2 && sqrDist > 0f)
                {
                    float dist = Mathf.Sqrt(sqrDist);

                    coh += posJ;
                    ali += boidJ.velocity;

                    if (dist < separationR)
                        sep += offset / dist;

                    neighborCount++;
                }
            }

            if (neighborCount == 0) continue;

            // 최종 벡터 정규화 (BoidsManager와 동일한 방식)
            Vector3 cohDir = ((coh / neighborCount) - posI).normalized;
            Vector3 aliDir = (ali / neighborCount).normalized;
            Vector3 sepDir = sep.normalized;

            // ── 모드별 라인 그리기 ─────────────────────────
            switch (gizmoMode)
            {
                case GizmoMode.Separation:
                    DrawForceArrow(posI, sepDir, separationColor, lineScale);
                    break;

                case GizmoMode.Alignment:
                    DrawForceArrow(posI, aliDir, alignmentColor, lineScale);
                    break;

                case GizmoMode.Cohesion:
                    DrawForceArrow(posI, cohDir, cohesionColor, lineScale);
                    break;

                case GizmoMode.All:
                    DrawForceArrow(posI, sepDir, separationColor, lineScale);
                    DrawForceArrow(posI, aliDir, alignmentColor, lineScale);
                    DrawForceArrow(posI, cohDir, cohesionColor, lineScale);
                    break;

                case GizmoMode.None:
                default:
                    break;
            }
        }
    }
#endif

    // -------------------------------------------------------
    // 화살표 그리기 (메인 라인 + 작은 화살촉)
    // -------------------------------------------------------
    void DrawForceArrow(Vector3 origin, Vector3 dir, Color color, float scale)
    {
        if (dir == Vector3.zero) return;

        Gizmos.color = color;

        Vector3 end = origin + dir * scale;
        Gizmos.DrawLine(origin, end);

        // 화살촉: 끝점에서 역방향 + 수직 방향으로 짧은 선 두 개
        float  headLen  = scale * 0.25f;
        Vector3 right   = Vector3.Cross(dir, Vector3.up).normalized;
        if (right == Vector3.zero)
            right = Vector3.Cross(dir, Vector3.right).normalized;

        Vector3 headBase = end - dir * headLen;
        Gizmos.DrawLine(end, headBase + right  * headLen * 0.5f);
        Gizmos.DrawLine(end, headBase - right  * headLen * 0.5f);
    }

    // -------------------------------------------------------
    // XZ 평면 기준 원 (씬뷰 가독성용)
    // -------------------------------------------------------
    void DrawWireCircle(Vector3 center, float radius, int segments = 24)
    {
        float step = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int s = 1; s <= segments; s++)
        {
            float angle = s * step * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}

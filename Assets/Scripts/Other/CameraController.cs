using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [Header("이동 속도 설정")]
    public float moveSpeed = 50f;
    public float minMoveSpeed = 1f;      // 최소 속도 제한
    public float maxMoveSpeed = 100f;    // 최대 속도 제한
    public float speedScrollSensitivity = 10f; // 휠을 굴릴 때 속도 변화량

    public float shiftSpeedMultiplier = 2.5f; // Shift 누를 때 속도 배율

    [Header("회전 및 줌 속도")]
    public float lookSensitivity = 3f;
    public float zoomSensitivity = 10f;

    [Header("속도 알림 UI 캐싱 설정")]
    [Tooltip("생성할 속도 표기 UI 프리팹을 할당해주세요.")]
    public GameObject speedUiPrefab;
    public float uiDisplayTime = 1.0f;   // UI가 유지되는 시간 (초)
    public float uiFadeSpeed = 4f;       // UI가 사라지는 속도

    private float rotationX = 0f;
    private float rotationY = 0f;

    // 인스턴스화된 UI 컴포넌트 캐싱 변수
    private CanvasGroup speedUiCanvasGroup;
    private Text speedUiText;
    private float uiHideTimer = 0f;

    void Start()
    {
        // 시작할 때 현재 카메라의 회전값을 초기화
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.y;
        rotationY = rot.x;

        // UI 인스턴스 생성 및 컴포넌트 캐싱
        InitializeSpeedUI();
    }

    void Update()
    {
        // 마우스 휠 입력 먼저 감지
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        // 1. 회전 및 속도 조절 (마우스 우클릭 상태)
        if (Input.GetMouseButton(1))
        {
            // [우클릭 중] 마우스 휠을 굴리면 이동 속도(moveSpeed) 조절
            if (scroll != 0)
            {
                moveSpeed += scroll * speedScrollSensitivity;
                moveSpeed = Mathf.Clamp(moveSpeed, minMoveSpeed, maxMoveSpeed);

                // 속도 UI 갱신 및 표시 트리거 (나누지 않고 원래 값 그대로 표시)
                ShowSpeedUI(moveSpeed * 0.1f);
            }

            // 마우스 드래그로 회전
            rotationX += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f);

            transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0f);
        }
        // 2. 줌 인/아웃 (우클릭을 안 한 상태에서 휠을 굴릴 때)
        else if (scroll != 0)
        {
            transform.position += transform.forward * scroll * zoomSensitivity;
        }

        // 3. 키보드 이동 (Input.GetAxisRaw 활용으로 WASD + 화살표 키 자동 지원)
        float horizontal = Input.GetAxisRaw("Horizontal"); // A, D, Left, Right
        float vertical = Input.GetAxisRaw("Vertical");     // W, S, Up, Down

        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= shiftSpeedMultiplier; // Shift 부스트
        }

        // 카메라 방향 기준 이동 벡터 계산
        Vector3 moveDirection = (transform.forward * vertical) + (transform.right * horizontal);

        // Q, E로 수직 상승/하강 (World 축 기준)
        if (Input.GetKey(KeyCode.E)) moveDirection += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) moveDirection -= Vector3.up;

        transform.position += moveDirection.normalized * currentSpeed * Time.deltaTime;

        // 4. 화면 평행 이동 (마우스 휠 클릭 드래그)
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X") * moveSpeed * 0.1f;
            float mouseY = Input.GetAxis("Mouse Y") * moveSpeed * 0.1f;

            transform.position -= transform.right * mouseX + transform.up * mouseY;
        }

        // 5. UI 페이드아웃 로직 처리
        HandleUiFade();
    }

    /// <summary>
    /// 할당된 UI 프리팹을 하위에 띄우고 컴포넌트를 미리 캐싱합니다.
    /// </summary>
    private void InitializeSpeedUI()
    {
        if (speedUiPrefab == null)
        {
            Debug.LogWarning("Speed UI Prefab이 할당되지 않았습니다! CameraController 인스펙터를 확인해주세요.");
            return;
        }

        // UI 오브젝트 생성 및 카메라의 하위 자식으로 종속
        GameObject uiInstance = Instantiate(speedUiPrefab, this.transform);

        // 투명도 조절용 CanvasGroup 및 글자 표시용 Text 컴포넌트 캐싱
        speedUiCanvasGroup = uiInstance.GetComponentInChildren<CanvasGroup>();
        speedUiText = uiInstance.GetComponentInChildren<Text>();

        if (speedUiCanvasGroup == null)
        {
            // 프리팹 최상위나 자식에 CanvasGroup이 없으면 자동으로 추가해서 캐싱
            speedUiCanvasGroup = uiInstance.AddComponent<CanvasGroup>();
        }

        // 기본적으로 처음엔 숨김 상태로 시작
        speedUiCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 속도가 변경되었을 때 원래 값을 화면에 노출하고 UI 타이머 리셋
    /// </summary>
    private void ShowSpeedUI(float speed)
    {
        if (speedUiText == null || speedUiCanvasGroup == null) return;

        // 소수점 첫째 자리까지 표시
        speedUiText.text = speed.ToString("F2");

        speedUiCanvasGroup.alpha = 1f; // 즉시 활성화
        uiHideTimer = uiDisplayTime;   // 유지 타이머 초기화
    }

    /// <summary>
    /// 시간이 지남에 따라 자연스럽게 페이드아웃시키는 연출
    /// </summary>
    private void HandleUiFade()
    {
        if (speedUiCanvasGroup == null) return;

        if (uiHideTimer > 0f)
        {
            uiHideTimer -= Time.deltaTime;
        }
        else
        {
            // 타이머 종료 시 부드럽게 투명도를 낮춤
            if (speedUiCanvasGroup.alpha > 0f)
            {
                speedUiCanvasGroup.alpha = Mathf.MoveTowards(speedUiCanvasGroup.alpha, 0f, uiFadeSpeed * Time.deltaTime);
            }
        }
    }
}
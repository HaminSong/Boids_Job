using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("이동 속도 설정")]
    public float moveSpeed = 20f;
    public float minMoveSpeed = 1f;      // 최소 속도 제한
    public float maxMoveSpeed = 100f;    // 최대 속도 제한
    public float speedScrollSensitivity = 5f; // 휠을 굴릴 때 속도 변화량

    public float shiftSpeedMultiplier = 2.5f; // Shift 누를 때 속도 배율

    [Header("회전 및 줌 속도")]
    public float lookSensitivity = 3f;
    public float zoomSensitivity = 10f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // 시작할 때 현재 카메라의 회전값을 초기화
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.y;
        rotationY = rot.x;
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
                Debug.Log($"현재 카메라 이동 속도: {moveSpeed:F1}"); // 콘솔에서 속도 확인용
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
    }
}
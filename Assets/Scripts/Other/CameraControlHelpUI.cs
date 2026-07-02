using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카메라 조작법을 안내하는 토글형 UI.
/// 텍스트/아이콘 레이아웃은 씬에 직접 배치되어 있다고 가정하고,
/// 이 스크립트는 (1) 패널 표시/숨김 토글, (2) 각 아이콘의 눌림 강조만 담당.
/// </summary>
public class CameraControlHelpUI : MonoBehaviour
{
    /// <summary>
    /// CameraController에서 실제로 사용하는 입력만 모아둔 열거형.
    /// KeyCode 전체 목록 대신 이것만 노출해서 인스펙터에서 고르기 쉽게 함.
    /// </summary>
    public enum ControlInput
    {
        W, A, S, D,
        Q, E,
        Shift,
        H,
        MouseLeft,
        MouseRight,
        MouseMiddle,
        MouseWheel,
    }

    /// <summary>토글 키로 자주 쓰는 후보만 모아둔 열거형 (KeyCode 전체 목록 대신 사용)</summary>
    public enum ToggleKeyOption
    {
        H,
        F1,
        Tab,
        Backquote, // ` (물결/백틱)
    }

    [System.Serializable]
    public struct IconBinding
    {
        [Tooltip("이 아이콘이 반응할 입력")]
        public ControlInput input;

        [Tooltip("씬에 이미 배치되어 있는 아이콘 Image 컴포넌트")]
        public Image targetImage;

        public Sprite outlineSprite; // 기본(안 눌림) 상태
        public Sprite filledSprite;  // 눌림 강조 상태
    }

    [Header("토글 설정")]
    public ToggleKeyOption toggleKey = ToggleKeyOption.H;
    public bool startVisible = false;

    [Header("UI 페이드 설정")]
    public float fadeSpeed = 6f;

    [Header("아이콘 바인딩 (씬에 배치된 아이콘들을 여기에 하나씩 등록)")]
    public IconBinding[] iconBindings;

    private CanvasGroup canvasGroup;
    private bool isVisible;
    private float targetAlpha;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 시작 시 각 아이콘을 outline(기본) 상태로 초기화
        foreach (var binding in iconBindings)
        {
            if (binding.targetImage != null && binding.outlineSprite != null)
            {
                binding.targetImage.sprite = binding.outlineSprite;
            }
        }

        isVisible = startVisible;
        targetAlpha = isVisible ? 1f : 0f;
        canvasGroup.alpha = targetAlpha;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    void Update()
    {
        if (Input.GetKeyDown(ToggleKeyToKeyCode(toggleKey)))
        {
            Toggle();
        }

        if (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        }

        // 안내창이 보일 때만 눌림 상태를 갱신 (숨겨져 있을 땐 계산 스킵)
        if (isVisible)
        {
            RefreshIconHighlights();
        }
    }

    public void Toggle()
    {
        isVisible = !isVisible;
        targetAlpha = isVisible ? 1f : 0f;

        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    /// <summary>
    /// 실제 입력 상태를 확인해서 각 바인딩의 outline / filled 스프라이트를 교체.
    /// </summary>
    private void RefreshIconHighlights()
    {
        foreach (var binding in iconBindings)
        {
            if (binding.targetImage == null) continue;

            bool active = IsInputActive(binding.input);
            Sprite target = active ? binding.filledSprite : binding.outlineSprite;

            if (target != null && binding.targetImage.sprite != target)
            {
                binding.targetImage.sprite = target;
            }
        }
    }

    private bool IsInputActive(ControlInput input)
    {
        switch (input)
        {
            case ControlInput.W: return Input.GetKey(KeyCode.W);
            case ControlInput.A: return Input.GetKey(KeyCode.A);
            case ControlInput.S: return Input.GetKey(KeyCode.S);
            case ControlInput.D: return Input.GetKey(KeyCode.D);
            case ControlInput.Q: return Input.GetKey(KeyCode.Q);
            case ControlInput.E: return Input.GetKey(KeyCode.E);
            case ControlInput.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            case ControlInput.H: return Input.GetKey(KeyCode.H);
            case ControlInput.MouseLeft: return Input.GetMouseButton(0);
            case ControlInput.MouseRight: return Input.GetMouseButton(1);
            case ControlInput.MouseMiddle: return Input.GetMouseButton(2);
            case ControlInput.MouseWheel: return Input.GetAxis("Mouse ScrollWheel") != 0f;
            default: return false;
        }
    }

    private static KeyCode ToggleKeyToKeyCode(ToggleKeyOption option)
    {
        switch (option)
        {
            case ToggleKeyOption.H: return KeyCode.H;
            case ToggleKeyOption.F1: return KeyCode.F1;
            case ToggleKeyOption.Tab: return KeyCode.Tab;
            case ToggleKeyOption.Backquote: return KeyCode.BackQuote;
            default: return KeyCode.H;
        }
    }
}
using UnityEngine;

/// <summary>
/// Vector2 필드를 인스펙터에서 양쪽 핸들이 있는 Range 슬라이더로 표시하기 위한 attribute.
/// x = min, y = max로 사용한다 (예: uniformScaleRange.x = 최소 배율, .y = 최대 배율).
///
/// 사용 예:
/// [MinMaxSlider(0f, 10f)]
/// public Vector2 uniformScaleRange = new Vector2(0.8f, 1.4f);
///
/// 실제로 슬라이더 UI를 그리는 코드는 Editor/MinMaxSliderDrawer.cs에 있다 (에디터 전용).
/// </summary>
public class MinMaxSliderAttribute : PropertyAttribute
{
    public readonly float min;
    public readonly float max;

    public MinMaxSliderAttribute(float min, float max)
    {
        this.min = min;
        this.max = max;
    }
}

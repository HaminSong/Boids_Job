using UnityEditor;
using UnityEngine;

/// <summary>
/// [MinMaxSlider(min, max)] attribute가 붙은 Vector2 필드를 인스펙터에서
/// "숫자입력 - 슬라이더(양쪽 핸들) - 숫자입력" 형태로 그려준다.
///
/// 슬라이더 핸들을 드래그하면 min(x)/max(y)가 바뀌고,
/// 좌우의 숫자 필드를 직접 클릭해서 슬라이더 한계를 넘는 값도 입력할 수 있다.
/// </summary>
[CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
public class MinMaxSliderDrawer : PropertyDrawer
{
    private const float FieldWidth = 50f;
    private const float Spacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Vector2)
        {
            EditorGUI.LabelField(position, label.text, "MinMaxSlider는 Vector2 필드에만 사용 가능합니다.");
            return;
        }

        MinMaxSliderAttribute range = (MinMaxSliderAttribute)attribute;
        Vector2 value = property.vector2Value;

        EditorGUI.BeginProperty(position, label, property);

        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        Rect minFieldRect = new Rect(position.x, position.y, FieldWidth, position.height);
        Rect sliderRect = new Rect(
            minFieldRect.xMax + Spacing,
            position.y,
            position.width - (FieldWidth * 2f) - (Spacing * 2f),
            position.height);
        Rect maxFieldRect = new Rect(sliderRect.xMax + Spacing, position.y, FieldWidth, position.height);

        float min = value.x;
        float max = value.y;

        EditorGUI.BeginChangeCheck();

        min = EditorGUI.FloatField(minFieldRect, min);
        EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, range.min, range.max);
        max = EditorGUI.FloatField(maxFieldRect, max);

        if (EditorGUI.EndChangeCheck())
        {
            // 숫자 필드로 직접 입력할 때 min > max가 되는 것만 방지 (슬라이더 한계 자체는 넘어갈 수 있게 허용)
            if (min > max) max = min;
            property.vector2Value = new Vector2(min, max);
        }

        EditorGUI.EndProperty();
    }
}

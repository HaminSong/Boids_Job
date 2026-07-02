using UnityEngine;
using UnityEditor;

// OceanWave.shader 전용 커스텀 머티리얼 인스펙터.
// - 기존 [Header(...)]로 나열식으로 늘어져 있던 프로퍼티를 접고 펼 수 있는 그룹으로 정리
// - 각 프로퍼티에 마우스를 올리면 무슨 역할인지 설명하는 툴팁 표시
//   (ShaderLab Properties 블록 자체는 [Tooltip] 어트리뷰트를 지원하지 않아 커스텀 에디터로 처리)
//
// 사용법: 이 파일을 프로젝트의 "Editor" 폴더 아래에 넣고,
//         OceanWave.shader의 Properties 블록 밑에 CustomEditor "OceanWaveShaderGUI" 한 줄 추가.
public class OceanWaveShaderGUI : ShaderGUI
{
    private struct PropInfo
    {
        public readonly string name;
        public readonly string tooltip;
        public PropInfo(string name, string tooltip)
        {
            this.name = name;
            this.tooltip = tooltip;
        }
    }

    private struct PropGroup
    {
        public readonly string title;
        public readonly string sessionKey;
        public readonly PropInfo[] props;
        public PropGroup(string title, PropInfo[] props)
        {
            this.title = title;
            this.props = props;
            this.sessionKey = "OceanWaveShaderGUI.Foldout." + title;
        }
    }

    // 그룹 제목 = 원래 [Header(...)]로 나눠져 있던 카테고리와 동일
    private static readonly PropGroup[] Groups =
    {
        new PropGroup("Wave Control", new[]
        {
            new PropInfo("_WaveSpeed", "전역 파도 진행 속도. Gerstner 위상 f = k·(dot(d,pos) - c·time)의 time에 곱해짐"),
            new PropInfo("_WaveRoughness", "파도 표면 노이즈 왜곡(도메인 워프) 강도. 값이 클수록 규칙적인 파형이 더 흐트러짐"),
            new PropInfo("_WaveHeightScale", "전체 파고(Y축 변위) 배율"),
            new PropInfo("_WaveSteepnessScale", "파도 뾰족함(steepness) 배율. 과하게 올리면 파도 마루가 겹치는 self-intersection 발생 가능"),
            new PropInfo("_ActiveWaveCount", "합성할 Gerstner 파도 개수 (최대 6). 줄일수록 정점/픽셀 연산량이 줄어 성능에 직접 영향"),
        }),
        new PropGroup("Local Transform Correction", new[]
        {
            new PropInfo("_LocalWaveScale", "파도 연산 전 로컬 XZ 좌표에 곱하는 스케일. 오브젝트 자체 스케일이 1이 아닐 때 파장이 뒤틀리는 걸 보정"),
            new PropInfo("_LocalHeightCorrection", "_LocalWaveScale로 XZ를 스케일링한 만큼 Y 변위도 같이 보정하는 계수"),
            new PropInfo("_NoiseSpaceScale", "마이크로 노멀맵 UV 타일링에 곱해지는 노이즈 공간 스케일 보정값"),
        }),
        new PropGroup("Water Color", new[]
        {
            new PropInfo("_ShallowColor", "얕은 물 색상 (수심차가 0에 가까울 때). 알파값이 얕은 물의 투명도로 사용됨"),
            new PropInfo("_DeepColor", "깊은 물 색상 (수심차가 Depth Fade Distance 이상일 때). 알파값이 깊은 물(+수중 시야)의 투명도로 사용됨"),
            new PropInfo("_DepthFadeDistance", "얕은 색 -> 깊은 색 전환이 끝나는 수심 거리"),
            new PropInfo("_DepthAlphaFade", "수심에 따라 Shallow/Deep Color 알파값 사이를 보간하는 곡선의 거듭제곱 지수"),
        }),
        new PropGroup("Wall Cross Section", new[]
        {
            new PropInfo("_WallTopColor", "측벽(단면) 상단 색상"),
            new PropInfo("_WallBottomColor", "측벽(단면) 하단 색상"),
            new PropInfo("_WallDepthPower", "측벽 상->하 색상 그라데이션의 거듭제곱 지수"),
            new PropInfo("_WallAlpha", "측벽 바깥면(Front Face) 알파"),
            new PropInfo("_WallInnerAlpha", "측벽 안쪽면(단면 내부, Back Face) 알파"),
        }),
        new PropGroup("Foam Detail", new[]
        {
            new PropInfo("_FoamColor", "거품 색상"),
            new PropInfo("_FoamDistance", "해안선 거품이 생기는 수심 거리. 0이면 해안 거품 비활성화"),
            new PropInfo("_CrestFoamThreshold", "파도 마루 거품이 생기기 시작하는 높이 임계값"),
            new PropInfo("_CrestFoamRange", "마루 거품 임계값 주변의 부드러운 전환 범위"),
        }),
        new PropGroup("Cast Shadow", new[]
        {
            new PropInfo("_CastShadows", "그림자 캐스팅 On/Off"),
        }),
        new PropGroup("Surface Specular", new[]
        {
            new PropInfo("_Smoothness", "수면 스무스니스. 스펙큘러 세기와 프레넬 블렌드 강도에 영향"),
            new PropInfo("_SpecularSharpness", "태양 반사 하이라이트가 얼마나 작고 쨍하게 뭉치는지 조절. 키우면 작고 날카로운 점광, 줄이면 크고 부드러운 광원이 됨"),
            new PropInfo("_SpecularIntensity", "스펙큘러 하이라이트 밝기 배율. Sharpness/Smoothness는 그대로 두고 밝기만 따로 줄이거나 키울 때 사용 (반짝이는 점들이 뭉쳐서 큰 덩어리로 보일 때 낮추면 도움됨)"),
            new PropInfo("_FresnelPower", "프레넬 효과(가장자리 반사) 거듭제곱 지수"),
            new PropInfo("_NormalMap", "마이크로 디테일 노멀맵. 서로 다른 속도로 스크롤되는 두 레이어를 블렌드해서 사용. Tiling/Offset 필드로 UV 반복/오프셋 조절"),
            new PropInfo("_NormalStrength", "디테일 노멀맵이 원래 지오메트리 노멀에 섞이는 강도"),
        }),
    };

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        foreach (PropGroup group in Groups)
        {
            bool expanded = SessionState.GetBool(group.sessionKey, true);

            EditorGUILayout.Space(4);
            bool newExpanded = EditorGUILayout.Foldout(expanded, group.title, true, EditorStyles.foldoutHeader);
            if (newExpanded != expanded)
                SessionState.SetBool(group.sessionKey, newExpanded);

            if (!newExpanded)
                continue;

            EditorGUI.indentLevel++;
            foreach (PropInfo info in group.props)
            {
                MaterialProperty prop = FindProperty(info.name, properties, false);
                if (prop == null)
                    continue; // 셰이더에서 프로퍼티가 삭제/이름 변경된 경우 조용히 스킵 (인스펙터가 깨지지 않도록)

                GUIContent label = new GUIContent(prop.displayName, info.tooltip);
                materialEditor.ShaderProperty(prop, label);
                // 참고: 텍스처 타입 프로퍼티는 ShaderProperty() 호출 하나로 Tiling/Offset까지 같이 그려짐
                // (NoScaleOffset 어트리뷰트가 없는 한). 여기서 TextureScaleOffsetProperty를 또 호출하면
                // Tiling/Offset이 중복으로 두 번 나타나므로 절대 추가로 호출하지 말 것.
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
    }
}

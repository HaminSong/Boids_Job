Shader "Custom/FishSwim"
{
    Properties
    {
        [Header(Textures)]
        _BaseMap        ("Diffuse",        2D)         = "white" {}  // 베이스 텍스처
        _BaseColor      ("Color Tint",     Color)      = (1,1,1,1)   // 색상 틴트
        _MetallicMap    ("Metallic Map",   2D)         = "black" {}  // 메탈릭 맵
        _MetallicScale  ("Metallic Scale", Range(0,1)) = 1.0         // 메탈릭 강도
        _Smoothness     ("Smoothness",     Range(0,1)) = 0.5         // 표면 매끄러움

        [Header(Swim Settings)]
        _MaxSwingAngle  ("Max Swing Angle", Float)      = 75.0  // 꼬리 최대 흔들림 각도 (도)
        _Frequency      ("Frequency",       Float)      = 1.5    // 유영 주기 (Hz), 클수록 빠르게 흔들림
        _WaveSpeed      ("Wave Speed",      Float)      = 4.0    // 파동이 머리에서 꼬리로 전달되는 속도
        _PhaseScale     ("Phase Scale",     Float)      = 0.3    // 위상 오프셋 배율, 클수록 개체별 타이밍 차이가 커짐

        [Header(Region Weights)]
        _HeadInfluence  ("Head Influence",  Range(0,1)) = 0.05  // 머리 구역 흔들림 강도
        _MidInfluence   ("Mid Influence",   Range(0,1)) = 0.05  // 중간 구역 흔들림 강도
        _TailInfluence  ("Tail Influence",  Range(0,1)) = 0.2   // 꼬리 구역 흔들림 강도

        [Header(Mesh Range)]
        _XMin           ("X Min", Float) = -2.75  // 메시 X축 최솟값, Inspector에서 직접 입력
        _XMax           ("X Max", Float) =  2.5  // 메시 X축 최댓값, Inspector에서 직접 입력
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FishSwimForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FishSwimShadowPass.hlsl"
            ENDHLSL
        }
    }
}

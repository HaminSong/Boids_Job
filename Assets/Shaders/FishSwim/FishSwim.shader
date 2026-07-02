Shader "Custom/FishSwim"
{
    Properties
    {
        [Header(Textures)]
        _BaseMap        ("Diffuse",        2D)         = "white" {}
        _BaseColor      ("Color Tint",     Color)      = (1,1,1,1)
        _MetallicMap    ("Metallic Map",   2D)         = "black" {}  // R채널 사용
        _MetallicScale  ("Metallic Scale", Range(0,1)) = 1.0
        _Smoothness     ("Smoothness",     Range(0,1)) = 0.5

        [Header(Swim Settings)]
        _MaxSwingAngle  ("Max Swing Angle", Float) = 75.0
        _Frequency      ("Frequency",       Float) = 1.5
        _WaveSpeed      ("Wave Speed",      Float) = 4.0
        _PhaseScale     ("Phase Scale",     Float) = 0.3

        [Header(Region Weights)]
        _HeadInfluence  ("Head Influence", Range(0,1)) = 0.05
        _MidInfluence   ("Mid Influence",  Range(0,1)) = 0.05
        _TailInfluence  ("Tail Influence", Range(0,1)) = 0.2

        [Header(Mesh Range)]
        _XMin ("X Min", Float) = -2.75
        _XMax ("X Max", Float) =  2.5
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

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            // 안개
            #pragma multi_compile_fog

            // 조명 및 그림자
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX
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

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            // Point/Spot Light 그림자 지원
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FishSwimShadowPass.hlsl"
            ENDHLSL
        }
    }
}

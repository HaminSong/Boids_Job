Shader "Custom/FishSwim"
{
    Properties
    {
        [Header(Textures)]
        _BaseMap        ("Diffuse",        2D)         = "white" {}
        _BaseColor      ("Color Tint",     Color)      = (1,1,1,1)
        _MetallicMap    ("Metallic Map",   2D)         = "black" {}
        _MetallicScale  ("Metallic Scale", Range(0,1)) = 1.0
        _Smoothness     ("Smoothness",     Range(0,1)) = 0.5

        [Header(Swim Settings)]
        _MaxSwingAngle  ("Max Swing Angle", Float)      = 35.0
        _Frequency      ("Frequency",       Float)      = 1.5
        _WaveSpeed      ("Wave Speed",      Float)      = 4.0

        [Header(Region Weights)]
        _HeadInfluence  ("Head Influence",  Range(0,1)) = 0.12
        _MidInfluence   ("Mid Influence",   Range(0,1)) = 0.1
        _TailInfluence  ("Tail Influence",  Range(0,1)) = 0.2

        [Header(Mesh Range)]
        _XMin           ("X Min", Float) = -3.75
        _XMax           ("X Max", Float) =  2.0
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MetallicMap_ST;
                float4 _BaseColor;
                float  _MetallicScale;
                float  _Smoothness;
                float  _MaxSwingAngle;
                float  _Frequency;
                float  _WaveSpeed;
                float  _HeadInfluence;
                float  _MidInfluence;
                float  _TailInfluence;
                float  _XMin;
                float  _XMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 SwimDeform(float3 posOS, float3 worldPos)
            {
                float xRange = _XMax - _XMin;
                if (xRange <= 0) return posOS;

                float tPos = saturate((posOS.x - _XMin) / xRange);

                float influence;
                if (tPos < 0.33)
                    influence = lerp(_HeadInfluence, _MidInfluence, tPos / 0.33);
                else
                    influence = lerp(_MidInfluence, _TailInfluence, (tPos - 0.33) / 0.67);

                float phase = dot(worldPos, float3(1.3, 2.7, 0.9)) * 0.05;
                float angle = sin(_Frequency * 6.28318 * _Time.y - tPos * _WaveSpeed - phase)
                              * _MaxSwingAngle * influence;
                float rad = angle * 0.01745329;

                // 기준점: 메시 중앙 (머리 고정 문제 해결)
                float xCenter = (_XMin + _XMax) * 0.5;
                float dx = posOS.x - xCenter;
                float dz = posOS.z;

                return float3(
                    xCenter + dx * cos(rad) - dz * sin(rad),
                    posOS.y,
                    dx * sin(rad) + dz * cos(rad)
                );
            }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 worldPos = mul(UNITY_MATRIX_M, float4(0,0,0,1)).xyz;
                float3 deformed = SwimDeform(IN.positionOS.xyz, worldPos);

                VertexPositionInputs posInputs = GetVertexPositionInputs(deformed);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS  = posInputs.positionCS;
                OUT.positionWS   = posInputs.positionWS;
                OUT.normalWS     = nrmInputs.normalWS;
                OUT.tangentWS    = nrmInputs.tangentWS;
                OUT.bitangentWS  = nrmInputs.bitangentWS;
                OUT.uv           = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half metallic   = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, IN.uv).r
                                  * _MetallicScale;

                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalize(IN.normalWS);
                inputData.viewDirectionWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI         = SampleSH(normalize(IN.normalWS));

                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = baseColor.rgb;
                surface.alpha      = 1.0;
                surface.metallic   = metallic;
                surface.smoothness = _Smoothness;
                surface.occlusion  = 1.0;
                surface.normalTS   = half3(0, 0, 1);

                return UniversalFragmentPBR(inputData, surface);
            }
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MetallicMap_ST;
                float4 _BaseColor;
                float  _MetallicScale;
                float  _Smoothness;
                float  _MaxSwingAngle;
                float  _Frequency;
                float  _WaveSpeed;
                float  _HeadInfluence;
                float  _MidInfluence;
                float  _TailInfluence;
                float  _XMin;
                float  _XMax;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            float3 SwimDeform(float3 posOS, float3 worldPos)
            {
                float xRange = _XMax - _XMin;
                if (xRange <= 0) return posOS;
                float tPos = saturate((posOS.x - _XMin) / xRange);
                float influence = tPos < 0.33
                    ? lerp(0.05, 0.45, tPos / 0.33)
                    : lerp(0.45, 1.0,  (tPos - 0.33) / 0.67);
                float phase  = dot(worldPos, float3(1.3, 2.7, 0.9)) * 0.05;
                float angle  = sin(_Frequency * 6.28318 * _Time.y - tPos * _WaveSpeed - phase)
                               * _MaxSwingAngle * influence;
                float rad    = angle * 0.01745329;
                float xCenter = (_XMin + _XMax) * 0.5;
                float dx = posOS.x - xCenter;
                float dz = posOS.z;
                return float3(xCenter + dx*cos(rad) - dz*sin(rad), posOS.y, dx*sin(rad) + dz*cos(rad));
            }

            Varyings vertShadow(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 worldPos = mul(UNITY_MATRIX_M, float4(0,0,0,1)).xyz;
                float3 deformed = SwimDeform(IN.positionOS.xyz, worldPos);
                float3 posWS    = mul(UNITY_MATRIX_M, float4(deformed, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_M, IN.normalOS));
                float  scale    = (1.0 - saturate(dot(_LightDirection, normalWS))) * 0.001;
                posWS          += normalWS * scale;
                OUT.positionHCS = mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}

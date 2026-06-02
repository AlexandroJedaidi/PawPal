Shader "PawFriends/Flat Foliage Cutout"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.55, 0.62, 0.55, 1)
        _CutoffMap ("Alpha Map", 2D) = "white" {}
        _Cutoff ("Alpha Clip Threshold", Range(0, 1)) = 0.333
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            TEXTURE2D(_CutoffMap);
            SAMPLER(sampler_CutoffMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _CutoffMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _CutoffMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_CutoffMap, sampler_CutoffMap, input.uv).a;
                clip(alpha - _Cutoff);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half lambert = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 litColor = _BaseColor.rgb * (ambient + lambert * mainLight.color * mainLight.shadowAttenuation);
                return half4(litColor, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

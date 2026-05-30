Shader "PawPal/Solid Blue Skybox"
{
    Properties
    {
        _Tint ("Sky Tint", Color) = (0.42, 0.70, 1.00, 1.00)
        _HorizonTint ("Horizon Tint", Color) = (0.72, 0.88, 1.00, 1.00)
        _Exposure ("Exposure", Range(0, 8)) = 1.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            fixed4 _HorizonTint;
            half _Exposure;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float horizon = saturate(normalize(input.direction).y * 0.5 + 0.5);
                fixed3 color = lerp(_HorizonTint.rgb, _Tint.rgb, horizon) * _Exposure;
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}

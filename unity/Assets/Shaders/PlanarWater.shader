Shader "Poketato/PlanarWater"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _Color ("Color", Color) = (0.15, 0.35, 0.4, 1)
        _Strength ("Strength", Range(0, 1)) = 0.65
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            float4 _Color;
            float _Strength;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                half4 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv);
                return lerp(_Color, reflection, _Strength);
            }
            ENDHLSL
        }
    }
}

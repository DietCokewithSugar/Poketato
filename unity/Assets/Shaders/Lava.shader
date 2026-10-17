// Animated emissive lava for poke-rouge's "熔岩池" field. HDR output so it feeds bloom.
Shader "Poketato/Lava"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Tiling ("World Tiling", Float) = 0.18
        _Speed ("Flow Speed", Float) = 0.04
        _CrustColor ("Crust Color", Color) = (0.12, 0.03, 0.02, 1)
        [HDR] _HotColor ("Hot Color", Color) = (4.0, 1.1, 0.15, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (7.0, 3.2, 0.6, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _NoiseTex_ST;
            float _Tiling;
            float _Speed;
            half4 _CrustColor;
            half4 _HotColor;
            half4 _GlowColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : TEXCOORD1; half fogFactor : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 uv = input.positionWS.xz * _Tiling;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv + t * _Speed * float2(1, 0.4)).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 1.9 - t * _Speed * float2(0.3, 1.1)).r;
                half h = saturate(n1 * 0.6h + n2 * 0.6h - 0.1h);
                half crust = smoothstep(0.46h, 0.6h, h);
                half pulse = 0.75h + 0.25h * sin(t * 2.3 + h * 9.0);
                half3 hot = lerp(_HotColor.rgb, _GlowColor.rgb, saturate((0.5h - h) * 3.0h)) * pulse;
                // Vertex alpha darkens the shore ring so the pool blends into the ground.
                half3 color = lerp(hot, _CrustColor.rgb, max(crust, 1.0h - input.color.a));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}

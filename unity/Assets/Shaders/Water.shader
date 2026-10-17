// Shallow water / puddle surface. Uses the planar reflection texture rendered by PlanarReflection.cs when the
// _PLANAR_REFLECTION global keyword is on, otherwise falls back to the reflection probe.
Shader "Poketato/Water"
{
    Properties
    {
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 0.6
        _Tiling ("World Tiling", Float) = 0.35
        _Speed ("Flow Speed", Float) = 0.06
        _ShallowColor ("Shallow Color", Color) = (0.32, 0.62, 0.72, 0.55)
        _DeepColor ("Deep Color", Color) = (0.08, 0.22, 0.34, 1)
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.85
        _Distortion ("Reflection Distortion", Range(0, 0.1)) = 0.025
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _Specular ("Specular", Range(0, 8)) = 2.5
        _SpecPower ("Specular Power", Range(8, 512)) = 160
        _RippleStrength ("Rain Ripple Strength", Range(0, 2)) = 0.7
        _RippleScale ("Rain Ripple Scale", Float) = 1.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _PLANAR_REFLECTION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_PlanarReflectionTex); SAMPLER(sampler_PlanarReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NormalMap_ST;
                half _NormalScale;
                float _Tiling;
                float _Speed;
                half4 _ShallowColor;
                half4 _DeepColor;
                half _ReflectionStrength;
                half _Distortion;
                half _FresnelPower;
                half _Specular;
                half _SpecPower;
                half _RippleStrength;
                float _RippleScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float2 Hash22(float2 p)
            {
                float3 a = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                a += dot(a, a.yzx + 33.33);
                return frac((a.xx + a.yz) * a.zy);
            }

            // Expanding rain rings, one per grid cell with a random phase. Returns a tangent-space xy offset.
            float2 RainRipples(float2 p, float t)
            {
                float2 cell = floor(p);
                float2 acc = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 c = cell + float2(x, y);
                    float2 h = Hash22(c);
                    float phase = frac(t * 0.7 + h.x);
                    float2 d = p - (c + 0.2 + 0.6 * h);
                    float r = length(d);
                    float ring = sin(saturate(r / max(phase, 1e-3)) * 9.0) ;
                    float w = (1 - phase) * (1 - phase) * saturate(1 - abs(r - phase * 0.8) * 6.0);
                    acc += (d / max(r, 1e-3)) * ring * w;
                }
                return acc;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 uvA = input.positionWS.xz * _Tiling + t * _Speed * float2(1.0, 0.6);
                float2 uvB = input.positionWS.xz * _Tiling * 1.37 - t * _Speed * float2(0.7, 1.0);
                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), _NormalScale);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), _NormalScale);
                float2 ripple = RainRipples(input.positionWS.xz * _RippleScale, t) * _RippleStrength;
                half3 nTS = normalize(half3(nA.xy + nB.xy + ripple, nA.z * nB.z));
                half3 n = normalize(half3(nTS.x, nTS.z, nTS.y));

                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(n, v)), _FresnelPower);
                fresnel = lerp(0.35h, 1.0h, fresnel);

            #if defined(_PLANAR_REFLECTION)
                float2 suv = input.screenPos.xy / input.screenPos.w;
                float2 ruv = float2(1.0 - suv.x, suv.y) + n.xz * _Distortion;
                half3 refl = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, ruv).rgb;
            #else
                half3 refl = GlossyEnvironmentReflection(reflect(-v, n), 0.08h, 1.0h);
            #endif

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = mainLight.shadowAttenuation;
                half3 h = normalize(mainLight.direction + v);
                half spec = pow(saturate(dot(n, h)), _SpecPower) * _Specular * shadow;

                half3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, 0.5h + 0.5h * nTS.y);
                body *= mainLight.color * (0.35h + 0.65h * shadow) * saturate(mainLight.direction.y + 0.2h) + SampleSH(n);

                half3 color = lerp(body, refl, fresnel * _ReflectionStrength) + spec * mainLight.color;
                half alpha = lerp(_ShallowColor.a, 1.0h, fresnel * _ReflectionStrength) * input.color.a;
                color = MixFog(color, input.fogFactor);
                return half4(color, saturate(alpha + spec * 0.5h));
            }
            ENDHLSL
        }
    }
    FallBack Off
}

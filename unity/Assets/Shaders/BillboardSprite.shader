// Pixel-art sprite drawn on an upright quad inside the 3D scene (HD-2D style).
// Lit by the main light (wrapped diffuse) and additional lights, casts and receives real-time shadows.
// One material per species atlas; the current animation frame comes from the per-instance _UVRect so
// every instance of a species can be GPU-instanced.
Shader "Poketato/BillboardSprite"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Atlas", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.6
        _UpTilt ("Normal Up Tilt", Range(0, 2)) = 0.8
        _AmbientBoost ("Ambient Boost", Range(0, 3)) = 1.1
        _ShadowReceiveOffset ("Shadow Receive Offset", Range(0, 1)) = 0.25
        _UVRect ("UV Rect (instanced)", Vector) = (0, 0, 1, 1)
        _Tint ("Tint (instanced)", Color) = (1, 1, 1, 1)
        _Flash ("Flash / Emission (instanced)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half _Cutoff;
            half _Wrap;
            half _UpTilt;
            half _AmbientBoost;
            half _ShadowReceiveOffset;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(SpriteProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _UVRect)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Flash)
        UNITY_INSTANCING_BUFFER_END(SpriteProps)

        float2 FrameUV(float2 uv)
        {
            float4 r = UNITY_ACCESS_INSTANCED_PROP(SpriteProps, _UVRect);
            return r.xy + uv * r.zw;
        }

        half SpriteAlpha(float2 uv)
        {
            half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            return a * UNITY_ACCESS_INSTANCED_PROP(SpriteProps, _Tint).a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = FrameUV(input.uv);
                half3 n = TransformObjectToWorldNormal(float3(0, 0, -1));
                o.normalWS = normalize(n + half3(0, _UpTilt, 0));
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half WrapDiffuse(half3 n, half3 l)
            {
                return saturate((dot(n, l) + _Wrap) / (1.0h + _Wrap));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 tint = UNITY_ACCESS_INSTANCED_PROP(SpriteProps, _Tint);
                half4 flash = UNITY_ACCESS_INSTANCED_PROP(SpriteProps, _Flash);
                clip(tex.a * tint.a - _Cutoff);

                half3 albedo = tex.rgb * tint.rgb;
                half3 n = normalize(input.normalWS);

                // The quad is part of the shadow map itself; sample a little towards the light to avoid self-shadowing.
                float3 receivePos = input.positionWS + _MainLightPosition.xyz * _ShadowReceiveOffset;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(receivePos));
                half3 light = mainLight.color * (WrapDiffuse(n, mainLight.direction) * mainLight.shadowAttenuation);
                light += SampleSH(n) * _AmbientBoost;

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    light += l.color * (l.distanceAttenuation * l.shadowAttenuation * WrapDiffuse(n, l.direction));
                LIGHT_LOOP_END
            #endif

                half3 color = albedo * light;
                color = lerp(color, half3(1, 1, 1) * (1.0h + flash.y), saturate(flash.x));
                color += albedo * flash.y;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                // Flip the normal towards the light so the bias pushes the right way for a two-sided quad.
                normalWS *= sign(dot(normalWS, lightDir) + 1e-4);
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
                o.uv = FrameUV(input.uv);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SpriteAlpha(input.uv) - _Cutoff);
                return 0;
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
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = FrameUV(input.uv);
                return o;
            }

            half frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SpriteAlpha(input.uv) - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = FrameUV(input.uv);
                o.normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SpriteAlpha(input.uv) - _Cutoff);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

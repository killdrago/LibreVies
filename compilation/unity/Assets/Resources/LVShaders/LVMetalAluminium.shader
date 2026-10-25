// Aluminium brosse pour les portes d'edition, version URP.
// Reste distinct du materiau Metal des decorations : rendu clair, metallique, legerement brosse.
Shader "LibreVies/MetalAluminium"
{
    Properties
    {
        _Color ("Teinte aluminium", Color) = (0.72,0.76,0.80,1)
        _MainTex ("Texture metal", 2D) = "white" {}
        _Tiling ("Echelle du brossage", Float) = 0.30
        _Metallic ("Metallic", Range(0,1)) = 0.95
        _Smoothness ("Brillance", Range(0,1)) = 0.90
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_ST;
                float _Tiling;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float3 normal = normalize(input.normalWS);
                float echelle = max(_Tiling, 0.01);
                half3 texture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * echelle).rgb;
                float brossage = sin((p.x + p.y * 0.35) * 38.0) * 0.035;
                half3 albedo = saturate(texture * _Color.rgb * (1.0 + brossage));

                float4 shadowCoord = TransformWorldToShadowCoord(p);
                Light mainLight = GetMainLight(shadowCoord);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(p));
                float3 halfDirection = normalize(mainLight.direction + viewDirection);
                float diffuse = saturate(dot(normal, mainLight.direction));
                float smoothness = saturate(_Smoothness);
                float specular = pow(saturate(dot(normal, halfDirection)), lerp(8.0, 160.0, smoothness));
                half3 ambient = SampleSH(normal) + 0.15;
                half3 lumiere = mainLight.color * mainLight.shadowAttenuation;

                // Metal : peu de diffus, reflet colore (teinte de l'albedo).
                half3 diffus = albedo * (ambient * 0.75 + lumiere * diffuse * 0.72) * (1.0 - _Metallic * 0.6);
                half3 reflet = albedo * lumiere * specular * _Metallic * (0.6 + smoothness * 0.6);
                return half4(diffus + reflet, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

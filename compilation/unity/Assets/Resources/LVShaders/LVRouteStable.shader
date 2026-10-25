// Rendu PBR leger pour le monde, version URP (Universal Render Pipeline).
// Triplanaire en coordonnees monde, relief (normale) et rugosite optionnels.
// Les cartes plates (normale neutre, rugosite nulle) ne changent pas le rendu.
Shader "LibreVies/StablePBR"
{
    Properties
    {
        _Color ("Teinte", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Tiling ("Echelle monde", Float) = 0.28
        _Defilement ("Defilement (eau)", Vector) = (0,0,0,0)
        _Metallic ("Metallic", Range(0,1)) = 0.05
        _Smoothness ("Brillance", Range(0,1)) = 0.28
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _BumpMap ("Relief (normale)", 2D) = "bump" {}
        _BumpStrength ("Relief", Range(0,2)) = 0
        _RoughMap ("Rugosite", 2D) = "white" {}
        _RoughStrength ("Force rugosite", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

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

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughMap); SAMPLER(sampler_RoughMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Tiling;
                float4 _Defilement;
                float _Metallic;
                float _Smoothness;
                half4 _EmissionColor;
                float _BumpStrength;
                float _RoughStrength;
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                float4 _RoughMap_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                return output;
            }

            half4 SampleTriplanar(float3 position, float3 normal)
            {
                float scale = max(_Tiling, 0.001);
                float3 poids = abs(normal);
                poids /= max(poids.x + poids.y + poids.z, 0.001);
                half4 x = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, position.zy * scale + _Defilement.xy);
                half4 y = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, position.xz * scale + _Defilement.xy);
                half4 z = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, position.xy * scale + _Defilement.xy);
                return x * poids.x + y * poids.y + z * poids.z;
            }

            // Normale triplanaire : la carte de relief est projetee sur les trois axes
            // puis ramenee dans l'espace du monde.
            float3 NormaleTriplanaire(float3 position, float3 normal)
            {
                float scale = max(_Tiling, 0.001);
                float3 poids = abs(normal);
                poids /= max(poids.x + poids.y + poids.z, 0.001);
                float3 tX = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, position.zy * scale).rgb * 2.0 - 1.0;
                float3 tY = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, position.xz * scale).rgb * 2.0 - 1.0;
                float3 tZ = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, position.xy * scale).rgb * 2.0 - 1.0;
                float sX = normal.x >= 0.0 ? 1.0 : -1.0;
                float sY = normal.y >= 0.0 ? 1.0 : -1.0;
                float sZ = normal.z >= 0.0 ? 1.0 : -1.0;
                float3 nX = float3(tX.z * sX, tX.y, tX.x);
                float3 nY = float3(tY.x, tY.z * sY, tY.y);
                float3 nZ = float3(tZ.x, tZ.y, tZ.z * sZ);
                return normalize(nX * poids.x + nY * poids.y + nZ * poids.z);
            }

            // Rugosite triplanaire (0 = lisse, 1 = rugueux).
            float RugositeTriplanaire(float3 position, float3 normal)
            {
                float scale = max(_Tiling, 0.001);
                float3 poids = abs(normal);
                poids /= max(poids.x + poids.y + poids.z, 0.001);
                float rX = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, position.zy * scale).r;
                float rY = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, position.xz * scale).r;
                float rZ = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, position.xy * scale).r;
                return rX * poids.x + rY * poids.y + rZ * poids.z;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalGeometrique = normalize(input.normalWS);
                float3 normal = normalGeometrique;
                float smoothness = _Smoothness;
                if (_BumpStrength > 0.001)
                    normal = normalize(lerp(normal, NormaleTriplanaire(input.positionWS, normalGeometrique), saturate(_BumpStrength * 4.0)));
                if (_RoughStrength > 0.001)
                    smoothness = lerp(_Smoothness, 1.0 - RugositeTriplanaire(input.positionWS, normalGeometrique), _RoughStrength);

                half3 albedo = SampleTriplanar(input.positionWS, normalGeometrique).rgb * _Color.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 lightDirection = mainLight.direction;
                float3 halfDirection = normalize(lightDirection + viewDirection);

                float diffuse = saturate(dot(normal, lightDirection));
                float highlight = pow(saturate(dot(normal, halfDirection)), lerp(8.0, 64.0, smoothness));
                half3 ambient = SampleSH(normal) + 0.15;
                half3 lumiere = mainLight.color * mainLight.shadowAttenuation;

                half3 couleur = albedo * (ambient * 0.75 + lumiere * diffuse * 0.72);
                couleur += lumiere * highlight * (_Metallic * 0.18 + _RoughStrength * 0.10 * smoothness);
                couleur += _EmissionColor.rgb;
                return half4(couleur, 1.0);
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
            Cull Off

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
}

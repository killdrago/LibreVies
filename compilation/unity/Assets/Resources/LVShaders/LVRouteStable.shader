// Rendu PBR leger et robuste pour tout le monde.
// Pas de surface shader Standard : le chemin vertex/fragment reste compatible
// avec les anciennes cartes Direct3D11 comme l'AMD Radeon R7 200.
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
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Tiling;
            float4 _Defilement;
            float _Metallic;
            float _Smoothness;
            fixed4 _EmissionColor;
            sampler2D _BumpMap;
            float _BumpStrength;
            sampler2D _RoughMap;
            float _RoughStrength;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                SHADOW_COORDS(2)
            };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                TRANSFER_SHADOW(output);
                return output;
            }

            fixed4 SampleTriplanar(float3 position, float3 normal)
            {
                float scale = max(_Tiling, 0.001);
                float3 weights = abs(normalize(normal));
                weights /= max(weights.x + weights.y + weights.z, 0.001);
                // _Defilement : decalage de texture (0 pour tout le monde sauf l'eau qui coule).
                fixed4 sampleX = tex2D(_MainTex, position.yz * scale + _Defilement.xy);
                fixed4 sampleY = tex2D(_MainTex, position.xz * scale + _Defilement.xy);
                fixed4 sampleZ = tex2D(_MainTex, position.xy * scale + _Defilement.xy);
                return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
            }

            // Normale triplanaire : la carte de relief est projetee sur les trois axes
            // puis ramenee dans l'espace du monde. Une carte plate donne la normale
            // geometrique, donc les materiaux sans carte ne changent pas.
            float3 NormaleTriplanaire(float3 position, float3 normal)
            {
                float scale = max(_Tiling, 0.001);
                float3 poids = abs(normal);
                poids /= max(poids.x + poids.y + poids.z, 0.001);
                float3 tX = tex2D(_BumpMap, position.zy * scale).rgb * 2.0 - 1.0;
                float3 tY = tex2D(_BumpMap, position.xz * scale).rgb * 2.0 - 1.0;
                float3 tZ = tex2D(_BumpMap, position.xy * scale).rgb * 2.0 - 1.0;
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
                float rX = tex2D(_RoughMap, position.zy * scale).r;
                float rY = tex2D(_RoughMap, position.xz * scale).r;
                float rZ = tex2D(_RoughMap, position.xy * scale).r;
                return rX * poids.x + rY * poids.y + rZ * poids.z;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float3 normalGeometrique = normalize(input.worldNormal);
                float3 normal = normalGeometrique;
                float smoothness = _Smoothness;
                if (_BumpStrength > 0.001)
                    normal = normalize(lerp(normal, NormaleTriplanaire(input.worldPosition, normal), saturate(_BumpStrength * 4.0)));
                if (_RoughStrength > 0.001)
                    smoothness = lerp(_Smoothness, 1.0 - RugositeTriplanaire(input.worldPosition, normalGeometrique), _RoughStrength);
                fixed3 albedo = SampleTriplanar(input.worldPosition, normalGeometrique).rgb * _Color.rgb;
                UNITY_LIGHT_ATTENUATION(ombre, input, input.worldPosition);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                float3 halfDirection = normalize(lightDirection + viewDirection);
                float diffuse = saturate(dot(normal, lightDirection));
                float highlight = pow(saturate(dot(normal, halfDirection)), lerp(8.0, 64.0, smoothness));
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75 + fixed3(0.18, 0.18, 0.18);
                fixed3 couleurEclairee = albedo * (ambient + diffuse * 0.72) + highlight * _Metallic * 0.18;
                fixed3 couleurOmbree = albedo * (ambient * 0.72);
                fixed3 colour = lerp(couleurOmbree, couleurEclairee, ombre);
                // Un plancher de lumiere evite le noir complet sur une vieille
                // carte, mais les ombres restent bien liees au monde.
                colour = max(colour, albedo * (0.12 + 0.10 * ombre));
                colour += _EmissionColor.rgb;
                return fixed4(colour, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

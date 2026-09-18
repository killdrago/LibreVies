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
        _Metallic ("Metallic", Range(0,1)) = 0.05
        _Smoothness ("Brillance", Range(0,1)) = 0.28
        _EmissionColor ("Emission", Color) = (0,0,0,0)
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Tiling;
            float _Metallic;
            float _Smoothness;
            fixed4 _EmissionColor;

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
            };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 SampleTriplanar(float3 position, float3 normal)
            {
                float scale = max(_Tiling, 0.001);
                float3 weights = abs(normalize(normal));
                weights /= max(weights.x + weights.y + weights.z, 0.001);
                fixed4 sampleX = tex2D(_MainTex, position.yz * scale);
                fixed4 sampleY = tex2D(_MainTex, position.xz * scale);
                fixed4 sampleZ = tex2D(_MainTex, position.xy * scale);
                return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                fixed3 albedo = SampleTriplanar(input.worldPosition, normal).rgb * _Color.rgb;
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                float3 halfDirection = normalize(lightDirection + viewDirection);
                float diffuse = saturate(dot(normal, lightDirection));
                float highlight = pow(saturate(dot(normal, halfDirection)), lerp(8.0, 64.0, _Smoothness));
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75 + fixed3(0.18, 0.18, 0.18);
                fixed3 colour = albedo * (ambient + diffuse * 0.72) + highlight * _Metallic * 0.18;
                // Un plancher de lumiere evite le noir complet sur une vieille
                // carte ou quand Unity ne fournit pas de lumiere directionnelle.
                colour = max(colour, albedo * 0.22);
                colour += _EmissionColor.rgb;
                return fixed4(colour, 1.0);
            }
            ENDCG
        }
    }
}

// Materiau opaque pour le corps MakeHuman et ses sous-vetements.
// Le textile est calcule en espace objet, pixel par pixel, afin de ne pas
// dependre des ilots UV qui peuvent melanger torse et bras.
Shader "LibreVies/PersonnageOpaque"
{
    Properties
    {
        _Color ("Couleur peau", Color) = (1,1,1,1)
        _MainTex ("Texture peau", 2D) = "white" {}
        _ClothColor ("Couleur sous-vetement", Color) = (0.08,0.10,0.16,1)
        _ClothTex ("Texture sous-vetement", 2D) = "white" {}
        _UnderwearFemale ("Personnage feminin", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        LOD 200
        Cull Back
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _ClothTex;
            fixed4 _Color;
            fixed4 _ClothColor;
            float _UnderwearFemale;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 localPosition : TEXCOORD2;
            };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.localPosition = input.vertex.xyz;
                return output;
            }

            float bandMask(float value, float bottom, float top)
            {
                float lower = smoothstep(bottom - 0.025, bottom + 0.025, value);
                float upper = 1.0 - smoothstep(top - 0.025, top + 0.025, value);
                return lower * upper;
            }

            float underwearMask(float3 point)
            {
                float side = abs(point.x);
                float frontBack = abs(point.z);
                float mask = 0.0;
                if (_UnderwearFemale > 0.5 && frontBack >= 0.045)
                {
                    float leftCup = pow((point.x + 0.09) / 0.14, 2.0)
                        + pow((point.y - 1.61) / 0.12, 2.0);
                    float rightCup = pow((point.x - 0.09) / 0.14, 2.0)
                        + pow((point.y - 1.61) / 0.12, 2.0);
                    float cups = 1.0 - smoothstep(0.78, 1.08, min(leftCup, rightCup));
                    float band = bandMask(point.y, 1.46, 1.55)
                        * (1.0 - smoothstep(0.19, 0.25, side));
                    float strapT = saturate((point.y - 1.61) / 0.29);
                    float leftStrap = 1.0 - smoothstep(0.018, 0.040,
                        abs(point.x - lerp(-0.09, -0.16, strapT)));
                    float rightStrap = 1.0 - smoothstep(0.018, 0.040,
                        abs(point.x - lerp(0.09, 0.16, strapT)));
                    if (point.y < 1.61 || point.y > 1.90)
                    {
                        leftStrap = 0.0;
                        rightStrap = 0.0;
                    }
                    mask = max(cups, max(band, max(leftStrap, rightStrap)));
                }
                if (point.y >= 0.94 && point.y <= 1.30)
                {
                    float width = lerp(0.15, 0.27,
                        saturate((point.y - 0.94) / 0.36));
                    float sides = 1.0 - smoothstep(width - 0.025, width + 0.025, side);
                    float lower = smoothstep(0.94, 0.99, point.y);
                    float upper = 1.0 - smoothstep(1.27, 1.30, point.y);
                    mask = max(mask, sides * min(lower, upper));
                }
                return saturate(mask);
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                fixed3 skinAlbedo = tex2D(_MainTex, input.uv).rgb * _Color.rgb;
                float2 clothUv = frac(input.localPosition.xy * 3.0);
                fixed3 clothAlbedo = tex2D(_ClothTex, clothUv).rgb * _ClothColor.rgb;
                fixed3 albedo = lerp(skinAlbedo, clothAlbedo,
                    underwearMask(input.localPosition));
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75 + fixed3(0.20, 0.20, 0.20);
                fixed3 colour = albedo * (ambient + diffuse * 0.72);
                return fixed4(max(colour, albedo * 0.22), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

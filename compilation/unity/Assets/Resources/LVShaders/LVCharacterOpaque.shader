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
                if (_UnderwearFemale <= 0.5 || abs(point.z) < 0.045)
                    return 0.0;
                // Test volontairement minimal : deux ronds jaunes a la place
                // des seins, sans bande, bretelle, texture ou culotte.
                float leftCircle = pow((point.x + 0.09) / 0.14, 2.0)
                    + pow((point.y - 1.61) / 0.12, 2.0);
                float rightCircle = pow((point.x - 0.09) / 0.14, 2.0)
                    + pow((point.y - 1.61) / 0.12, 2.0);
                return 1.0 - smoothstep(0.82, 1.02,
                    min(leftCircle, rightCircle));
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                fixed3 skinAlbedo = tex2D(_MainTex, input.uv).rgb * _Color.rgb;
                // Le test utilise uniquement une couleur pleine, sans motif.
                fixed3 clothAlbedo = _ClothColor.rgb;
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

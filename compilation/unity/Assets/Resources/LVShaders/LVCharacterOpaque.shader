// Matériau opaque pour les maillages humanoïdes importés.
// Les personnages ne doivent pas afficher leurs faces internes.
Shader "LibreVies/PersonnageOpaque"
{
        Properties
    {
        _Color ("Couleur peau", Color) = (1,1,1,1)
        _MainTex ("Texture peau", 2D) = "white" {}
        _ClothColor ("Couleur sous-vetement", Color) = (0.08,0.10,0.16,1)
        _ClothTex ("Texture sous-vetement", 2D) = "white" {}
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
            float4 _ClothTex_ST;
            fixed4 _Color;
            fixed4 _ClothColor;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
                fixed mask : TEXCOORD2;
            };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.mask = saturate(input.color.r);
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                fixed3 skinAlbedo = tex2D(_MainTex, input.uv).rgb * _Color.rgb;
                fixed3 clothAlbedo = tex2D(_ClothTex,
                    input.uv * _ClothTex_ST.xy + _ClothTex_ST.zw).rgb * _ClothColor.rgb;
                fixed3 albedo = lerp(skinAlbedo, clothAlbedo, input.mask);
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75 + fixed3(0.20, 0.20, 0.20);
                fixed3 colour = albedo * (ambient + diffuse * 0.72);
                return fixed4(max(colour, albedo * 0.22), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

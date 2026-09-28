// Matériau opaque pour les maillages humanoïdes importés.
// Les personnages ne doivent pas afficher leurs faces internes.
Shader "LibreVies/PersonnageOpaque"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
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
            fixed4 _Color;

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
            };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                fixed3 albedo = tex2D(_MainTex, input.uv).rgb * _Color.rgb;
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75 + fixed3(0.20, 0.20, 0.20);
                fixed3 colour = albedo * (ambient + diffuse * 0.72);
                return fixed4(max(colour, albedo * 0.22), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

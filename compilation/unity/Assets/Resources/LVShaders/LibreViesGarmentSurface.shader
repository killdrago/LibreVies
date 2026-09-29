// Surface de vetement directement dans le maillage skine du personnage.
// Le sous-maillage vetement partage les memes vertices et poids que la peau.
Shader "LibreVies/SoutienGorgeSurface"
{
    Properties
    {
        _MainTex ("Texture du vetement", 2D) = "white" {}
        _Color ("Teinte", Color) = (1,1,1,1)
        _Cutoff ("Seuil alpha", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        LOD 200
        Cull Back
        ZWrite On
        AlphaToMask On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Cutoff;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 garmentUv : TEXCOORD1;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 garmentUv : TEXCOORD1;
            };

            Varyings vert(AppData input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.garmentUv = input.garmentUv;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                fixed4 clothingSample = tex2D(_MainTex, input.garmentUv) * _Color;
                clip(clothingSample.a - _Cutoff);
                float3 normal = normalize(input.normal);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75 + fixed3(0.20, 0.20, 0.20);
                fixed3 colour = clothingSample.rgb * (ambient + diffuse * 0.72);
                return fixed4(max(colour, clothingSample.rgb * 0.22), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Transparent/Cutout/VertexLit"
}

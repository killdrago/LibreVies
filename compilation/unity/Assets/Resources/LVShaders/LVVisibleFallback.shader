// Repli de rendu toujours visible pour les builds Windows.
// Ce shader simple evite qu'une variante PBR non supportee rende tout le monde invisible.
Shader "LibreVies/RenduVisibleSecours"
{
    Properties
    {
        _Color ("Teinte", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Tiling ("Echelle monde", Float) = 0.35
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
                fixed4 textureColor = SampleTriplanar(input.worldPosition, input.worldNormal);
                float3 normal = normalize(input.worldNormal);
                float3 sunDirection = normalize(_WorldSpaceLightPos0.xyz);
                float lighting = saturate(dot(normal, sunDirection) * 0.45 + 0.55);
                return fixed4(textureColor.rgb * _Color.rgb * lighting, 1.0);
            }
            ENDCG
        }
    }
}

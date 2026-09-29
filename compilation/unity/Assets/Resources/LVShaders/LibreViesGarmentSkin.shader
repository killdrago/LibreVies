// Remplacement du vetement sur la surface du corps.
// Le PNG est compose avec SkinBase dans le meme SkinnedMeshRenderer :
// il n'y a ni quad frontal ni mesh de bretelle flottant.
Shader "LibreVies/SoutienGorgeSkin"
{
    Properties
    {
        _Color ("Teinte peau", Color) = (1,1,1,1)
        _MainTex ("Peau", 2D) = "white" {}
        _GarmentTex ("Soutien-gorge PNG", 2D) = "black" {}
        _GarmentScale ("Taille", Float) = 1
        _OffsetX ("Deplacement X", Float) = 0
        _OffsetY ("Deplacement Y", Float) = 0.050909
        _OffsetZ ("Profondeur Z", Float) = 0.243182
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _GarmentTex;
            fixed4 _Color;
            float _GarmentScale;
            float _OffsetX;
            float _OffsetY;
            float _OffsetZ;

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

            float DistanceToSegment(float2 point, float2 start, float2 end)
            {
                float2 segment = end - start;
                float amount = saturate(dot(point - start, segment)
                    / max(dot(segment, segment), 0.0001));
                return distance(point, start + segment * amount);
            }

            fixed4 LitColour(fixed4 value, float3 normal)
            {
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normalize(normal), lightDirection));
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * 0.75
                    + fixed3(0.20, 0.20, 0.20);
                fixed3 lit = value.rgb * (ambient + diffuse * 0.72);
                return fixed4(max(lit, value.rgb * 0.22), 1.0);
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                fixed4 skin = tex2D(_MainTex, input.uv) * _Color;
                fixed4 garment = fixed4(0, 0, 0, 0);
                float factor = clamp(_GarmentScale, 0.25, 3.0);
                float garmentWidth = 0.36 * factor;
                float garmentHeight = garmentWidth * 300.0 / 322.0;
                float centerY = 1.61 + _OffsetY;
                float2 point = input.localPosition.xy - float2(_OffsetX, centerY);
                float2 uv = point / float2(garmentWidth, garmentHeight)
                    + float2(0.5, 0.5);
                float frontSide = min(0.0, _OffsetZ * 0.1);

                if (input.localPosition.z >= frontSide
                    && uv.x >= 0.0 && uv.x <= 1.0
                    && uv.y >= 0.0 && uv.y <= 1.0)
                {
                    garment = tex2D(_GarmentTex, uv);
                }
                else if (input.localPosition.z < -0.01)
                {
                    float2 left0 = float2(-0.15, -0.28) * factor;
                    float2 left1 = float2(-0.18, -0.10) * factor;
                    float2 left2 = float2(-0.19, 0.12) * factor;
                    float2 left3 = float2(-0.18, 0.19) * factor;
                    float2 right0 = float2(0.15, -0.28) * factor;
                    float2 right1 = float2(0.18, -0.10) * factor;
                    float2 right2 = float2(0.19, 0.12) * factor;
                    float2 right3 = float2(0.18, 0.19) * factor;
                    float strapDistance = min(
                        min(DistanceToSegment(point, left0, left1),
                            DistanceToSegment(point, left1, left2)),
                        min(DistanceToSegment(point, left2, left3),
                            min(DistanceToSegment(point, right0, right1),
                                min(DistanceToSegment(point, right1, right2),
                                    DistanceToSegment(point, right2, right3)))));
                    if (strapDistance <= 0.040 * factor)
                    {
                        float t = saturate((point.y + 0.28 * factor)
                            / (0.47 * factor));
                        float strapU = point.x < 0.0
                            ? 0.07 + (1.0 - t) * 0.08
                            : 0.93 - (1.0 - t) * 0.08;
                        garment = tex2D(_GarmentTex,
                            float2(strapU, 0.45 + t * 0.55));
                    }
                }

                fixed4 skinLit = LitColour(skin, input.normal);
                fixed4 garmentLit = LitColour(garment, input.normal);
                float amount = saturate(garment.a);
                return fixed4(lerp(skinLit.rgb, garmentLit.rgb, amount), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

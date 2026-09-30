// Surface unique : la peau et les sous-vetements sont composes par pixel.
// Le rendu reste opaque et utilise le meme mesh skine que le corps.
Shader "LibreVies/SoutienGorgeSkin"
{
    Properties
    {
        _Color ("Teinte peau", Color) = (1,1,1,1)
        _MainTex ("Peau", 2D) = "white" {}
        _GarmentTex ("Texture soutien-gorge", 2D) = "white" {}
        _UnderwearColor ("Couleur culotte", Color) = (0.80,0.71,0.62,1)
        _GarmentScale ("Taille", Float) = 1
        _OffsetX ("Deplacement X", Float) = 0
        _OffsetY ("Deplacement Y", Float) = 0.050909
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
            fixed4 _UnderwearColor;
            float _GarmentScale;
            float _OffsetX;
            float _OffsetY;

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

            float EllipseMask(float2 point, float2 center, float2 radius)
            {
                float2 value = (point - center) / max(radius, float2(0.0001, 0.0001));
                return 1.0 - smoothstep(0.94, 1.0, dot(value, value));
            }

            fixed4 Light(fixed4 value, float3 normal)
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
                fixed4 garment = _UnderwearColor;
                float garmentAmount = 0.0;
                float factor = clamp(_GarmentScale, 0.25, 3.0);
                float width = 0.36 * factor;
                float height = width * 300.0 / 322.0;
                float centerY = 1.61 + _OffsetY;
                float2 point = input.localPosition.xy - float2(_OffsetX, centerY);

                // Bonnet et decollete avant. Les masques continus recouvrent
                // toute la poitrine, y compris la zone du teton, sans trou.
                if (input.localPosition.z >= 0.0)
                {
                    float2 garmentUv = point / float2(width, height)
                        + float2(0.5, 0.5);
                    fixed4 sampled = fixed4(0, 0, 0, 0);
                    float sampledAmount = 0.0;
                    if (garmentUv.x >= 0.0 && garmentUv.x <= 1.0
                        && garmentUv.y >= 0.0 && garmentUv.y <= 1.0)
                    {
                        sampled = tex2D(_GarmentTex, garmentUv);
                        sampledAmount = smoothstep(0.01, 0.16, sampled.a);
                    }

                    float cupY = -height * 0.14;
                    float cupX = width * 0.29;
                    float cupRadiusX = width * 0.34 * 1.15;
                    float cupRadiusY = height * 0.34 * 1.15;
                    float cups = max(
                        EllipseMask(point, float2(-cupX, cupY),
                            float2(cupRadiusX, cupRadiusY)),
                        EllipseMask(point, float2(cupX, cupY),
                            float2(cupRadiusX, cupRadiusY)));
                    float bridge = (1.0 - smoothstep(0.035, 0.047,
                        max(abs(point.x) - width * 0.20,
                            abs(point.y + 0.025) - 0.040)));
                    float frontStraps = 1.0 - smoothstep(0.014 * factor,
                        0.027 * factor, min(
                            DistanceToSegment(point, float2(-cupX, cupY + height * 0.02),
                                float2(-width * 0.40, cupY + height * 0.02 + width * 0.68)),
                            DistanceToSegment(point, float2(cupX, cupY + height * 0.02),
                                float2(width * 0.40, cupY + height * 0.02 + width * 0.68))));
                    garmentAmount = max(sampledAmount, max(cups, max(bridge, frontStraps)));
                    garment.rgb = lerp(_UnderwearColor.rgb, sampled.rgb,
                        sampledAmount);
                }
                else
                {
                    float backStraps = 1.0 - smoothstep(0.014 * factor,
                        0.027 * factor, min(
                            DistanceToSegment(point, float2(-0.15, -0.28) * factor,
                                float2(-0.18, 0.19) * factor),
                            DistanceToSegment(point, float2(0.15, -0.28) * factor,
                                float2(0.18, 0.19) * factor)));
                    float bandDistance = max(
                        abs(point.y + width * 0.38) - 0.035,
                        abs(point.x) - width * 0.52);
                    float backBand = 1.0 - smoothstep(0.0, 0.012, bandDistance);
                    garmentAmount = max(backStraps, backBand);
                }

                // Culotte basse : son haut est descendu pour rester sur les
                // hanches et ses bords sont lisses, sans pointes de triangles.
                float pantyBottom = 0.82;
                float pantyTop = 1.12;
                if (input.localPosition.y >= pantyBottom - 0.02
                    && input.localPosition.y <= pantyTop + 0.02)
                {
                    float t = saturate((input.localPosition.y - pantyBottom)
                        / (pantyTop - pantyBottom));
                    float halfWidth = lerp(0.205, 0.27, t) * factor;
                    float horizontal = 1.0 - smoothstep(0.0, 0.012,
                        abs(input.localPosition.x - _OffsetX) - halfWidth);
                    float vertical = smoothstep(pantyBottom - 0.02,
                        pantyBottom + 0.012, input.localPosition.y)
                        * (1.0 - smoothstep(pantyTop - 0.012,
                            pantyTop + 0.02, input.localPosition.y));
                    float pantyAmount = horizontal * vertical;
                    if (pantyAmount > garmentAmount)
                    {
                        garment = _UnderwearColor;
                        garmentAmount = pantyAmount;
                    }
                }

                fixed4 skinLit = Light(skin, input.normal);
                fixed4 garmentLit = Light(fixed4(garment.rgb, 1.0), input.normal);
                return fixed4(lerp(skinLit.rgb, garmentLit.rgb,
                    saturate(garmentAmount)), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

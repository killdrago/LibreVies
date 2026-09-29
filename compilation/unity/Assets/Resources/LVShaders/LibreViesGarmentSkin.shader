// Vetements integres dans le meme maillage skine que la peau.
// La projection est calculee par pixel : les bords du soutien-gorge et de la
// culotte ne dependent pas d'une decoupe de triangles.
Shader "LibreVies/SoutienGorgeSkin"
{
    Properties
    {
        _Color ("Teinte peau", Color) = (1,1,1,1)
        _MainTex ("Peau", 2D) = "white" {}
        _GarmentTex ("Soutien-gorge", 2D) = "white" {}
        _UnderwearColor ("Couleur sous-vetements", Color) = (0.80,0.71,0.62,1)
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
                fixed4 garment = fixed4(0, 0, 0, 0);
                float factor = clamp(_GarmentScale, 0.25, 3.0);
                float width = 0.36 * factor;
                float height = width * 300.0 / 322.0;
                float centerY = 1.61 + _OffsetY;
                float2 point = input.localPosition.xy - float2(_OffsetX, centerY);

                // Le PNG dessine directement la forme du soutien-gorge sur
                // les fragments de la surface du torse.
                if (input.localPosition.z >= 0.0)
                {
                    float2 garmentUv = point / float2(width, height)
                        + float2(0.5, 0.5);
                    if (garmentUv.x >= 0.0 && garmentUv.x <= 1.0
                        && garmentUv.y >= 0.0 && garmentUv.y <= 1.0)
                        garment = tex2D(_GarmentTex, garmentUv);
                }
                else
                {
                    // Les deux bretelles et la bande dorsale utilisent la
                    // meme couleur et les pixels de bretelle du PNG.
                    float2 left0 = float2(-0.15, -0.28) * factor;
                    float2 left1 = float2(-0.18, 0.19) * factor;
                    float2 right0 = float2(0.15, -0.28) * factor;
                    float2 right1 = float2(0.18, 0.19) * factor;
                    float strapDistance = min(
                        DistanceToSegment(point, left0, left1),
                        DistanceToSegment(point, right0, right1));
                    if (strapDistance <= 0.022 * factor)
                    {
                        float t = saturate((point.y + 0.28 * factor)
                            / (0.47 * factor));
                        float strapU = point.x < 0.0
                            ? 0.07 + (1.0 - t) * 0.08
                            : 0.93 - (1.0 - t) * 0.08;
                        garment = tex2D(_GarmentTex,
                            float2(strapU, 0.45 + t * 0.55));
                    }
                    else if (abs(point.y + width * 0.38) <= 0.035
                        && abs(point.x) <= width * 0.52)
                    {
                        garment = _UnderwearColor;
                    }
                }

                // Culotte basse : elle recouvre le devant et le dos jusqu'au
                // haut des cuisses, sans remonter comme un calecon taille haute.
                float pantyBottom = 0.74;
                float pantyTop = 1.23;
                if (input.localPosition.y >= pantyBottom
                    && input.localPosition.y <= pantyTop)
                {
                    float t = saturate((input.localPosition.y - pantyBottom)
                        / (pantyTop - pantyBottom));
                    float halfWidth = lerp(0.19, 0.27, t) * factor;
                    if (abs(input.localPosition.x - _OffsetX) <= halfWidth)
                        garment = _UnderwearColor;
                }

                fixed4 skinLit = Light(skin, input.normal);
                fixed4 garmentLit = Light(garment, input.normal);
                return fixed4(lerp(skinLit.rgb, garmentLit.rgb,
                    saturate(garment.a)), 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

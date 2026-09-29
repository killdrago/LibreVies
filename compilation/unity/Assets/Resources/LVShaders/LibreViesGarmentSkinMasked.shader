Shader "LibreVies/GarmentSkinMasked"
{
    Properties
    {
        [MainColor] _Color ("Skin color", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Skin texture", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.35
        _GarmentTex ("Garment alpha", 2D) = "black" {}
        _MaskCenterWS ("Mask center", Vector) = (0,0,0,0)
        _MaskRightWS ("Mask right", Vector) = (1,0,0,0)
        _MaskUpWS ("Mask up", Vector) = (0,1,0,0)
        _MaskForwardWS ("Mask forward", Vector) = (0,0,1,0)
        _MaskWidth ("Mask width", Float) = 1
        _MaskHeight ("Mask height", Float) = 1
        _MaskMinDepth ("Mask minimum depth", Float) = -0.5
        _MaskMaxDepth ("Mask maximum depth", Float) = 0.15
        _MaskEnabled ("Mask enabled", Float) = 0
        _MaskAlphaClip ("Mask alpha threshold", Range(0,1)) = 0.08
        _BackStrapColor ("Back strap color", Color) = (0.8,0.71,0.62,1)
        _BackStrapScale ("Back strap scale", Float) = 1
        _BackStrapWidth ("Back strap width", Float) = 0.026
        _BackStrapMinDepth ("Back strap minimum depth", Float) = -0.5
        _BackStrapMaxDepth ("Back strap maximum depth", Float) = -0.08
        _BackStrapEnabled ("Back straps enabled", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _GarmentTex;
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        float4 _MaskCenterWS;
        float4 _MaskRightWS;
        float4 _MaskUpWS;
        float4 _MaskForwardWS;
        float _MaskWidth;
        float _MaskHeight;
        float _MaskMinDepth;
        float _MaskMaxDepth;
        float _MaskEnabled;
        float _MaskAlphaClip;
        fixed4 _BackStrapColor;
        float _BackStrapScale;
        float _BackStrapWidth;
        float _BackStrapMinDepth;
        float _BackStrapMaxDepth;
        float _BackStrapEnabled;

        float DistanceToSegment(float2 point, float2 start, float2 end)
        {
            float2 segment = end - start;
            float amount = saturate(dot(point - start, segment)
                / max(dot(segment, segment), 0.0001));
            return length(point - (start + segment * amount));
        }

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        void surf(Input IN, inout SurfaceOutputStandard surface)
        {
            fixed4 skin = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            if (_MaskEnabled > 0.5)
            {
                float3 right = normalize(_MaskRightWS.xyz);
                float3 up = normalize(_MaskUpWS.xyz);
                float3 forward = normalize(_MaskForwardWS.xyz);
                float3 relative = IN.worldPos - _MaskCenterWS.xyz;
                float u = dot(relative, right) / max(_MaskWidth, 0.0001) + 0.5;
                float v = dot(relative, up) / max(_MaskHeight, 0.0001) + 0.5;
                float depth = dot(relative, forward);
                float front = dot(normalize(IN.worldNormal), forward);

                if (u >= 0.0 && u <= 1.0 && v >= 0.0 && v <= 1.0
                    && depth >= _MaskMinDepth && depth <= _MaskMaxDepth
                    // Les bretelles passent sur une surface un peu tournee :
                    // on conserve une marge pour couvrir les epaules sans
                    // peindre le dos.
                    && front > -0.15)
                {
                    fixed4 garment = tex2D(_GarmentTex, float2(u, v));
                    if (garment.a > _MaskAlphaClip)
                    {
                        // Le maillage de peau recoit directement la couleur
                        // du vetement : ce n'est pas un rectangle pose devant.
                        // Les pixels transparents gardent la peau d'origine.
                        skin.rgb = lerp(skin.rgb, garment.rgb, garment.a);
                    }
                }

                if (_BackStrapEnabled > 0.5)
                {
                    float strapScale = max(_BackStrapScale, 0.25);
                    float2 point = float2(dot(relative, right), dot(relative, up));
                    float2 left0 = float2(-0.15, -0.28) * strapScale;
                    float2 left1 = float2(-0.18, -0.10) * strapScale;
                    float2 left2 = float2(-0.19, 0.12) * strapScale;
                    float2 left3 = float2(-0.18, 0.19) * strapScale;
                    float2 right0 = float2(0.15, -0.28) * strapScale;
                    float2 right1 = float2(0.18, -0.10) * strapScale;
                    float2 right2 = float2(0.19, 0.12) * strapScale;
                    float2 right3 = float2(0.18, 0.19) * strapScale;
                    float leftDistance = min(DistanceToSegment(point, left0, left1),
                        min(DistanceToSegment(point, left1, left2),
                            DistanceToSegment(point, left2, left3)));
                    float rightDistance = min(DistanceToSegment(point, right0, right1),
                        min(DistanceToSegment(point, right1, right2),
                            DistanceToSegment(point, right2, right3)));
                    float backFacing = dot(normalize(IN.worldNormal), -forward);
                    bool onBack = depth >= _BackStrapMinDepth
                        && depth <= _BackStrapMaxDepth && backFacing > -0.15;
                    if (onBack && min(leftDistance, rightDistance)
                        <= _BackStrapWidth * strapScale)
                        skin.rgb = _BackStrapColor.rgb;
                }
            }

            surface.Albedo = skin.rgb;
            surface.Metallic = _Metallic;
            surface.Smoothness = _Glossiness;
            surface.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}

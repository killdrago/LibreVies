Shader "LibreVies/GarmentSkinMasked"
{
    Properties
    {
        _Color ("Skin color", Color) = (1,1,1,1)
        _MainTex ("Skin texture", 2D) = "white" {}
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
                    // decouper la peau du dos.
                    && front > -0.15)
                {
                    fixed garmentAlpha = tex2D(_GarmentTex, float2(u, v)).a;
                    clip(garmentAlpha - _MaskAlphaClip);
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

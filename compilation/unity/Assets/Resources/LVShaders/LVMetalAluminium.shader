// Aluminium brosse pour les portes d'edition.
// Ce shader reste distinct du materiau Metal des decorations afin que la
// porte puisse recevoir un rendu clair, metallique et legerement brosse.
Shader "LibreVies/MetalAluminium"
{
    Properties
    {
        _Color ("Teinte aluminium", Color) = (0.72,0.76,0.80,1)
        _MainTex ("Texture metal", 2D) = "white" {}
        _Tiling ("Echelle du brossage", Float) = 0.30
        _Metallic ("Metallic", Range(0,1)) = 0.95
        _Smoothness ("Brillance", Range(0,1)) = 0.90
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Tiling;
        float _Metallic;
        float _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float echelle = max(_Tiling, 0.01);
            fixed4 textureMetal = tex2D(_MainTex, input.uv_MainTex * echelle);
            float brossage = sin((input.worldPos.x + input.worldPos.y * 0.35) * 38.0) * 0.035;
            output.Albedo = saturate(textureMetal.rgb * _Color.rgb * (1.0 + brossage));
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            output.Alpha = 1.0;
        }
        ENDCG
    }
    Fallback "Standard"
}

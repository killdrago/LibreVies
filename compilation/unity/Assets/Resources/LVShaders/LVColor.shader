// Shader de couleur procedural doux de LibreVies.
// Il est place dans Resources pour etre toujours embarque dans la build.
Shader "LibreVies/Couleur"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _DetailScale ("Echelle du motif", Float) = 0.75
        _DetailStrength ("Force du motif", Range(0,0.3)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0
        fixed4 _Color;
        fixed4 _EmissionColor;
        float _DetailScale;
        float _DetailStrength;
        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };
        void surf(Input IN, inout SurfaceOutput o)
        {
            // Variation tres fine : mur platre, pierre et bois cessent d'etre
            // des cubes parfaitement plats sans avoir besoin d'un asset image.
            float motifA = sin(IN.worldPos.x * _DetailScale) * sin(IN.worldPos.z * _DetailScale);
            float motifB = sin((IN.worldPos.x + IN.worldPos.y) * _DetailScale * 2.17);
            float variation = (motifA * 0.65 + motifB * 0.35) * _DetailStrength;
            o.Albedo = saturate(_Color.rgb * (1.0 + variation));
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

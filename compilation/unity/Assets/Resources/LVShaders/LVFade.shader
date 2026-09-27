// Materiau mat_fade pour les objets qui cachent le heros et le camouflage.
Shader "LibreVies/Translucide"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _Alpha ("Transparence", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        float _Alpha;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Alpha = _Alpha * _Color.a;
        }
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}

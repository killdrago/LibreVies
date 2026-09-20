// Verre bleu transparent pour les fenetres des maisons.
Shader "LibreVies/VerreBleu"
{
    Properties
    {
        _Color ("Teinte bleue", Color) = (0.12,0.48,0.88,1)
        _MainTex ("Revetement", 2D) = "white" {}
        _Tiling ("Echelle du revetement", Float) = 1.0
        _Alpha ("Transparence", Range(0,1)) = 0.30
        _Metallic ("Metallic", Range(0,1)) = 0.05
        _Smoothness ("Brillance", Range(0,1)) = 0.90
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // Les deux faces restent visibles pour voir l'interieur depuis les deux cotes.
        Cull Off
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Tiling;
        float _Alpha;
        float _Metallic;
        float _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float echelle = max(_Tiling, 0.01);
            fixed4 revetement = tex2D(_MainTex, input.uv_MainTex * echelle);
            output.Albedo = saturate(revetement.rgb * _Color.rgb);
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            output.Emission = _Color.rgb * 0.035;
            output.Alpha = _Alpha * _Color.a * revetement.a;
        }
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}

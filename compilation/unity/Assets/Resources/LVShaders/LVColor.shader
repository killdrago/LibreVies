// Shader de secours du projet LibreVies.
//
// POURQUOI CE FICHIER EXISTE :
// Le jeu cree tous ses materiaux en code (new Material(Shader.Find("Standard"))).
// Or Unity RETIRE de la build les shaders integres qu'aucun materiau du projet
// ne reference : Shader.Find("Standard") renvoie donc NULL dans l'exe compile,
// et tout le monde s'affiche en MAGENTA (rose fluo).
// Ce shader est dans Assets/Resources/ : Unity l'inclut TOUJOURS dans la build.
// Le code l'utilise en secours quand "Standard" n'est pas disponible.
//
// Il eclaire comme Standard de base : lumiere directionnelle (le soleil) +
// lumiere ambiante + ombres recues, ce qui garde le rendu low-poly du jeu.
Shader "LibreVies/Couleur"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0

        fixed4 _Color;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

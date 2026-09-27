// Ciel de secours du projet LibreVies (degrade bleu vif -> horizon chaud).
//
// Meme raison que LVColor.shader : "Skybox/Procedural" est un shader integre
// qu'Unity peut retirer de la build (Shader.Find renvoie alors NULL et le ciel
// n'est plus celui voulu). Ce shader est dans Assets/Resources/, donc toujours
// embarque. Le code l'utilise quand "Skybox/Procedural" n'est pas trouve.
Shader "LibreVies/Ciel"
{
    Properties
    {
        _SkyColor ("Haut du ciel", Color) = (0.30,0.46,0.78,1)
        _HorizonColor ("Horizon", Color) = (0.98,0.74,0.48,1)
        _GroundColor ("Sol lointain", Color) = (0.45,0.50,0.26,1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 position : SV_POSITION;
            float3 direction : TEXCOORD0;
        };

        fixed4 _SkyColor;
        fixed4 _HorizonColor;
        fixed4 _GroundColor;

        v2f vert(appdata v)
        {
            v2f o;
            o.position = UnityObjectToClipPos(v.vertex);
            // La sphere du cielbox est centree sur la camera : la position du
            // sommet est donc directement la direction a colorer.
            o.direction = v.vertex.xyz;
            return o;
        }

        fixed4 frag(v2f i) : SV_Target
        {
            float h = normalize(i.direction).y;
            fixed4 ciel = lerp(_HorizonColor, _SkyColor, saturate(h * 1.15 + 0.05));
            return lerp(_GroundColor, ciel, saturate(h * 6.0 + 0.55));
        }
        ENDCG
    }
    Fallback Off
}

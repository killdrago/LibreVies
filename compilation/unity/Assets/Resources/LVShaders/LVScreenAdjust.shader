// Reglage luminosite/contraste/nettete/vivacite visible dans les options.
Shader "Hidden/LibreVies/ReglagesEcran"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Brightness ("Luminosite", Float) = 0.5
        _Contrast ("Contraste", Float) = 1
        _Nettete ("Nettete", Float) = 0
        _Vivacite ("Vivacite", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Brightness;
            float _Contrast;
            float _Nettete;
            float _Vivacite;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 couleur = tex2D(_MainTex, i.uv);
                // Nettete : masque flou (4 voisins) soustrait pour accentuer les contours.
                float2 dx = float2(_MainTex_TexelSize.x, 0);
                float2 dy = float2(0, _MainTex_TexelSize.y);
                fixed3 flou = (tex2D(_MainTex, i.uv + dx).rgb + tex2D(_MainTex, i.uv - dx).rgb
                             + tex2D(_MainTex, i.uv + dy).rgb + tex2D(_MainTex, i.uv - dy).rgb) * 0.25;
                fixed3 image = couleur.rgb + (couleur.rgb - flou) * _Nettete;
                // Vivacite : couleurs un peu plus riches (0 = aucun effet).
                float gris = dot(image, float3(0.299, 0.587, 0.114));
                image = lerp(gris.xxx, image, 1.0 + _Vivacite);
                image = (image - 0.5) * _Contrast + 0.5;
                // 0.5 = image neutre. Les deux moities assombrissent ou
                // eclaircissent progressivement sans forcer l'ecran au noir
                // ou au blanc : le joueur regle autour de cette reference.
                float variation = (_Brightness - 0.5) * 0.9;
                image *= 1.0 + variation;
                couleur.rgb = saturate(image);
                return couleur;
            }
            ENDCG
        }
    }
    Fallback Off
}

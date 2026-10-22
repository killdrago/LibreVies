// Reglages d'image visibles dans les options : luminosite, contraste, nettete,
// vivacite, halo des lumieres (bloom), etalonnage chaud/froid et vignettage.
// Passe 0 : image finale. Passes 1 a 3 : halo (seuil, flou horizontal, flou vertical).
Shader "Hidden/LibreVies/ReglagesEcran"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Brightness ("Luminosite", Float) = 0.5
        _Contrast ("Contraste", Float) = 1
        _Nettete ("Nettete", Float) = 0
        _Vivacite ("Vivacite", Float) = 0
        _HaloIntensite ("Halo", Float) = 0
        _Etalonnage ("Etalonnage", Float) = 0
        _Vignettage ("Vignettage", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Passe 0 : image finale
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _Halo;
            float _Brightness;
            float _Contrast;
            float _Nettete;
            float _Vivacite;
            float _HaloIntensite;
            float _Etalonnage;
            float _Vignettage;
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
                // Halo des lumieres (passes 1 a 3), ajoute avant la correction.
                image += tex2D(_Halo, i.uv).rgb * _HaloIntensite;
                // Etalonnage : ombres froides, hautes lumieres chaudes.
                float lum = dot(image, float3(0.299, 0.587, 0.114));
                float3 teinte = lerp(float3(0.97, 1.0, 1.04), float3(1.04, 1.0, 0.95), saturate(lum));
                image = lerp(image, image * teinte, _Etalonnage);
                image = (image - 0.5) * _Contrast + 0.5;
                // 0.5 = image neutre. Les deux moities assombrissent ou
                // eclaircissent progressivement sans forcer l'ecran au noir
                // ou au blanc : le joueur regle autour de cette reference.
                float variation = (_Brightness - 0.5) * 0.9;
                image *= 1.0 + variation;
                // Vignettage : les bords de l'ecran sont un peu plus sombres.
                float rayon = length((i.uv - 0.5) * float2(1.2, 1.0)) * 1.4;
                image *= 1.0 - _Vignettage * smoothstep(0.5, 1.1, rayon);
                couleur.rgb = saturate(image);
                return couleur;
            }
            ENDCG
        }

        // Passe 1 : seuil du halo (ne garde que les zones tres claires)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                float luminosite = max(c.r, max(c.g, c.b));
                float k = saturate((luminosite - 0.85) / 0.15);
                return fixed4(c * k, 1);
            }
            ENDCG
        }

        // Passe 2 : flou horizontal (gaussien, 9 pixels)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float poids[5] = { 0.227, 0.194, 0.121, 0.054, 0.016 };
                float2 d = float2(_MainTex_TexelSize.x * 2.0, 0);
                fixed3 s = tex2D(_MainTex, i.uv).rgb * poids[0];
                for (int k = 1; k < 5; k++)
                    s += (tex2D(_MainTex, i.uv + d * k).rgb + tex2D(_MainTex, i.uv - d * k).rgb) * poids[k];
                return fixed4(s, 1);
            }
            ENDCG
        }

        // Passe 3 : flou vertical
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float poids[5] = { 0.227, 0.194, 0.121, 0.054, 0.016 };
                float2 d = float2(0, _MainTex_TexelSize.y * 2.0);
                fixed3 s = tex2D(_MainTex, i.uv).rgb * poids[0];
                for (int k = 1; k < 5; k++)
                    s += (tex2D(_MainTex, i.uv + d * k).rgb + tex2D(_MainTex, i.uv - d * k).rgb) * poids[k];
                return fixed4(s, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

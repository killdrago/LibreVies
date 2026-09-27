// Reglage luminosite/contraste visible dans les options.
Shader "Hidden/LibreVies/ReglagesEcran"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Brightness ("Luminosite", Float) = 0
        _Contrast ("Contraste", Float) = 1
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
            float _Brightness;
            float _Contrast;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 couleur = tex2D(_MainTex, i.uv);
                fixed3 image = (couleur.rgb - 0.5) * _Contrast + 0.5;
                // 0 = noir absolu, 0.5 = image neutre, 1 = blanc absolu.
                float versNoir = saturate((0.5 - _Brightness) * 2.0);
                float versBlanc = saturate((_Brightness - 0.5) * 2.0);
                image = lerp(image, fixed3(0, 0, 0), versNoir);
                image = lerp(image, fixed3(1, 1, 1), versBlanc);
                couleur.rgb = saturate(image);
                return couleur;
            }
            ENDCG
        }
    }
    Fallback Off
}

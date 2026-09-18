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
                couleur.rgb = (couleur.rgb - 0.5) * _Contrast + 0.5 + _Brightness;
                return saturate(couleur);
            }
            ENDCG
        }
    }
    Fallback Off
}

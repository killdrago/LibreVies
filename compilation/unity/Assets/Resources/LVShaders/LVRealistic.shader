// Materiau PBR procedural de LibreVies.
// Le mapping triplanaire permet aux maillages proceduraux (cubes, terrain,
// toits et rochers) de recevoir une vraie texture sans UV artisanal.
Shader "LibreVies/RealistePBR"
{
    Properties
    {
        _Color ("Teinte", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Tiling ("Echelle monde", Float) = 0.35
        _BumpStrength ("Relief de surface", Range(0,0.5)) = 0.12
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Brillance", Range(0,1)) = 0.35
        _OcclusionStrength ("Occlusion", Range(0,1)) = 0.55
        _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        LOD 350
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Tiling;
        float _BumpStrength;
        float _Metallic;
        float _Smoothness;
        float _OcclusionStrength;
        fixed4 _EmissionColor;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        fixed4 EchantillonnerTriplanaire(float3 position, float3 normale)
        {
            float echelle = max(_Tiling, 0.001);
            float3 poids = abs(normalize(normale));
            poids = poids / max(poids.x + poids.y + poids.z, 0.001);
            fixed4 surX = tex2D(_MainTex, position.yz * echelle);
            fixed4 surY = tex2D(_MainTex, position.xz * echelle);
            fixed4 surZ = tex2D(_MainTex, position.xy * echelle);
            return surX * poids.x + surY * poids.y + surZ * poids.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 echantillon = EchantillonnerTriplanaire(IN.worldPos, IN.worldNormal);
            fixed4 couleur = echantillon * _Color;
            o.Albedo = couleur.rgb;
            o.Alpha = couleur.a;
            o.Metallic = _Metallic;
            // La luminance de l'albedo module legerement la brillance : les
            // creux de pierre/bois sont plus mats que les zones polies.
            float luminance = dot(echantillon.rgb, float3(0.30, 0.59, 0.11));
            o.Smoothness = saturate(_Smoothness * (0.72 + luminance * 0.45));
            o.Occlusion = lerp(1.0, echantillon.r, _OcclusionStrength);
            // Micro-relief derive de la texture : aucune normal map externe
            // n'est necessaire pour casser les surfaces parfaitement plates.
            float reliefX = (echantillon.r - echantillon.b) * _BumpStrength;
            float reliefY = (echantillon.g - echantillon.r) * _BumpStrength;
            o.Normal = normalize(float3(reliefX, reliefY, 1.0));
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    FallBack "Standard"
}

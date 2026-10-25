// Verre bleu transparent pour les fenetres des maisons (version URP).
Shader "LibreVies/VerreBleu"
{
    Properties
    {
        _Color ("Teinte bleue", Color) = (0.12,0.48,0.88,1)
        _MainTex ("Revetement", 2D) = "white" {}
        _Tiling ("Echelle du revetement", Float) = 1.0
        _Alpha ("Transparence", Range(0,1)) = 0.24
        _Metallic ("Metallic", Range(0,1)) = 0.05
        _Smoothness ("Brillance", Range(0,1)) = 0.90
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        LOD 250

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Les deux faces restent visibles pour voir l'interieur depuis les deux cotes.
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_ST;
                float _Tiling;
                float _Alpha;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float echelle = max(_Tiling, 0.01);
                half4 revetement = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * echelle);
                half3 albedo = saturate(revetement.rgb * _Color.rgb);

                float3 normal = normalize(input.normalWS);
                if (!IS_FRONT_VFACE(isFront, true, false)) normal = -normal;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDirection = normalize(mainLight.direction + viewDirection);

                float diffuse = saturate(dot(normal, mainLight.direction));
                float highlight = pow(saturate(dot(normal, halfDirection)), lerp(8.0, 96.0, _Smoothness));
                half3 ambient = SampleSH(normal);
                half3 lumiere = mainLight.color * mainLight.shadowAttenuation;

                half3 couleur = albedo * (ambient * 0.75 + lumiere * diffuse * 0.6);
                couleur += _Color.rgb * 0.035;
                couleur += lumiere * highlight * (0.25 + _Metallic * 0.2);

                float alpha = _Alpha * _Color.a * revetement.a;
                return half4(couleur, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

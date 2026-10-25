// Ciel de secours du projet LibreVies (degrade bleu vif -> horizon chaud), version URP.
// Embarque dans Assets/Resources/ : toujours present dans la build.
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
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SkyUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColor;
                half4 _HorizonColor;
                half4 _GroundColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // La sphere du ciel est centree sur la camera : la position du sommet
                // donne directement la direction a colorer.
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float h = normalize(input.direction).y;
                half4 ciel = lerp(_HorizonColor, _SkyColor, saturate(h * 1.15 + 0.05));
                return lerp(_GroundColor, ciel, saturate(h * 6.0 + 0.55));
            }
            ENDHLSL
        }
    }
    Fallback Off
}

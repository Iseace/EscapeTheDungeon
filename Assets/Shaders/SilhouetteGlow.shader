Shader "Custom/SilhouetteGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1, 0.2, 0.2, 0.45)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SilhouetteGlow"

            // Render on top of everything
            ZTest Always
            ZWrite Off
            Cull Back

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS : POSITION;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

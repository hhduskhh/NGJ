Shader "Echoes/DungeonDarkness"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _PlayerVision;
                float4 _Echoes[32];
                int _EchoCount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.world = world.xy;
                return output;
            }

            float InCircle(float2 worldPosition, float4 circle)
            {
                float distanceFromCenter = length(worldPosition - circle.xy);
                return 1 - smoothstep(circle.z, circle.z + circle.w, distanceFromCenter);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float visible = InCircle(input.world, _PlayerVision);
                [loop] for (int i = 0; i < _EchoCount; i++)
                    visible = max(visible, InCircle(input.world, _Echoes[i]));
                return half4(0, 0, 0, 1 - visible);
            }
            ENDHLSL
        }
    }
}

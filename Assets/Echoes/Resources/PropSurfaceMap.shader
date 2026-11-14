Shader "Echoes/PropSurfaceMap"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _MapSize, _Footprint;
            float _Opacity;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 world = TransformObjectToWorld(input.positionOS).xy;
                float2 clipPosition = (world + .5) / _MapSize.xy * 2 - 1;
                #if UNITY_UV_STARTS_AT_TOP
                    clipPosition.y = -clipPosition.y;
                #endif
                output.positionCS = float4(clipPosition, 0, 1);
                output.uv = input.uv;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                clip(tex2D(_MainTex, input.uv).a * _Opacity - .5);
                return _Footprint;
            }
            ENDHLSL
        }
    }
}

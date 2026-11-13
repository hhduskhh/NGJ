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
                float4 _MapSize;
                float4 _PlayerVision;
                float4 _Echoes[32];
                int _EchoCount;
            CBUFFER_END
            sampler2D _Walkable;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.world = world.xy;
                return output;
            }

            bool InBounds(int2 cell)
            {
                return all(cell >= 0) && all(cell < int2(_MapSize.xy));
            }

            bool IsWalkable(int2 cell)
            {
                return InBounds(cell) && tex2Dlod(_Walkable, float4((cell + .5) * _MapSize.zw, 0, 0)).r > .5;
            }

            // Same cell traversal and corner rule as DungeonMap.HasLineOfSight.
            // Reveal the first wall cell itself, but never the space beyond it.
            bool HasLineOfSight(float2 origin, float2 destination)
            {
                int2 cell = int2(floor(origin + .5));
                int2 goal = int2(floor(destination + .5));
                if (!IsWalkable(cell)) return false;
                float2 direction = destination - origin;
                int2 step = int2(direction.x > 0 ? 1 : -1, direction.y > 0 ? 1 : -1);
                float2 delta = float2(direction.x == 0 ? 1e20 : abs(1 / direction.x),
                    direction.y == 0 ? 1e20 : abs(1 / direction.y));
                float2 next = float2(direction.x == 0 ? 1e20 : (cell.x + step.x * .5 - origin.x) / direction.x,
                    direction.y == 0 ? 1e20 : (cell.y + step.y * .5 - origin.y) / direction.y);
                [loop] for (int i = 0; i < 96; i++) // DungeonMap.Width + DungeonMap.Height
                {
                    if (all(cell == goal)) return true;
                    if (abs(next.x - next.y) <= .000001)
                    {
                        if (!IsWalkable(cell + int2(step.x, 0)) || !IsWalkable(cell + int2(0, step.y))) return false;
                        cell += step;
                        next += delta;
                    }
                    else if (next.x < next.y) { cell.x += step.x; next.x += delta.x; }
                    else { cell.y += step.y; next.y += delta.y; }
                    if (all(cell == goal)) return InBounds(cell);
                    if (!IsWalkable(cell)) return false;
                }
                return false;
            }

            float InCircle(float2 worldPosition, float4 circle, float alreadyVisible)
            {
                float distanceFromCenter = length(worldPosition - circle.xy);
                float coverage = 1 - smoothstep(circle.z, circle.z + circle.w, distanceFromCenter);
                // Avoid grid walks for circles that cannot brighten this pixel.
                [branch] if (coverage <= alreadyVisible) return 0;
                return HasLineOfSight(circle.xy, worldPosition) ? coverage : 0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float visible = InCircle(input.world, _PlayerVision, 0);
                [loop] for (int i = 0; i < _EchoCount; i++)
                {
                    if (visible >= 1) break;
                    visible = max(visible, InCircle(input.world, _Echoes[i], visible));
                }
                return half4(0, 0, 0, 1 - visible);
            }
            ENDHLSL
        }
    }
}

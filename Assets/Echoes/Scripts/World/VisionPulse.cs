using UnityEngine;

namespace Echoes
{
    public sealed class VisionPulse
    {
        public Vector2 Origin { get; }
        public float MaximumRadius { get; }
        public float StartedAt { get; }
        internal LineRenderer Outline;

        public VisionPulse(Vector2 origin, float maximumRadius, float startedAt)
        { Origin = origin; MaximumRadius = maximumRadius; StartedAt = startedAt; }

        public float RadiusAt(float now, GameConfig config) => EvaluateRadius(now - StartedAt,
            MaximumRadius, config.visionExpandDuration, config.visionHoldDuration, config.visionShrinkDuration);

        public static float EvaluateRadius(float age, float maximum, float expand, float hold, float shrink)
        {
            if (age <= 0) return 0;
            if (age < expand)
            {
                float t = age / expand;
                return maximum * (1 - (1 - t) * (1 - t)); // Quadratic easeOut: fast, then slow.
            }
            if (age < expand + hold) return maximum;
            float contraction = Mathf.Clamp01((age - expand - hold) / shrink);
            return maximum * (1 - contraction * contraction); // Quadratic easeIn: slow, then fast.
        }

        public static float Coverage(Vector2 position, Vector2 origin, float mainRadius, float gradientWidth)
        {
            float t = Mathf.Clamp01((Vector2.Distance(position, origin) - mainRadius) / gradientWidth);
            return 1 - t * t * (3 - 2 * t); // Same smooth gradient as the darkness shader.
        }
    }
}

using UnityEngine;

namespace Echoes
{
    public static class ShopQualityRules
    {
        // Level 1 is the starting level: the first increase occurs at level 6.
        public static float[] Weights(GameConfig config, int level)
        {
            var weights = (float[])config.shopTierWeights.Clone();
            int steps = Mathf.Max(0, level - 1) / Mathf.Max(1, config.shopQualityLevelStep);
            Vector3 gains = Vector3.Max(Vector3.zero, config.shopQualityGains);
            float perStep = gains.x + gains.y + gains.z;
            if (perStep <= 0) return weights;
            float transfer = Mathf.Min(weights[0], steps * perStep);
            weights[0] -= transfer;
            for (int tier = 1; tier < 4; tier++) weights[tier] += transfer * gains[tier - 1] / perStep;
            return weights;
        }

        // When a passive tier has no eligible items, retain the relative weights
        // of the remaining tiers instead of drawing uniformly from all items.
        public static int Select(float[] weights, double sample, bool[] eligible = null)
        {
            float total = 0;
            for (int i = 0; i < 4; i++)
                if (eligible == null || eligible[i]) total += Mathf.Max(0, weights[i]);
            if (total <= 0) return -1;
            double value = sample * total;
            int last = -1;
            for (int i = 0; i < 4; i++)
            {
                if ((eligible != null && !eligible[i]) || weights[i] <= 0) continue;
                last = i;
                value -= weights[i];
                if (value < 0) return i;
            }
            return last;
        }
    }
}

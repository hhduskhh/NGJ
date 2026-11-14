using UnityEngine;

namespace Echoes
{
    // Shared by combat and tests. Distance is measured at the moment a shot is fired.
    public static class CombatRules
    {
        public static bool CountsForPressure(bool alive, bool awake, Vector2 enemy,
            Vector2 player, float radius) => alive && awake &&
            (enemy - player).sqrMagnitude <= radius * radius;

        public static float DamageMultiplier(int nearby, int cap, float bonusPerEnemy) =>
            1 + Mathf.Clamp(nearby, 0, cap) * Mathf.Max(0, bonusPerEnemy);

        public static int ExperienceRequired(int level, int first, int step) =>
            first + Mathf.Max(0, level - 1) * step;
    }
}

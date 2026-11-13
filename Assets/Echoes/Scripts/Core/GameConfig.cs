using UnityEngine;

namespace Echoes
{
    [CreateAssetMenu(menuName = "Echoes/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Player")]
        [Min(1)] public float playerHealth = 100;
        [Min(.1f)] public float moveSpeed = 4.5f;
        [Min(1)] public float bulletDamage = 10;
        [Min(.05f)] public float shotInterval = .28f;
        [Min(1)] public float bulletSpeed = 22;
        [Header("Visibility")]
        [Min(1)] public float playerVisionRadius = 3;
        [Min(.1f)] public float echoRevealDuration = 1.5f;
        [Header("Sound — ignores walls in this prototype")]
        [Min(1)] public float gunNoiseRadius = 6;
        [Min(1)] public float waveSpeed = 22;
        [Min(1)] public float pressureRadius = 5;
        [Min(0)] public float bonusPerEnemy = .08f;
        [Min(1)] public int bonusEnemyCap = 8;
        [Header("Finite floor population")]
        [Min(1)] public int enemiesPerRoom = 8;
        [Min(1)] public float enemyHealth = 32;
        [Min(.1f)] public float enemySpeed = 1.7f;
        [Min(1)] public float enemyDamage = 9;
        [Min(1)] public float sightRange = 6;
        [Min(.1f)] public float enemyAttackInterval = 1;
        [Min(.1f)] public float playerInvulnerability = .65f;
        [Header("Progression")]
        [Min(1)] public int experiencePerKill = 1;
        [Min(1)] public int firstLevelExperience = 4;
        [Min(1)] public int experienceStep = 3;
        [Header("Bell decoy")]
        [Min(1)] public float decoyRadius = 9;
        [Min(1)] public float decoyThrowRange = 6;
        [Min(1)] public float decoyDuration = 4;
        [Min(1)] public float decoyCooldown = 12;
        [Header("Gatekeeper")]
        [Min(1)] public float bossHealth = 1100;
        [Min(1)] public float bossDamage = 14;
        [Min(.1f)] public float bossAttackInterval = 2.8f;
        [Min(.1f)] public float bossWarningDuration = .9f;
        [Min(1)] public float bossBulletSpeed = 4;
        [Range(4, 24)] public int bossBulletCount = 12;
        public int randomSeed = 713;
    }
}

using UnityEngine;

namespace Echoes
{
    [CreateAssetMenu(menuName = "Echoes/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Interface feedback")]
        [Min(0)] public float buttonPressDepth = 2f;
        [Min(.01f)] public float healthTrailSeconds = .4f;
        [Min(.01f)] public float floorRevealSeconds = .6f;
        [Min(.01f)] public float victoryRevealSeconds = 1.2f;
        [Header("Player")]
        [Min(1)] public float playerHealth = 100;
        [Min(.1f)] public float moveSpeed = 4.5f;
        [Min(1)] public float bulletDamage = 10;
        [Min(.05f)] public float shotInterval = .28f;
        [Min(1)] public float bulletSpeed = 22;
        [Min(.01f)] public float bulletTrailSeconds = .35f;
        [Min(.1f)] public float bulletTrailRadius = 1.15f;
        [Header("Visibility")]
        [Tooltip("Walking pulse main radius. The stationary main radius is always zero.")]
        [Min(1)] public float playerVisionRadius = 3;
        [Min(.1f)] public float gradientVisionRange = 3;
        [Min(.01f)] public float walkFirstPulseDelay = .2f;
        [Min(.01f)] public float walkPulseInterval = .5f;
        [Min(.01f)] public float visionExpandDuration = .35f;
        [Min(0)] public float visionHoldDuration = .5f;
        [Min(.01f)] public float visionShrinkDuration = .4f;
        [Range(0, .2f)] public float visionRingAlpha = .06f;
        public float VisionPulseDuration => visionExpandDuration + visionHoldDuration + visionShrinkDuration;
        public float WalkingDetectionRadius => playerVisionRadius + gradientVisionRange;
        [Header("Sound — ignores walls in this prototype")]
        [Min(1)] public float gunNoiseRadius = 6;
        [Min(1)] public float pressureRadius = 5;
        [Min(0)] public float bonusPerEnemy = .08f;
        [Min(1)] public int bonusEnemyCap = 8;
        [Header("Dungeon generation")]
        [Min(1)] public float altarEntranceDistance = 3;
        [Min(1)] public int generationAttempts = 100;
        [Range(0, 1)] public float extraDoorChance = .15f;
        [Min(1)] public int enemiesPerCell = 7;
        [Min(1)] public int minEnemiesPerRoom = 6;
        [Min(1)] public int maxEnemiesPerRoom = 32;
        [Tooltip("Tile stride used to collect enemy spawn points; lower values add more points.")]
        [Min(1)] public int spawnCellStride = 2;
        [Header("Room traps")]
        [Min(0)] public float spikeDamage = 10;
        [Min(.1f)] public float spikeRadius = .85f;
        [Min(.1f)] public float spikeRest = 2f;
        [Min(.1f)] public float spikeWarning = .75f;
        [Min(.1f)] public float spikeActive = .65f;
        [Min(0)] public float bombDamage = 16;
        [Min(.1f)] public float bombRadius = 2.1f;
        [Min(.1f)] public float bombFuse = 1.1f;
        [Header("Finite floor population")]
        [Min(1)] public float enemyHealth = 32;
        [Min(.1f)] public float walkerSpeed = 3.8f;
        [Min(.1f)] public float guardSpeed = 1.55f;
        [Min(.1f)] public float guardRange = 9;
        [Min(.1f)] public float guardWindup = .75f;
        [Min(.1f)] public float guardShotInterval = 2.2f;
        [Min(.1f)] public float guardBulletSpeed = 5.5f;
        [Min(1)] public float enemyDamage = 9;
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
        [Min(.1f)] public float bossSpeed = 1.275f;
        [Min(1)] public float bossHealth = 1100;
        [Min(1)] public float bossDamage = 14;
        [Min(.1f)] public float bossAttackInterval = 2.8f;
        [Min(.1f)] public float bossWarningDuration = .9f;
        [Min(1)] public float bossBulletSpeed = 4;
        [Range(4, 24)] public int bossBulletCount = 12;
        [Header("Floor scaling")]
        public float[] floorEnemyHealth = { 1f, 1.2f, 1.4f };
        public float[] floorEnemyDamage = { 1f, 1.1f, 1.2f };
        public float[] floorBossHealth = { 1f, 1.35f, 1.7f };
        [Range(0, 1)] public float floorEntryHealFraction = .3f;
        [Header("Shop and loot")]
        [Min(1)] public int goldPerKill = 6;
        [Min(.2f)] public float goldPickupRadius = 1.5f;
        [Min(.5f)] public float shopInteractRadius = 2.5f;
        [Min(1)] public int shopRefreshCost = 4;
        [Min(0)] public int shopRefreshIncrease = 2;
        public float[] shopTierWeights = { 65, 25, 8, 2 };
        [Min(1)] public int shopQualityLevelStep = 5;
        [Tooltip("每档从品质0转移给品质1、2、3的权重；品质0耗尽后停止增长。")]
        public Vector3 shopQualityGains = new Vector3(2, 2, 1);
        public int[] passivePrices = { 18, 36, 60, 90 };
        public WeaponDefinition[] weapons = WeaponDefinition.Defaults();
        public PassiveDefinition[] passives = PassiveDefinition.Defaults();
        public int randomSeed = 713;
    }
}

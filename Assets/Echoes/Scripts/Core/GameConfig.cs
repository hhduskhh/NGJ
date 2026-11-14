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
        [Header("Minimap")]
        [Min(80)] public float minimapSize = 200;
        [Min(4)] public float minimapMarkerSize = 8;
        [Range(1, 3)] public int minimapOutlineTiles = 1;
        public Color minimapRoomOutline = new Color32(99, 226, 221, 255);
        public Color minimapFrontierDoor = new Color32(255, 204, 84, 255);
        public Color minimapExploredDoor = new Color32(194, 202, 205, 255);
        [Header("Player")]
        [Min(1)] public float playerHealth = 100;
        [Min(.1f)] public float moveSpeed = 9f;
        [Min(1)] public float bulletDamage = 10;
        [Min(.05f)] public float shotInterval = .28f;
        [Min(1)] public float bulletSpeed = 33;
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
        [Header("Combat audio")]
        [Range(0, 1)] public float weaponVolume = .15f;
        [Range(0, 1)] public float pickupVolume = .1f;
        [Range(0, 1)] public float hitVolume = .09f;
        [Range(0, 1)] public float warningVolume = .16f;
        [Range(0, 1)] public float wakeVolume = .14f;
        [Range(0, 1)] public float enemyStepVolume = .07f;
        [Min(.1f)] public float enemyStepDistance = 2.4f;
        [Header("Sound — ignores walls in this prototype")]
        [Min(1)] public float gunNoiseRadius = 6;
        [Min(0)] public float bonusPerEnemy = .08f;
        [Min(1)] public int bonusEnemyCap = 8;
        [Header("Dungeon generation")]
        [Min(0)] public int maxFurniturePerCell = 5;
        [Min(1)] public float altarEntranceDistance = 3;
        [Min(1)] public int generationAttempts = 100;
        [Range(0, 1)] public float extraDoorChance = .15f;
        [Tooltip("每层地图房间格的边长；索引 0/1/2 对应第 1/2/3 层。")]
        public int[] floorGridSizes = { 3, 4, 5 };
        [Tooltip("每层随机选择中间房格子的次数；索引 0/1/2 对应第 1/2/3 层。")]
        public int[] spawnCellDraws = { 24, 64, 120 };
        [Tooltip("每层每个格子的刷新权重；同一层内所有格子目前使用相同权重。")]
        public int[] spawnCellWeights = { 1, 1, 1 };
        [Tooltip("每层每次选中格子时生成的小怪数量下限。")]
        public int[] spawnAmountMin = { 1, 4, 9 };
        [Tooltip("每层每次选中格子时生成的小怪数量上限。")]
        public int[] spawnAmountMax = { 3, 8, 15 };
        [Tooltip("Tile stride used to collect enemy spawn points; lower values add more points.")]
        [Min(1)] public int spawnCellStride = 2;
        [Header("Room traps")]
        [Min(0)] public int trapAnchorSearchRadius = 3;
        [Min(0)] public int trapDoorClearance = 2;
        [Min(0)] public float spikeDamage = 10;
        [Min(.1f)] public float spikeRadius = .85f;
        [Min(.1f)] public float spikeRest = 2f;
        [Min(.1f)] public float spikeWarning = .75f;
        [Min(.1f)] public float spikeActive = .65f;
        [Min(0)] public float bombDamage = 48;
        [Min(.1f)] public float bombRadius = 2.1f;
        [Min(.1f)] public float bombFuse = 1.1f;
        [Min(.05f)] public float bombFlashSeconds = .4f;
        [Header("Finite floor population")]
        [Min(1)] public float enemyHealth = 32;
        [Min(.1f)] public float walkerSpeed = 7.6f;
        [Min(.1f)] public float guardSpeed = 3.1f;
        [Min(.1f)] public float guardRange = 9;
        [Min(.1f)] public float guardWindup = .75f;
        [Min(.1f)] public float guardShotInterval = 2.2f;
        [Min(.1f)] public float guardBulletSpeed = 8.25f;
        [Min(1)] public float enemyDamage = 9;
        [Min(.1f)] public float enemyAttackInterval = 1;
        [Min(.1f)] public float playerInvulnerability = .65f;
        [Min(0)] public float corpseHoldSeconds = .3f;
        [Min(.05f)] public float corpseFadeSeconds = 1.1f;
        [Header("Progression")]
        [Min(1)] public int experiencePerKill = 1;
        [Min(1)] public int firstLevelExperience = 4;
        [Min(1)] public int experienceStep = 3;
        [Header("Full-screen light")]
        [Min(.1f)] public float screenLightDuration = 5;
        [Min(.05f)] public float screenLightWaveDuration = .7f;
        [Min(.1f)] public float screenLightCooldown = 15;
        [Range(0, 1)] public float screenLightVolume = .16f;

        [Header("Bell decoy")]
        [Min(1)] public float decoyRadius = 9;
        [Min(.05f)] public float decoyFlightSeconds = .4f;
        [Min(0)] public float decoyArcHeight = 1.2f;
        [Min(1)] public float decoyDuration = 4;
        [Min(1)] public float decoyCooldown = 12;
        [Header("Gatekeeper")]
        [Min(.1f)] public float bossSpeed = 2.55f;
        [Min(1)] public float bossHealth = 1100;
        [Min(1)] public float bossDamage = 14;
        [Min(.1f)] public float bossAttackInterval = .8f;
        [Min(.1f)] public float bossWarningDuration = .4f;
        [Min(.1f)] public float bossDashInterval = 1.4f;
        [Min(.1f)] public float bossDashDuration = .32f;
        [Min(.1f)] public float bossDashSpeed = 11f;
        [Min(1)] public float finalBossAttackRate = 2f;
        [Min(1)] public float bossBulletSpeed = 6;
        [Min(1)] public float bossBulletScale = 1.35f;
        [Range(4, 24)] public int bossBulletCount = 18;
        [Min(.1f)] public float bossSummonInterval = 8f;
        [Min(1)] public int bossSummonCount = 2;
        [Min(1)] public int bossSummonLimit = 4;
        [Header("Floor scaling")]
        public float[] floorEnemyHealth = { 1f, 2.4f, 4.2f };
        public float[] floorEnemyDamage = { 1f, 1.1f, 1.2f };
        public float[] floorBossHealth = { 2f, 6.75f, 17f };
        [Range(0, 1)] public float floorEntryHealFraction = .3f;
        [Header("Shop and loot")]
        [Min(1)] public int goldPerKill = 6;
        [Min(.2f)] public float goldPickupRadius = 4f;
        [Min(.1f)] public float goldPullSpeed = 3f;
        [Min(.1f)] public float goldPullAcceleration = 36f;
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

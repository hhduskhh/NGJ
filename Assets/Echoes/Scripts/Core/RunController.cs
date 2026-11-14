using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    public enum RunState { Playing, Upgrade, Paused, Shop, FloorCleared, Won, Lost }

    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        public GameConfig Config => config;
        public DungeonMap Map { get; private set; }
        public PlayerController Player { get; private set; }
        public NoiseSystem Noise { get; private set; }
        public ShopController Shop { get; private set; }
        public GameHud Hud { get; private set; }
        public DungeonVisibility Visibility { get; private set; }
        public EnemyController Boss { get; private set; }
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public RunState State { get; private set; }
        public int Floor { get; private set; } = 1;
        public string FloorTitle { get { return FloorTheme.Title(Floor); } }
        public float EnemyHealthScale { get { return FloorScale(config.floorEnemyHealth); } }
        public float EnemyDamageScale { get { return FloorScale(config.floorEnemyDamage); } }
        public float BossHealthScale { get { return FloorScale(config.floorBossHealth); } }
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public int Experience { get; private set; }
        public int Level { get; private set; } = 1;
        public int NearbyEnemies { get; private set; }
        public float DamageMultiplier => CombatRules.DamageMultiplier(NearbyEnemies,
            Player.PressureCap, Player.PressureBonus);
        public int RequiredExperience => CombatRules.ExperienceRequired(Level,
            config.firstLevelExperience, config.experienceStep);
        public bool BossActivated { get; private set; }
        public bool Muted { get; private set; }
        public bool FeelDebugMode { get; private set; }
        public void SetFeelDebugMode(bool enabled) => FeelDebugMode = enabled;
        public List<UpgradeDefinition> Choices { get; private set; }
        public System.Random Random { get; private set; }
        private int pendingUpgradeCost;

        private void Start()
        {
            Time.timeScale = 1;
            AudioListener.volume = 1;
            Random = new System.Random(config.randomSeed);
            Floor = 1;
            Map = gameObject.AddComponent<DungeonMap>();
            Map.Generate(this, Floor - 1);
            Noise = gameObject.AddComponent<NoiseSystem>();
            Noise.Initialize(this);
            Player = PlayerController.Create(this, Map.StartPosition);
            Player.InitializeWeapons();
            Camera.main.gameObject.AddComponent<CameraFollow>().Target = Player.transform;
            Shop = new GameObject("FloorShop").AddComponent<ShopController>();
            Shop.Initialize(this);
            SpawnFloor();
            Visibility = gameObject.AddComponent<DungeonVisibility>();
            Visibility.Initialize(this);
            Hud = gameObject.AddComponent<GameHud>();
            Hud.Initialize(this);
        }

        private void SpawnFloor()
        {
            // Spawn once. Sound, upgrades and the boss never create additional enemies.
            for (int room = 1; room < Map.Rooms.Count - 1; room++)
            {
                var cells = Map.SpawnCells(room);
                int perCell = Mathf.Max(1, config.enemiesPerCell);
                int min = Mathf.Max(1, config.minEnemiesPerRoom);
                int max = Mathf.Max(min, config.maxEnemiesPerRoom);
                int count = Mathf.Clamp(
                    Mathf.RoundToInt(Map.Rooms[room].CellCount * perCell), min, max);
                for (int i = 0; i < count && i < cells.Count; i++)
                    Enemies.Add(EnemyController.Create(this, cells[i], false));
            }
            Boss = EnemyController.Create(this, Map.BossPosition, true);
            Enemies.Add(Boss);
        }

        private float FloorScale(float[] values)
        {
            if (values == null || values.Length == 0) return 1;
            return values[Mathf.Clamp(Floor - 1, 0, values.Length - 1)];
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                Muted = !Muted;
                AudioListener.volume = Muted ? 0 : 1;
            }
            if (State == RunState.FloorCleared && Input.GetKeyDown(KeyCode.E)) { AdvanceFloor(); return; }
            if (State == RunState.Shop && Input.GetKeyDown(KeyCode.Escape)) { Shop.Close(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) &&
                (State == RunState.Playing || State == RunState.Paused))
                SetState(State == RunState.Playing ? RunState.Paused : RunState.Playing);
            if (State != RunState.Playing) return;
            if (Input.GetKeyDown(KeyCode.E) && Shop.Open()) return;
            Elapsed += Time.deltaTime;
            RefreshPressure();
            if (!BossActivated && Vector2.Distance(Player.transform.position, Map.AltarPosition) < 3 &&
                Input.GetKeyDown(KeyCode.E)) ActivateBoss();
        }

        public void RefreshPressure()
        {
            NearbyEnemies = 0;
            foreach (var enemy in Enemies)
                if (enemy && CombatRules.CountsForPressure(enemy.Alive, enemy.Awake,
                    enemy.transform.position, Player.transform.position, config.pressureRadius))
                    NearbyEnemies++;
        }

        public void ActivateBoss()
        {
            if (BossActivated || State != RunState.Playing) return;
            BossActivated = true;
            Boss.ActivateBoss();
            Hud.Notify("守关者已苏醒 · 击败它即可通关", 4);
        }

        public void EnemyKilled(EnemyController enemy)
        {
            Kills++;
            if (enemy.IsBoss)
            {
                SetState(Floor >= FloorTheme.Count ? RunState.Won : RunState.FloorCleared);
                return;
            }
            GoldPickup.Drop(this, enemy.transform.position, config.goldPerKill);
            Player.Passives.OnKill();
            Experience += config.experiencePerKill;
            TryLevelUp();
        }

        public void AdvanceFloor()
        {
            if (State != RunState.FloorCleared || Floor >= FloorTheme.Count) return;
            Floor++;
            ClearFloorObjects();
            Map.Generate(this, Floor - 1);
            Player.PlaceAt(Map.StartPosition);
            Player.Heal(Player.MaxHealth * config.floorEntryHealFraction);
            Shop.BeginFloor(Map.StartPosition);
            SpawnFloor();
            Visibility.Rebuild();
            Hud.RebuildMinimap();
            BossActivated = false;
            SetState(RunState.Playing);
            Hud.Notify("进入第 " + Floor + " 层 · " + FloorTheme.Title(Floor), 4);
        }

        private void ClearFloorObjects()
        {
            foreach (var enemy in Enemies) if (enemy) Destroy(enemy.gameObject);
            Enemies.Clear();
            Boss = null;
            foreach (var projectile in FindObjectsOfType<Projectile>())
                if (projectile) Destroy(projectile.gameObject);
            foreach (var decoy in FindObjectsOfType<BellDecoy>())
                if (decoy) Destroy(decoy.gameObject);
            foreach (var wave in FindObjectsOfType<NoiseWave>())
                if (wave) Destroy(wave.gameObject);
            foreach (var effect in FindObjectsOfType<ArtEffect>())
                if (effect) Destroy(effect.gameObject);
            foreach (var gold in FindObjectsOfType<GoldPickup>())
                if (gold) Destroy(gold.gameObject);
            Visibility?.ClearPulses();
        }

        private void TryLevelUp()
        {
            if (State != RunState.Playing || Experience < RequiredExperience) return;
            pendingUpgradeCost = RequiredExperience;
            Choices = UpgradeDefinition.Roll(Random);
            SetState(RunState.Upgrade);
        }

        public void ChooseUpgrade(int index)
        {
            if (State != RunState.Upgrade || index < 0 || index >= Choices.Count) return;
            Choices[index].Apply(Player);
            Experience -= pendingUpgradeCost;
            Level++;
            pendingUpgradeCost = 0;
            SetState(RunState.Playing);
            TryLevelUp(); // Preserve overflow and queue additional levels without discarding XP.
        }

        public void SetState(RunState state)
        {
            if (State == RunState.Upgrade && state == RunState.Paused) return;
            State = state;
            Time.timeScale = state == RunState.Playing ? 1 : 0;
            Hud?.RefreshModal();
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDestroy() { Time.timeScale = 1; AudioListener.volume = 1; }
    }
}

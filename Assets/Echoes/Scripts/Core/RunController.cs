using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    public enum RunState { Playing, Upgrade, Paused, Shop, FloorCleared, Won, Lost, Backpack }

    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.12f;
        public GameConfig Config => config;
        public DungeonMap Map { get; private set; }
        public PlayerController Player { get; private set; }
        public NoiseSystem Noise { get; private set; }
        public ShopController Shop { get; private set; }
        public GameHud Hud { get; private set; }
        public DungeonVisibility Visibility { get; private set; }
        public EnemyController Boss { get; private set; }
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        private readonly List<EnemyController> bossMinions = new List<EnemyController>();
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
        private static int? restartSeed;
        private RunState backpackReturnState;

        private void Start()
        {
            Time.timeScale = 1;
            AudioListener.volume = 1;
            config = Instantiate(config);
            if (restartSeed.HasValue) config.randomSeed = restartSeed.Value;
            restartSeed = null;
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
            var explorationMusic = gameObject.AddComponent<AudioSource>();
            explorationMusic.playOnAwake = false;
            explorationMusic.clip = Resources.Load<AudioClip>("EmptyCity");
            explorationMusic.loop = true;
            explorationMusic.volume = musicVolume;
            explorationMusic.Play();
        }

        private void SpawnFloor()
        {
            // Initial room population. The activated boss summons reinforcements separately.
            var spawnAreas = new List<DungeonMap.SpawnArea>();
            for (int room = 1; room < Map.Rooms.Count - 1; room++)
                spawnAreas.AddRange(Map.SpawnAreas(room));
            SpawnSelectedCells(spawnAreas);
            Boss = EnemyController.Create(this, Map.BossPosition, true);
            Enemies.Add(Boss);
        }

        private void SpawnSelectedCells(List<DungeonMap.SpawnArea> areas)
        {
            int draws = Mathf.Max(0, FloorValue(config.spawnCellDraws, 24));
            int min = Mathf.Max(1, FloorValue(config.spawnAmountMin, 1));
            int max = Mathf.Max(min, FloorValue(config.spawnAmountMax, 3));
            int weight = Mathf.Max(1, FloorValue(config.spawnCellWeights, 1));
            // Spawn rolls use their own per-floor seed so combat randomness cannot
            // shift the layout of later floors.
            var spawnRandom = new System.Random(config.randomSeed + Floor * 1013 + 29);
            for (int draw = 0; draw < draws; draw++)
            {
                int totalWeight = 0;
                foreach (var area in areas)
                    if (area.Points.Count > 0) totalWeight += weight;
                if (totalWeight <= 0) break;

                int roll = spawnRandom.Next(totalWeight);
                DungeonMap.SpawnArea chosen = null;
                foreach (var area in areas)
                {
                    if (area.Points.Count == 0) continue;
                    if (roll < weight) { chosen = area; break; }
                    roll -= weight;
                }
                if (chosen == null) continue;

                int count = Mathf.Min(spawnRandom.Next(min, max + 1), chosen.Points.Count);
                for (int i = 0; i < count; i++)
                {
                    int index = spawnRandom.Next(chosen.Points.Count);
                    Vector2 point = chosen.Points[index];
                    chosen.Points[index] = chosen.Points[chosen.Points.Count - 1];
                    chosen.Points.RemoveAt(chosen.Points.Count - 1);
                    Enemies.Add(EnemyController.Create(this, point, false));
                }
            }
        }

        private int FloorValue(int[] values, int fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            return values[Mathf.Clamp(Floor - 1, 0, values.Length - 1)];
        }

        private float FloorScale(float[] values)
        {
            if (values == null || values.Length == 0) return 1;
            return values[Mathf.Clamp(Floor - 1, 0, values.Length - 1)];
        }

        internal void SummonBossMinions()
        {
            int alive = 0;
            for (int i = bossMinions.Count - 1; i >= 0; i--)
            {
                var minion = bossMinions[i];
                if (minion.Alive) alive++;
                else if (!minion.gameObject.activeSelf)
                {
                    Enemies.Remove(minion);
                    bossMinions.RemoveAt(i);
                    Destroy(minion.gameObject);
                }
            }
            int remaining = Mathf.Min(config.bossSummonCount, config.bossSummonLimit - alive);
            Vector2 origin = Boss.transform.position;
            var room = Map.RoomAtPosition(origin);
            if (remaining <= 0 || room == null) return;
            var cells = Map.SpawnCells(room.Index);
            cells.Sort((a, b) => (a - origin).sqrMagnitude.CompareTo((b - origin).sqrMagnitude));
            Vector2 player = Player.transform.position;
            foreach (var cell in cells)
            {
                if ((cell - player).sqrMagnitude < 9f ||
                    Enemies.Exists(e => e.Alive && ((Vector2)e.transform.position - cell).sqrMagnitude < 2.25f)) continue;
                var minion = EnemyController.Create(this, cell, false);
                Enemies.Add(minion);
                bossMinions.Add(minion);
                minion.Hear(player);
                if (--remaining == 0) break;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                Muted = !Muted;
                AudioListener.volume = Muted ? 0 : 1;
            }
            if (State == RunState.FloorCleared || State == RunState.Won)
            {
                Hud.ReadResultInput();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (State == RunState.Backpack) CloseBackpack();
                else OpenBackpack();
                return;
            }
            if (State == RunState.Backpack && Input.GetKeyDown(KeyCode.Escape)) { CloseBackpack(); return; }
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
                foreach (var gold in FindObjectsOfType<GoldPickup>()) gold.Collect();
                SetState(Floor >= FloorTheme.Count ? RunState.Won : RunState.FloorCleared);
                return;
            }
            bool doubleLoot = Player.DoubleLootChance > 0 && Random.NextDouble() < Player.DoubleLootChance;
            GoldPickup.Drop(this, enemy.transform.position, config.goldPerKill * (doubleLoot ? 2 : 1));
            Player.Passives.OnKill();
            Experience += config.experiencePerKill;
            TryLevelUp();
        }

        public void AdvanceFloor()
        {
            if (State != RunState.FloorCleared || Floor >= FloorTheme.Count) return;
            Floor++;
            BossActivated = false;
            ClearFloorObjects();
            Map.Generate(this, Floor - 1);
            Player.PlaceAt(Map.StartPosition);
            Player.Heal(Player.MaxHealth * config.floorEntryHealFraction);
            Shop.BeginFloor(Map.StartPosition);
            SpawnFloor();
            Visibility.Rebuild();
            Hud.RebuildMinimap();
            SetState(RunState.Playing);
            Hud.Notify("进入第 " + Floor + " 层 · " + FloorTheme.Title(Floor), 4);
        }

        private void ClearFloorObjects()
        {
            foreach (var enemy in Enemies) if (enemy) Destroy(enemy.gameObject);
            Enemies.Clear();
            bossMinions.Clear();
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
            Choices = UpgradeDefinition.Roll(Random, Player);
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

        public void OpenBackpack()
        {
            if (State != RunState.Playing && State != RunState.Shop && State != RunState.Paused && State != RunState.Upgrade) return;
            backpackReturnState = State;
            SetState(RunState.Backpack);
        }

        public void CloseBackpack()
        {
            if (State == RunState.Backpack) SetState(backpackReturnState);
        }

        public void SetState(RunState state)
        {
            if (State == RunState.Upgrade && state == RunState.Paused) return;
            State = state;
            Noise?.PauseActionAudio(state != RunState.Playing);
            Time.timeScale = state == RunState.Playing ? 1 : 0;
            Hud?.RefreshModal();
        }

        public void Restart()
        {
            var next = System.Guid.NewGuid().GetHashCode() & int.MaxValue;
            restartSeed = next == config.randomSeed ? next ^ 1 : next;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDestroy()
        {
            if (Random != null) Destroy(config);
            Time.timeScale = 1;
            AudioListener.volume = 1;
        }
    }
}

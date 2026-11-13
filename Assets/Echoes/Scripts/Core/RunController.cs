using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    public enum RunState { Playing, Upgrade, Paused, Won, Lost }

    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        public GameConfig Config => config;
        public DungeonMap Map { get; private set; }
        public PlayerController Player { get; private set; }
        public NoiseSystem Noise { get; private set; }
        public GameHud Hud { get; private set; }
        public EnemyController Boss { get; private set; }
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public RunState State { get; private set; }
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public int Experience { get; private set; }
        public int Level { get; private set; } = 1;
        public int NearbyEnemies { get; private set; }
        public float DamageMultiplier => CombatRules.DamageMultiplier(NearbyEnemies,
            config.bonusEnemyCap, config.bonusPerEnemy);
        public int RequiredExperience => CombatRules.ExperienceRequired(Level,
            config.firstLevelExperience, config.experienceStep);
        public bool BossActivated { get; private set; }
        public bool Muted { get; private set; }
        public List<UpgradeDefinition> Choices { get; private set; }
        public System.Random Random { get; private set; }

        private void Start()
        {
            Time.timeScale = 1;
            AudioListener.volume = 1;
            Random = new System.Random(config.randomSeed);
            Map = gameObject.AddComponent<DungeonMap>();
            Map.Generate(this);
            Noise = gameObject.AddComponent<NoiseSystem>();
            Noise.Initialize(this);
            Player = PlayerController.Create(this, Map.Rooms[0].Center);
            Camera.main.gameObject.AddComponent<CameraFollow>().Target = Player.transform;
            SpawnFloor();
            Hud = gameObject.AddComponent<GameHud>();
            Hud.Initialize(this);
        }

        private void SpawnFloor()
        {
            // Spawn once. Sound, upgrades and the boss never create additional enemies.
            for (int room = 1; room < Map.Rooms.Count - 1; room++)
            {
                var cells = Map.SpawnCells(room);
                for (int i = 0; i < config.enemiesPerRoom && i < cells.Count; i++)
                    Enemies.Add(EnemyController.Create(this, cells[i], false));
            }
            Boss = EnemyController.Create(this, Map.BossPosition, true);
            Enemies.Add(Boss);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                Muted = !Muted;
                AudioListener.volume = Muted ? 0 : 1;
            }
            if (Input.GetKeyDown(KeyCode.Escape) &&
                (State == RunState.Playing || State == RunState.Paused))
                SetState(State == RunState.Playing ? RunState.Paused : RunState.Playing);
            if (State != RunState.Playing) return;
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
            if (enemy.IsBoss) { SetState(RunState.Won); return; }
            Experience += config.experiencePerKill;
            TryLevelUp();
        }

        private void TryLevelUp()
        {
            if (State != RunState.Playing || Experience < RequiredExperience) return;
            Experience -= RequiredExperience;
            Level++;
            Choices = UpgradeDefinition.Roll(Random);
            SetState(RunState.Upgrade);
        }

        public void ChooseUpgrade(int index)
        {
            if (State != RunState.Upgrade || index < 0 || index >= Choices.Count) return;
            Choices[index].Apply(Player);
            SetState(RunState.Playing);
            TryLevelUp(); // Preserve overflow and queue additional levels without discarding XP.
        }

        public void SetState(RunState state)
        {
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

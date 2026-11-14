using UnityEngine;

namespace Echoes
{
    public sealed class PlayerController : MonoBehaviour
    {
        private RunController run;
        private Rigidbody2D body;
        private SpriteRenderer sprite;
        public WeaponInventory Weapons { get; private set; }
        public PassiveInventory Passives { get; private set; }
        private CharacterArt art;
        private LineRenderer pressureRing;
        private bool waitForFireRelease;
        private Vector2 movement, aim = Vector2.right;
        private float invulnerableUntil, decoyReadyAt;
        public float Health { get; private set; }
        public float MaxHealth { get; set; }
        public float DamageScale { get; set; } = 1;
        public float MoveScale { get; set; } = 1;
        public float ShotIntervalScale { get; set; } = 1;
        public float DecoyCooldownReduction { get; set; }
        public float CriticalDamageBonus { get; set; }
        public float PressureBonusPerEnemy { get; set; }
        public float DoubleLootChance { get; set; }
        public float NoiseRadiusBonus { get; set; }
        public float NoiseRadius => Mathf.Clamp(run.Config.gunNoiseRadius + NoiseRadiusBonus + Passives.Noise, 3, 12);
        public float AttackSpeed => Mathf.Max(.1f, 1 + Passives.AttackSpeed + Passives.ConvertedAttackSpeed) / ShotIntervalScale;
        public float CriticalChance => Mathf.Clamp01(.1f + Passives.Critical);
        public float CriticalMultiplier => Mathf.Clamp(2 + CriticalDamageBonus + Passives.CriticalDamage, 1, 5);
        public float TotalDamageScale => Mathf.Max(.1f, DamageScale + Passives.Damage + Passives.ConvertedDamage +
            (Passives.Has(ItemSpecial.LoneFighter) ? (run.NearbyEnemies <= 1 ? .3f : -.1f) : 0));
        public float PressureBonus => Mathf.Clamp(run.Config.bonusPerEnemy + PressureBonusPerEnemy + Passives.Pressure, 0, .2f);
        public int PressureCap => Mathf.Clamp(run.Config.bonusEnemyCap + Passives.PressureCap, 1, 12);
        public float DecoyCooldownRemaining => Mathf.Max(0, decoyReadyAt - Time.time);
        public float DecoyCooldown => Mathf.Max(4, run.Config.decoyCooldown * Mathf.Max(.1f, 1 + Passives.DecoyCooldown) - DecoyCooldownReduction);
        public Vector2 AimPoint { get; private set; }
        public Vector2 AimDirection => aim;
        public EnemyController AimTarget { get; private set; }

        public static PlayerController Create(RunController run, Vector2 position)
        {
            var renderer = PixelArt.Create("Explorer", "player", position, 10);
            var player = renderer.gameObject.AddComponent<PlayerController>();
            player.run = run; player.sprite = renderer;
            player.MaxHealth = player.Health = run.Config.playerHealth;
            player.body = renderer.gameObject.AddComponent<Rigidbody2D>();
            player.body.gravityScale = 0;
            player.body.freezeRotation = true;
            player.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var hitbox = renderer.gameObject.AddComponent<CircleCollider2D>();
            hitbox.radius = .33f;
            hitbox.offset = Vector2.zero; // Bottom-pivot sprites must not auto-fit the collider to the head.
            player.art = renderer.gameObject.AddComponent<CharacterArt>();
            player.art.Initialize("hero");
            player.Passives = new PassiveInventory(player);
            player.pressureRing = PixelArt.Circle("PressureRadius", player.transform,
                new Color(.36f, .89f, .86f, .16f), .025f);
            PixelArt.SetRadius(player.pressureRing, run.Config.pressureRadius);
            return player;
        }

        private void Update()
        {
            if (run.State != RunState.Playing) { movement = Vector2.zero; waitForFireRelease = true; return; }
            movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            AimPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.F)) run.Visibility.TryLightScreen();
            UpdateAim(AimPoint, false);
            Weapons.Tick(Time.deltaTime, AimPoint);
            if (!Input.GetMouseButton(0)) waitForFireRelease = false;
            else if (Input.GetMouseButtonDown(0) && UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                waitForFireRelease = true;
            if (!waitForFireRelease && Input.GetMouseButton(0)) TryFire(AimPoint);
            if (Input.GetKeyDown(KeyCode.Q) && DecoyCooldownRemaining <= 0) ThrowDecoy();
        }

        private void FixedUpdate()
        {
            run.Visibility.UpdateWalking(body.position, Time.fixedDeltaTime);
            body.velocity = run.State == RunState.Playing ? movement * run.Config.moveSpeed * Mathf.Clamp(MoveScale + Passives.MoveSpeed, .6f, 1.6f) : Vector2.zero;
        }

        private void UpdateAim(Vector2 manualPoint, bool forceManualAim)
        {
            AimTarget = forceManualAim ? null : FindVisibleTarget();
            var target = AimTarget ? (Vector2)AimTarget.transform.position : manualPoint;
            var direction = target - (Vector2)transform.position;
            if (direction.sqrMagnitude > .01f) aim = direction.normalized;
            sprite.flipX = aim.x < 0;
        }

        public EnemyController FindVisibleTarget(Vector2? muzzle = null)
        {
            EnemyController nearest = null;
            float closest = float.MaxValue;
            foreach (var enemy in run.Enemies)
            {
                if (!enemy || !enemy.Alive || (enemy.IsBoss && !run.BossActivated)) continue;
                Vector2 position = enemy.transform.position;
                float distance = (position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= closest || !run.Visibility.IsVisible(position)) continue;
                var viewport = Camera.main.WorldToViewportPoint(position);
                if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                if (!run.Map.HasLineOfSight(transform.position, position)) continue;
                if (!run.Map.HasFiringLine(muzzle ?? (Vector2)transform.position, position, enemy.transform)) continue;
                nearest = enemy; closest = distance;
            }
            return nearest;
        }

        // Skills may explicitly opt out; normal bullets fix their direction when fired, without homing.
        public bool TryFire(Vector2 manualPoint, bool forceManualAim = false)
        {
            if (run.State != RunState.Playing) return false;
            UpdateAim(manualPoint, forceManualAim);
            bool fired = Weapons.Fire(manualPoint, forceManualAim);
            if (fired) art.Attack();
            return fired;
        }

        public void InitializeWeapons() => Weapons = new WeaponInventory(run);

        public void PlaceAt(Vector2 position)
        {
            body.velocity = Vector2.zero;
            body.position = position;
            transform.position = position;
            invulnerableUntil = Time.time + .5f;
            waitForFireRelease = true;
        }

        private void ThrowDecoy()
        {
            var delta = Vector2.ClampMagnitude(AimPoint - (Vector2)transform.position, run.Config.decoyThrowRange);
            var point = run.Map.NearestFloor((Vector2)transform.position + delta);
            // Do not throw through walls, even though the resulting sound ignores walls.
            if (!run.Map.HasClearInteractionPath(transform.position, point))
            { run.Hud.Notify("诱饵投掷路径被墙壁挡住", 2); return; }
            BellDecoy.Create(run, point);
            decoyReadyAt = Time.time + DecoyCooldown;
        }

        public void TakeDamage(float amount)
        {
            if (run.State != RunState.Playing || Time.time < invulnerableUntil) return;
            if (run.Random.NextDouble() < Mathf.Clamp(Passives.Dodge, 0, .5f))
            { invulnerableUntil = Time.time + .15f; return; }
            Health = Mathf.Max(0, Health - amount);
            if (amount > 0) run.Noise.PlayHurt();
            art.Hurt(run.Config.playerInvulnerability);
            invulnerableUntil = Time.time + run.Config.playerInvulnerability;
            if (Health <= 0) { run.SetState(RunState.Lost); }
        }

        public void Heal(float amount) => Health = Mathf.Min(MaxHealth, Health + amount);
    }
}

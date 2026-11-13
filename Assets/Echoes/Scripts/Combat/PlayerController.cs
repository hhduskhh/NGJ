using UnityEngine;

namespace Echoes
{
    public sealed class PlayerController : MonoBehaviour
    {
        private RunController run;
        private Rigidbody2D body;
        private SpriteRenderer sprite;
        private Transform gun;
        private LineRenderer pressureRing;
        private Vector2 movement, aim = Vector2.right;
        private float shotTimer, invulnerableUntil, decoyReadyAt;
        public float Health { get; private set; }
        public float MaxHealth { get; set; }
        public float DamageScale { get; set; } = 1;
        public float MoveScale { get; set; } = 1;
        public float ShotIntervalScale { get; set; } = 1;
        public float DecoyCooldownScale { get; set; } = 1;
        public float NoiseRadiusBonus { get; set; }
        public float NoiseRadius => run.Config.gunNoiseRadius + NoiseRadiusBonus;
        public float DecoyCooldownRemaining => Mathf.Max(0, decoyReadyAt - Time.time);
        public Vector2 AimPoint { get; private set; }
        public Vector2 AimDirection => aim;
        public EnemyController AimTarget { get; private set; }

        public static PlayerController Create(RunController run, Vector2 position)
        {
            var renderer = PixelArt.Create("Explorer", "player", position, 10, null, 1.1f);
            var player = renderer.gameObject.AddComponent<PlayerController>();
            player.run = run; player.sprite = renderer;
            player.MaxHealth = player.Health = run.Config.playerHealth;
            player.body = renderer.gameObject.AddComponent<Rigidbody2D>();
            player.body.gravityScale = 0;
            player.body.freezeRotation = true;
            player.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            renderer.gameObject.AddComponent<CircleCollider2D>().radius = .3f;
            player.gun = PixelArt.Create("Gun", "gun", position, 12, player.transform, .8f).transform;
            player.pressureRing = PixelArt.Circle("PressureRadius", player.transform,
                new Color(.36f, .89f, .86f, .16f), .025f);
            PixelArt.SetRadius(player.pressureRing, run.Config.pressureRadius / 1.1f);
            return player;
        }

        private void Update()
        {
            if (run.State != RunState.Playing) { movement = Vector2.zero; return; }
            movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            AimPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            UpdateAim(AimPoint, false);
            sprite.color = Time.time < invulnerableUntil && Mathf.Sin(Time.time * 35) > 0 ? PixelArt.Red : Color.white;
            shotTimer -= Time.deltaTime;
            if (Input.GetMouseButton(0)) TryFire(AimPoint);
            if (Input.GetKeyDown(KeyCode.Q) && DecoyCooldownRemaining <= 0) ThrowDecoy();
        }

        private void FixedUpdate()
        {
            run.Visibility.UpdateWalking(body.position, Time.fixedDeltaTime);
            body.velocity = run.State == RunState.Playing ? movement * run.Config.moveSpeed * MoveScale : Vector2.zero;
        }

        private void UpdateAim(Vector2 manualPoint, bool forceManualAim)
        {
            AimTarget = forceManualAim ? null : FindVisibleTarget();
            var target = AimTarget ? (Vector2)AimTarget.transform.position : manualPoint;
            var direction = target - (Vector2)transform.position;
            if (direction.sqrMagnitude > .01f) aim = direction.normalized;
            gun.position = (Vector2)transform.position + aim * .55f;
            gun.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            sprite.flipX = aim.x < 0;
        }

        private EnemyController FindVisibleTarget()
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
                nearest = enemy; closest = distance;
            }
            return nearest;
        }

        // Skills may explicitly opt out; normal bullets fix their direction when fired, without homing.
        public bool TryFire(Vector2 manualPoint, bool forceManualAim = false)
        {
            if (run.State != RunState.Playing || shotTimer > 0) return false;
            UpdateAim(manualPoint, forceManualAim);
            shotTimer = Mathf.Max(.1f, run.Config.shotInterval * ShotIntervalScale);
            run.RefreshPressure();
            Projectile.Create(run, (Vector2)transform.position + aim * .65f, aim,
                run.Config.bulletSpeed, run.Config.bulletDamage * DamageScale * run.DamageMultiplier, false);
            run.Noise.Emit(transform.position, NoiseRadius, NoiseKind.Gunshot);
            run.Noise.PlayShot();
            return true;
        }

        private void ThrowDecoy()
        {
            var delta = Vector2.ClampMagnitude(AimPoint - (Vector2)transform.position, run.Config.decoyThrowRange);
            var point = run.Map.NearestFloor((Vector2)transform.position + delta);
            // Do not throw through walls, even though the resulting sound ignores walls.
            if (!run.Map.HasLineOfSight(transform.position, point))
            { run.Hud.Notify("诱饵投掷路径被墙壁挡住", 2); return; }
            BellDecoy.Create(run, point);
            decoyReadyAt = Time.time + Mathf.Max(5, run.Config.decoyCooldown * DecoyCooldownScale);
        }

        public void TakeDamage(float amount)
        {
            if (run.State != RunState.Playing || Time.time < invulnerableUntil) return;
            Health = Mathf.Max(0, Health - amount);
            invulnerableUntil = Time.time + run.Config.playerInvulnerability;
            if (Health <= 0) run.SetState(RunState.Lost);
        }

        public void Heal(float amount) => Health = Mathf.Min(MaxHealth, Health + amount);
    }
}

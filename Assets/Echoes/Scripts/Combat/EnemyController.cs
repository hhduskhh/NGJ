using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public enum EnemyState { Sleeping, Investigating, Chasing, Alert, Dead }

    public sealed class EnemyController : MonoBehaviour
    {
        private RunController run;
        private Rigidbody2D body;
        private SpriteRenderer sprite, marker, healthFill;
        private Vector2 destination, lastSeen;
        private List<Vector2> path = new List<Vector2>();
        private int pathIndex;
        private float repath, lostSight, contactReady, wanderTimer, attackTimer, warningTimer;
        private LineRenderer warning;
        private CharacterArt art;
        private SpriteRenderer muzzleWarning;
        public bool IsGuard { get; private set; }
        public EnemyState State { get; private set; } = EnemyState.Sleeping;
        public bool IsBoss { get; private set; }
        public bool Alive => State != EnemyState.Dead;
        public bool Awake => State != EnemyState.Sleeping && Alive;
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsWindingUp => warningTimer > 0;
        public bool IsContactTelegraph => !IsBoss && !IsGuard && State == EnemyState.Chasing &&
            Vector2.Distance(transform.position, run.Player.transform.position) > .8f &&
            Vector2.Distance(transform.position, run.Player.transform.position) < 1.4f;
        public Vector2 InvestigationTarget => destination;
        public Vector2 LastSeenPosition => lastSeen;

        public static EnemyController Create(RunController run, Vector2 position, bool boss)
        {
            var renderer = PixelArt.Create(boss ? "Gatekeeper" : "Sleeper", boss ? "boss" : "enemy",
                position, 9);
            var enemy = renderer.gameObject.AddComponent<EnemyController>();
            enemy.run = run; enemy.sprite = renderer; enemy.IsBoss = boss;
            enemy.MaxHealth = enemy.Health = boss ?
                run.Config.bossHealth * run.BossHealthScale :
                run.Config.enemyHealth * run.EnemyHealthScale;
            enemy.destination = enemy.lastSeen = position;
            enemy.body = renderer.gameObject.AddComponent<Rigidbody2D>();
            enemy.body.gravityScale = 0; enemy.body.freezeRotation = true;
            enemy.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var hitbox = renderer.gameObject.AddComponent<CircleCollider2D>();
            hitbox.radius = boss ? .64f : .32f;
            hitbox.offset = Vector2.zero;
            enemy.art = renderer.gameObject.AddComponent<CharacterArt>();
            enemy.IsGuard = !boss && run.Enemies.Count % 2 == 1;
            enemy.art.Initialize(boss ? "boss" : enemy.IsGuard ? "guard" : "walker");
            if (enemy.IsGuard)
            {
                enemy.muzzleWarning = PixelArt.Create("GuardMuzzleWarning", "guardMuzzle",
                    position, DungeonVisibility.DangerOrder, enemy.transform);
                enemy.muzzleWarning.color = PixelArt.Gold;
                enemy.muzzleWarning.enabled = false;
            }
            enemy.marker = PixelArt.Create("StateMarker", "sleepMarker", position + Vector2.up * (boss ? 3.7f : 1.85f),
                35, enemy.transform);
            enemy.healthFill = PixelArt.Create("Health", "solid", position + Vector2.down * .4f,
                35, enemy.transform);
            enemy.healthFill.transform.localScale = new Vector3(.8f, .06f, 1);
            enemy.healthFill.color = PixelArt.Red;
            enemy.repath = (float)run.Random.NextDouble() * .5f;
            enemy.attackTimer = boss ? run.Config.bossAttackInterval : 0;
            enemy.RefreshVisual();
            return enemy;
        }

        public void Hear(Vector2 source)
        {
            // Gatekeeper is a deliberate opt-in challenge, activated at its altar.
            if (!Alive || IsBoss || (State == EnemyState.Chasing && CanDetectPlayer())) return;
            State = EnemyState.Investigating;
            destination = run.Map.NearestFloor(source);
            repath = 0; RefreshVisual();
        }

        public void ActivateBoss()
        { State = EnemyState.Chasing; lastSeen = run.Player.transform.position; RefreshVisual(); }

        private bool CanDetectPlayer()
        {
            float radius = run.Config.WalkingDetectionRadius;
            if (IsGuard && State == EnemyState.Chasing) radius = Mathf.Max(radius, run.Config.guardRange);
            return Vector2.Distance(transform.position, run.Player.transform.position) <= radius &&
                run.Map.HasLineOfSight(transform.position, run.Player.transform.position);
        }

        private void Update()
        {
            if (run.State != RunState.Playing || !Alive || !Awake) { body.velocity = Vector2.zero; return; }
            float dt = Time.deltaTime;
            Vector2 player = run.Player.transform.position;
            bool seesPlayer = CanDetectPlayer();
            if (seesPlayer)
            { State = EnemyState.Chasing; lastSeen = player; lostSight = 0; }
            else if (State == EnemyState.Chasing)
            {
                lostSight += dt;
                if (lostSight > 2.5f) { State = EnemyState.Alert; destination = lastSeen; wanderTimer = 2; }
            }
            if (State == EnemyState.Chasing) destination = lastSeen;
            if (IsGuard && UpdateGuardAttack(dt, seesPlayer, player))
            {
                body.velocity = Vector2.zero;
                repath = 0;
                RefreshVisual();
                return;
            }
            if (State == EnemyState.Investigating && Vector2.Distance(transform.position, destination) < .5f)
            { State = EnemyState.Alert; lastSeen = destination; wanderTimer = 1; }
            if (State == EnemyState.Alert)
            {
                wanderTimer -= dt;
                if (wanderTimer <= 0)
                {
                    wanderTimer = 2.5f;
                    var offset = new Vector2((float)run.Random.NextDouble() * 3 - 1.5f,
                        (float)run.Random.NextDouble() * 3 - 1.5f);
                    destination = run.Map.NearestFloor(lastSeen + offset);
                }
            }
            repath -= dt;
            if (repath <= 0)
            { repath = .55f; path = run.Map.FindPath(transform.position, destination); pathIndex = 0; }
            Move();
            if (!IsGuard && Vector2.Distance(transform.position, player) < (IsBoss ? 1.15f : .8f) && Time.time >= contactReady)
            {
                contactReady = Time.time + run.Config.enemyAttackInterval;
                art.Attack();
                run.Player.TakeDamage((IsBoss ? run.Config.bossDamage : run.Config.enemyDamage) *
                    run.EnemyDamageScale);
            }
            if (IsBoss) UpdateBossAttack(dt);
            RefreshVisual();
        }

        private void Move()
        {
            while (pathIndex < path.Count && Vector2.Distance(transform.position, path[pathIndex]) < .22f) pathIndex++;
            Vector2 direction = pathIndex < path.Count ? (path[pathIndex] - (Vector2)transform.position).normalized : Vector2.zero;
            float speed = IsBoss ? run.Config.bossSpeed : IsGuard ? run.Config.guardSpeed : run.Config.walkerSpeed;
            body.velocity = direction * speed;
            if (direction.x != 0) sprite.flipX = direction.x < 0;
        }

        private bool UpdateGuardAttack(float dt, bool seesPlayer, Vector2 player)
        {
            attackTimer -= dt;
            Vector2 origin = transform.position;
            if (!seesPlayer || State != EnemyState.Chasing || !run.Visibility.InCamera(origin) ||
                Vector2.Distance(player, origin) > run.Config.guardRange)
            {
                warningTimer = 0;
                return false;
            }
            Vector2 direction = (player - origin).normalized;
            sprite.flipX = direction.x < 0;
            if (warningTimer > 0)
            {
                warningTimer -= dt;
                if (warningTimer <= 0)
                {
                    Projectile.Create(run, origin, direction, run.Config.guardBulletSpeed,
                        run.Config.enemyDamage * run.EnemyDamageScale, true);
                    attackTimer = run.Config.guardShotInterval - run.Config.guardWindup;
                    art.Attack();
                }
            }
            else if (attackTimer <= 0) warningTimer = run.Config.guardWindup;
            return true;
        }

        private void LateUpdate()
        {
            if (!muzzleWarning) return;
            muzzleWarning.enabled = Alive && IsWindingUp && run.Visibility.CanSeeDanger(transform.position);
            if (!muzzleWarning.enabled) return;
            var offset = ((Vector2)(run.Player.transform.position - transform.position)).normalized * .25f;
            muzzleWarning.transform.localPosition = offset;
            muzzleWarning.enabled = run.Visibility.CanSeeDanger(muzzleWarning.transform.position);
            float progress = 1 - warningTimer / run.Config.guardWindup;
            muzzleWarning.transform.localScale = Vector3.one * Mathf.Lerp(.55f, 1f, progress);
        }

        private void UpdateBossAttack(float dt)
        {
            if (warningTimer > 0)
            {
                warningTimer -= dt;
                if (warningTimer <= 0)
                {
                    Destroy(warning.gameObject);
                    for (int i = 0; i < run.Config.bossBulletCount; i++)
                    {
                        float angle = i * Mathf.PI * 2 / run.Config.bossBulletCount;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Projectile.Create(run, (Vector2)transform.position + direction * 1.1f, direction,
                            run.Config.bossBulletSpeed, run.Config.bossDamage * run.EnemyDamageScale, true);
                    }
                    attackTimer = run.Config.bossAttackInterval;
                    art.Attack();
                }
                return;
            }
            attackTimer -= dt;
            if (attackTimer <= 0)
            {
                warningTimer = run.Config.bossWarningDuration;
                warning = PixelArt.Circle("BossAttackTelegraph", transform, PixelArt.Red, .08f);
                PixelArt.SetRadius(warning, 1);
                run.Hud.Notify("守关者蓄力 · 红色环形弹幕即将发射", run.Config.bossWarningDuration);
            }
        }

        public void TakeDamage(float amount, Vector2? attackOrigin = null)
        {
            if (!Alive || (IsBoss && !run.BossActivated)) return;
            if (CanDetectPlayer())
            { State = EnemyState.Chasing; lastSeen = run.Player.transform.position; lostSight = 0; }
            else if (!IsBoss) Hear(attackOrigin ?? (Vector2)run.Player.transform.position);
            Health = Mathf.Max(0, Health - amount);
            art.Hurt();
            if (Health > 0) { RefreshVisual(); return; }
            State = EnemyState.Dead;
            art.BeginCorpseFade(run.Config);
            body.velocity = Vector2.zero;
            GetComponent<Collider2D>().enabled = false;
            marker.enabled = healthFill.enabled = false;
            if (warning) Destroy(warning.gameObject);
            run.EnemyKilled(this);
        }

        private void RefreshVisual()
        {
            string kind = State == EnemyState.Sleeping ? "sleepMarker" :
                State == EnemyState.Chasing || IsWindingUp ? "alertMarker" : "investigateMarker";
            marker.GetComponent<ArtBinding>().Kind = kind;
            marker.sprite = GameArt.Get(kind);
            marker.color = Color.white;
            healthFill.transform.localScale = new Vector3(.8f * Health / MaxHealth, .06f, 1);
            healthFill.enabled = Health < MaxHealth;
        }
    }
}

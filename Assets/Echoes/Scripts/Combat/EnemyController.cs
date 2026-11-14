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
        private Vector2 lastStepPosition;
        private float distanceSinceStep;
        private List<Vector2> path = new List<Vector2>();
        private int pathIndex;
        private float repath, lostSight, contactReady, wanderTimer, attackTimer, warningTimer;
        private LineRenderer warning;
        private float dashCooldown, dashRemaining;
        private Vector2 dashDirection;
        private bool dashStep, dashHitPlayer;
        private CharacterArt art;
        private SpriteRenderer muzzleWarning;
        private PolygonCollider2D damageShape;
        private readonly List<Vector2> outline = new List<Vector2>();
        private Sprite lastDamageSprite;
        private bool lastFlip, trackingHit, holdingPosition;
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
            bool isGuard = !boss && run.Enemies.Count % 2 == 1;
            string species = boss ? "boss" : isGuard ? "guard" : "walker";
            var renderer = PixelArt.Create(boss ? "Gatekeeper" : "Sleeper", species,
                position, 9);
            var enemy = renderer.gameObject.AddComponent<EnemyController>();
            enemy.run = run; enemy.sprite = renderer; enemy.IsBoss = boss;
            enemy.MaxHealth = enemy.Health = boss ?
                run.Config.bossHealth * run.BossHealthScale :
                run.Config.enemyHealth * run.EnemyHealthScale;
            enemy.destination = enemy.lastSeen = position;
            enemy.lastStepPosition = position;
            enemy.body = renderer.gameObject.AddComponent<Rigidbody2D>();
            enemy.body.gravityScale = 0; enemy.body.freezeRotation = true;
            enemy.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var hitbox = renderer.gameObject.AddComponent<CircleCollider2D>();
            hitbox.radius = boss ? .64f : .32f;
            hitbox.offset = Vector2.zero;
            enemy.damageShape = renderer.gameObject.AddComponent<PolygonCollider2D>();
            enemy.damageShape.isTrigger = true;
            enemy.art = renderer.gameObject.AddComponent<CharacterArt>();
            enemy.IsGuard = isGuard;
            enemy.art.Initialize(species);
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
            if (boss && run.Floor >= 2)
            {
                enemy.dashCooldown = run.Config.bossDashInterval;
                enemy.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            enemy.RefreshVisual();
            return enemy;
        }

        internal void UpdateDamageShape(Sprite frame, bool flip)
        {
            if (!Alive || (frame == lastDamageSprite && flip == lastFlip)) return;
            lastDamageSprite = frame;
            lastFlip = flip;
            damageShape.pathCount = frame.GetPhysicsShapeCount();
            for (int i = 0; i < damageShape.pathCount; i++)
            {
                frame.GetPhysicsShape(i, outline);
                if (flip)
                    for (int p = 0; p < outline.Count; p++)
                        outline[p] = new Vector2(-outline[p].x, outline[p].y);
                damageShape.SetPath(i, outline);
            }
        }

        public void Hear(Vector2 source)
        {
            // Gatekeeper is a deliberate opt-in challenge, activated at its altar.
            if (!Alive || IsBoss || trackingHit || (State == EnemyState.Chasing && CanDetectPlayer())) return;
            if (State == EnemyState.Sleeping) run.Noise.PlayWake(transform.position);
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
            if (seesPlayer) trackingHit = false;
            if (seesPlayer || trackingHit)
            { State = EnemyState.Chasing; lastSeen = player; lostSight = 0; }
            else if (State == EnemyState.Chasing)
            {
                lostSight += dt;
                if (lostSight > 2.5f) { State = EnemyState.Alert; destination = lastSeen; wanderTimer = 2; }
            }
            if (State == EnemyState.Chasing) destination = lastSeen;
            holdingPosition = IsGuard && UpdateGuardAttack(dt, seesPlayer, player);
            if (holdingPosition)
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
            { repath = .55f; path = run.Map.FindPath(body.position, destination); pathIndex = 0; }
            if (!IsGuard && (!IsBoss || run.Floor == 1) &&
                Vector2.Distance(transform.position, player) < (IsBoss ? 1.15f : .8f) && Time.time >= contactReady)
            {
                contactReady = Time.time + run.Config.enemyAttackInterval;
                art.Attack();
                run.Player.TakeDamage((IsBoss ? run.Config.bossDamage : run.Config.enemyDamage) *
                    run.EnemyDamageScale);
            }
            if (IsBoss)
            {
                float attackTime = dt * (run.Floor == 3 ? run.Config.finalBossAttackRate : 1f);
                if (run.Floor != 2) UpdateBossBarrage(attackTime);
                if (run.Floor >= 2) UpdateBossDash(attackTime, player);
            }
            RefreshVisual();
        }

        private void FixedUpdate()
        {
            // Track teleports while sleeping too; they are not walking steps.
            Vector2 position = body.position;
            float distance = Vector2.Distance(position, lastStepPosition);
            lastStepPosition = position;
            dashStep = false;
            if (run.State != RunState.Playing || !Alive || !Awake || holdingPosition)
            { body.velocity = Vector2.zero; return; }
            if (dashRemaining > 0)
            {
                dashStep = true;
                float step = Mathf.Min(dashRemaining, Time.fixedDeltaTime);
                body.velocity = dashDirection * (run.Config.bossDashSpeed * step / Time.fixedDeltaTime);
                dashRemaining -= step;
                repath = 0;
                return;
            }
            Move();
            if (IsBoss) return;
            distanceSinceStep += distance;
            float stride = Mathf.Max(.1f, run.Config.enemyStepDistance);
            if (distanceSinceStep < stride) return;
            distanceSinceStep %= stride;
            run.Noise.PlayEnemyStep(position);
        }

        private void Move()
        {
            Vector2 position = body.position;
            while (pathIndex < path.Count && Vector2.Distance(position, path[pathIndex]) < .22f) pathIndex++;
            Vector2 delta = pathIndex < path.Count ? path[pathIndex] - position : Vector2.zero;
            Vector2 direction = delta.normalized;
            float speed = IsBoss ? run.Config.bossSpeed : IsGuard ? run.Config.guardSpeed : run.Config.walkerSpeed;
            // Advance at the physics cadence and stop at the waypoint, even
            // when a slow render frame contains several physics steps.
            body.velocity = direction * Mathf.Min(speed, delta.magnitude / Time.fixedDeltaTime);
            if (direction.x != 0) sprite.flipX = direction.x < 0;
        }

        private bool UpdateGuardAttack(float dt, bool seesPlayer, Vector2 player)
        {
            attackTimer -= dt;
            Vector2 origin = transform.position;
            if (!seesPlayer || State != EnemyState.Chasing || !run.Visibility.InCamera(origin) ||
                Vector2.Distance(player, origin) > run.Config.guardRange ||
                !run.Map.HasFiringLine(origin, player, run.Player.transform))
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
            else if (attackTimer <= 0)
            {
                warningTimer = run.Config.guardWindup;
                run.Noise.PlayGuardReady(origin);
            }
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

        private void UpdateBossDash(float dt, Vector2 player)
        {
            dashCooldown -= dt;
            if (dashCooldown > 0 || dashRemaining > 0) return;
            dashDirection = (player - body.position).normalized;
            dashRemaining = run.Config.bossDashDuration;
            dashCooldown = run.Config.bossDashInterval;
            dashHitPlayer = false;
            if (dashDirection.x != 0) sprite.flipX = dashDirection.x < 0;
            art.Attack();
        }

        private void OnCollisionEnter2D(Collision2D collision) => HitDuringDash(collision);
        private void OnCollisionStay2D(Collision2D collision) => HitDuringDash(collision);

        private void HitDuringDash(Collision2D collision)
        {
            if (!dashStep || dashHitPlayer || !Alive || run.State != RunState.Playing ||
                collision.gameObject != run.Player.gameObject) return;
            dashHitPlayer = true;
            run.Player.TakeDamage(run.Config.bossDamage * run.EnemyDamageScale);
        }

        private void UpdateBossBarrage(float dt)
        {
            if (warningTimer > 0)
            {
                warningTimer -= dt;
                if (warningTimer <= 0)
                {
                    Destroy(warning.gameObject);
                    run.Noise.StopBossReady();
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
                run.Noise.PlayBossReady(warningTimer / (run.Floor == 3 ? run.Config.finalBossAttackRate : 1f));
                warning = PixelArt.Circle("BossAttackTelegraph", transform, PixelArt.Red, .08f);
                PixelArt.SetRadius(warning, 1);
            }
        }

        public void TakeDamage(float amount, bool playImpact = true)
        {
            if (!Alive || (IsBoss && !run.BossActivated)) return;
            trackingHit = true;
            State = EnemyState.Chasing;
            lastSeen = run.Player.transform.position;
            lostSight = repath = 0;
            Health = Mathf.Max(0, Health - amount);
            if (amount > 0 && (playImpact || Health <= 0))
                run.Noise.QueueImpact(this, Health <= 0);
            art.Hurt();
            if (Health > 0) { RefreshVisual(); return; }
            State = EnemyState.Dead;
            art.BeginCorpseFade(run.Config);
            body.velocity = Vector2.zero;
            GetComponent<CircleCollider2D>().enabled = false;
            damageShape.enabled = false;
            marker.enabled = healthFill.enabled = false;
            if (warning)
            {
                run.Noise.StopBossReady();
                Destroy(warning.gameObject);
            }
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

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
        public EnemyState State { get; private set; } = EnemyState.Sleeping;
        public bool IsBoss { get; private set; }
        public bool Alive => State != EnemyState.Dead;
        public bool Awake => State != EnemyState.Sleeping && Alive;
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public Vector2 InvestigationTarget => destination;
        public Vector2 LastSeenPosition => lastSeen;

        public static EnemyController Create(RunController run, Vector2 position, bool boss)
        {
            var renderer = PixelArt.Create(boss ? "Gatekeeper" : "Sleeper", boss ? "boss" : "enemy",
                position, 9, null, boss ? 2 : 1);
            var enemy = renderer.gameObject.AddComponent<EnemyController>();
            enemy.run = run; enemy.sprite = renderer; enemy.IsBoss = boss;
            enemy.MaxHealth = enemy.Health = boss ? run.Config.bossHealth : run.Config.enemyHealth;
            enemy.destination = enemy.lastSeen = position;
            enemy.body = renderer.gameObject.AddComponent<Rigidbody2D>();
            enemy.body.gravityScale = 0; enemy.body.freezeRotation = true;
            enemy.body.interpolation = RigidbodyInterpolation2D.Interpolate;
            renderer.gameObject.AddComponent<CircleCollider2D>().radius = .32f;
            enemy.marker = PixelArt.Create("StateMarker", "solid", position + Vector2.up * (boss ? 1.4f : .75f),
                15, enemy.transform, .16f);
            enemy.healthFill = PixelArt.Create("Health", "solid", position + Vector2.down * (boss ? 1.3f : .6f),
                15, enemy.transform);
            enemy.healthFill.transform.localScale = new Vector3(.8f, .06f, 1);
            enemy.healthFill.color = PixelArt.Red;
            enemy.repath = (float)run.Random.NextDouble() * .5f;
            enemy.attackTimer = run.Config.bossAttackInterval;
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

        private bool CanDetectPlayer() => Vector2.Distance(transform.position, run.Player.transform.position) <=
            run.Config.WalkingDetectionRadius && run.Map.HasLineOfSight(transform.position, run.Player.transform.position);

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
            if (Vector2.Distance(transform.position, player) < (IsBoss ? 1.15f : .8f) && Time.time >= contactReady)
            {
                contactReady = Time.time + run.Config.enemyAttackInterval;
                run.Player.TakeDamage(IsBoss ? run.Config.bossDamage : run.Config.enemyDamage);
            }
            if (IsBoss) UpdateBossAttack(dt);
            RefreshVisual();
        }

        private void Move()
        {
            while (pathIndex < path.Count && Vector2.Distance(transform.position, path[pathIndex]) < .22f) pathIndex++;
            Vector2 direction = pathIndex < path.Count ? (path[pathIndex] - (Vector2)transform.position).normalized : Vector2.zero;
            body.velocity = direction * run.Config.enemySpeed * (IsBoss ? .75f : 1);
            if (direction.x != 0) sprite.flipX = direction.x < 0;
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
                            run.Config.bossBulletSpeed, run.Config.bossDamage, true);
                    }
                    attackTimer = run.Config.bossAttackInterval;
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
            if (Health > 0) { RefreshVisual(); return; }
            State = EnemyState.Dead;
            body.velocity = Vector2.zero;
            GetComponent<Collider2D>().enabled = false;
            sprite.color = new Color(.3f, .26f, .33f, .4f);
            transform.localScale *= .65f;
            marker.enabled = healthFill.enabled = false;
            if (warning) Destroy(warning.gameObject);
            run.EnemyKilled(this);
        }

        private void RefreshVisual()
        {
            marker.color = State == EnemyState.Sleeping ? new Color(.4f, .46f, .59f) :
                State == EnemyState.Chasing ? PixelArt.Red : PixelArt.Gold;
            marker.transform.localScale = State == EnemyState.Chasing ? new Vector3(.12f, .3f, 1) :
                State == EnemyState.Sleeping ? new Vector3(.24f, .08f, 1) : new Vector3(.2f, .2f, 1);
            healthFill.transform.localScale = new Vector3(.8f * Health / MaxHealth, .06f, 1);
            healthFill.enabled = Health < MaxHealth;
            sprite.color = State == EnemyState.Sleeping ? new Color(.5f, .55f, .66f) : Color.white;
        }
    }
}

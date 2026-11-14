using UnityEngine;
using System.Collections.Generic;

namespace Echoes
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float BaseRadius = .085f;
        private const float DefaultSizeScale = 1.2f;
        public const float DefaultRadius = BaseRadius * DefaultSizeScale;
        private float hitRadius;
        private RunController run;
        private Vector2 direction;
        private float speed, damage;
        private bool hostile;
        private SpriteRenderer hostileSprite;
        private int pierces;
        private HashSet<EnemyController> impactTargets;
        private DungeonVisibility.ShotTrail trail;
        private readonly HashSet<EnemyController> struck = new HashSet<EnemyController>();
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);

        public static void Create(RunController run, Vector2 position, Vector2 direction,
            float speed, float damage, bool hostile, int pierces = 0, HashSet<EnemyController> impactTargets = null,
            float sizeScale = DefaultSizeScale)
        {
            var renderer = PixelArt.Create(hostile ? "HostileProjectile" : "PlayerProjectile",
                hostile ? "hostileBullet" : "bullet", position, 30, scale: sizeScale);
            var projectile = renderer.gameObject.AddComponent<Projectile>();
            projectile.run = run; projectile.direction = direction.normalized;
            projectile.speed = speed; projectile.damage = damage; projectile.hostile = hostile;
            projectile.pierces = pierces;
            projectile.hitRadius = BaseRadius * sizeScale;
            projectile.impactTargets = impactTargets;
            projectile.trail = run.Visibility.BeginTrail(position, projectile.direction, speed);
            if (hostile)
            {
                projectile.hostileSprite = renderer;
                renderer.sortingOrder = DungeonVisibility.DangerOrder;
                renderer.enabled = run.Visibility.CanSeeDanger(position);
            }
        }

        private void LateUpdate()
        {
            if (hostile) hostileSprite.enabled = run.Visibility.CanSeeDanger(transform.position);
        }

        private void Update()
        {
            if (run.State != RunState.Playing) return;
            float distance = speed * Time.deltaTime;
            // Cast the entire movement segment, so fast shots cannot tunnel through walls/enemies.
            int count = Physics2D.CircleCast(transform.position, hitRadius, direction,
                new ContactFilter2D().NoFilter(), hits, distance);
            Collider2D nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var enemy = hit.collider.GetComponent<EnemyController>();
                var player = hit.collider.GetComponent<PlayerController>();
                if (enemy && (!enemy.Alive || struck.Contains(enemy))) continue;
                bool blocksShot = run.Map.BlocksProjectile(hit.collider);
                bool isTarget = hostile ? player != null : enemy != null;
                if (!isTarget && !blocksShot) continue;
                if (hit.distance > nearestDistance || (hit.distance == nearestDistance && !blocksShot)) continue;
                nearestDistance = hit.distance; nearest = hit.collider;
            }
            transform.position += (Vector3)(direction * (nearest ? nearestDistance : distance));
            trail.Advance(transform.position);
            if (nearest)
            {
                ArtEffect.Play(transform.position, true,
                    hostile ? PixelArt.Red : Color.white);
                if (hostile) nearest.GetComponent<PlayerController>()?.TakeDamage(damage);
                else
                {
                    var enemy = nearest.GetComponent<EnemyController>();
                    if (enemy)
                    {
                        struck.Add(enemy);
                        enemy.TakeDamage(damage, impactTargets == null || impactTargets.Add(enemy));
                        if (pierces-- > 0)
                        {
                            damage *= .75f;
                            return;
                        }
                    }
                }
                Destroy(gameObject); return;
            }
        }
    }
}

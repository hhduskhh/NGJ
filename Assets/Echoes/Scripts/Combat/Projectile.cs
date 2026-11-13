using UnityEngine;
using System.Collections.Generic;

namespace Echoes
{
    public sealed class Projectile : MonoBehaviour
    {
        private RunController run;
        private Vector2 direction;
        private Vector2 origin;
        private float speed, damage, life;
        private bool hostile;
        private int pierces;
        private float remainingRange;
        private readonly HashSet<EnemyController> struck = new HashSet<EnemyController>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        public static void Create(RunController run, Vector2 position, Vector2 direction,
            float speed, float damage, bool hostile, float range = float.MaxValue, int pierces = 0)
        {
            var renderer = PixelArt.Create(hostile ? "HostileProjectile" : "PlayerProjectile",
                hostile ? "hostileBullet" : "bullet", position, 30);
            var projectile = renderer.gameObject.AddComponent<Projectile>();
            projectile.run = run; projectile.direction = direction.normalized;
            projectile.origin = position;
            projectile.speed = speed; projectile.damage = damage; projectile.hostile = hostile;
            projectile.remainingRange = range; projectile.pierces = pierces;
            projectile.life = hostile ? 8 : 1.8f;
        }

        private void Update()
        {
            if (run.State != RunState.Playing) return;
            float distance = Mathf.Min(remainingRange, speed * Time.deltaTime);
            // Cast the entire movement segment, so fast shots cannot tunnel through walls/enemies.
            int count = Physics2D.CircleCastNonAlloc(transform.position, .085f, direction, hits, distance);
            Collider2D nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var enemy = hit.collider.GetComponent<EnemyController>();
                var player = hit.collider.GetComponent<PlayerController>();
                if (enemy && (!enemy.Alive || struck.Contains(enemy))) continue;
                if ((!hostile && player) || (hostile && enemy)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance; nearest = hit.collider;
            }
            if (nearest)
            {
                ArtEffect.Play((Vector2)transform.position + direction * nearestDistance, true,
                    hostile ? PixelArt.Red : Color.white);
                if (hostile) nearest.GetComponent<PlayerController>()?.TakeDamage(damage);
                else
                {
                    var enemy = nearest.GetComponent<EnemyController>();
                    if (enemy)
                    {
                        struck.Add(enemy); enemy.TakeDamage(damage, origin);
                        if (pierces-- > 0)
                        {
                            damage *= .75f;
                            transform.position += (Vector3)(direction * nearestDistance);
                            remainingRange -= nearestDistance;
                            return;
                        }
                    }
                }
                Destroy(gameObject); return;
            }
            transform.position += (Vector3)(direction * distance);
            remainingRange -= distance;
            life -= Time.deltaTime;
            if (life <= 0 || remainingRange <= 0) Destroy(gameObject);
        }
    }
}

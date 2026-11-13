using UnityEngine;

namespace Echoes
{
    public sealed class Projectile : MonoBehaviour
    {
        private RunController run;
        private Vector2 direction;
        private Vector2 origin;
        private float speed, damage, life;
        private bool hostile;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        public static void Create(RunController run, Vector2 position, Vector2 direction,
            float speed, float damage, bool hostile)
        {
            var renderer = PixelArt.Create(hostile ? "HostileProjectile" : "PlayerProjectile",
                "solid", position, 20, null, hostile ? .22f : .18f);
            renderer.color = hostile ? PixelArt.Red : PixelArt.Gold;
            var projectile = renderer.gameObject.AddComponent<Projectile>();
            projectile.run = run; projectile.direction = direction.normalized;
            projectile.origin = position;
            projectile.speed = speed; projectile.damage = damage; projectile.hostile = hostile;
            projectile.life = hostile ? 8 : 1.8f;
        }

        private void Update()
        {
            if (run.State != RunState.Playing) return;
            float distance = speed * Time.deltaTime;
            // Cast the entire movement segment, so fast shots cannot tunnel through walls/enemies.
            int count = Physics2D.CircleCastNonAlloc(transform.position, .085f, direction, hits, distance);
            Collider2D nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var enemy = hit.collider.GetComponent<EnemyController>();
                var player = hit.collider.GetComponent<PlayerController>();
                if ((!hostile && player) || (hostile && enemy)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance; nearest = hit.collider;
            }
            if (nearest)
            {
                if (hostile) nearest.GetComponent<PlayerController>()?.TakeDamage(damage);
                else nearest.GetComponent<EnemyController>()?.TakeDamage(damage, origin);
                Destroy(gameObject); return;
            }
            transform.position += (Vector3)(direction * distance);
            life -= Time.deltaTime;
            if (life <= 0) Destroy(gameObject);
        }
    }
}

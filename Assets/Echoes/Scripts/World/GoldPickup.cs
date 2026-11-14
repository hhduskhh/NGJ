using UnityEngine;

namespace Echoes
{
    public sealed class GoldPickup : MonoBehaviour
    {
        private RunController run;
        private SpriteRenderer visual;
        private int amount;
        private float pullSpeed;

        public static void Drop(RunController run, Vector2 position, int amount)
        {
            var visual = PixelArt.Create("Gold", "gold", position, GameArt.FootOrder(position.y) - 1);
            var pickup = visual.gameObject.AddComponent<GoldPickup>();
            pickup.run = run;
            pickup.amount = amount;
            pickup.visual = visual;
        }

        private void Update()
        {
            if (run.State != RunState.Playing || amount <= 0) return;
            Vector2 position = transform.position;
            Vector2 target = run.Player.transform.position;
            var config = run.Config;
            if (pullSpeed == 0 && (target - position).sqrMagnitude > config.goldPickupRadius * config.goldPickupRadius)
                return;
            if (!run.Map.HasClearInteractionPath(position, target))
            {
                pullSpeed = 0;
                return;
            }

            pullSpeed = Mathf.Max(config.goldPullSpeed, pullSpeed) + config.goldPullAcceleration * Time.deltaTime;
            var next = Vector2.MoveTowards(position, target, pullSpeed * Time.deltaTime);
            transform.position = next;
            visual.sortingOrder = GameArt.FootOrder(next.y) + 2;
            if ((target - next).sqrMagnitude > .01f) return;

            run.Shop.Credit(amount);
            run.Noise.PlayPickup();
            amount = 0;
            Destroy(gameObject);
        }
    }
}

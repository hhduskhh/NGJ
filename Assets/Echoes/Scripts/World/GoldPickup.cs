using UnityEngine;

namespace Echoes
{
    public sealed class GoldPickup : MonoBehaviour
    {
        private RunController run;
        private int amount;
        public static void Drop(RunController run, Vector2 position, int amount)
        {
            var visual = PixelArt.Create("Gold", "gold", position, GameArt.FootOrder(position.y) - 1);
            var pickup = visual.gameObject.AddComponent<GoldPickup>();
            pickup.run = run; pickup.amount = amount;
        }
        private void Update()
        {
            if (run.State != RunState.Playing || amount <= 0) return;
            if (Vector2.Distance(run.Player.transform.position, transform.position) > run.Config.goldPickupRadius ||
                !run.Map.HasClearInteractionPath(run.Player.transform.position, transform.position)) return;
            run.Shop.Credit(amount); amount = 0; Destroy(gameObject);
        }
    }
}

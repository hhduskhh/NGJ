using UnityEngine;

namespace Echoes
{
    public sealed class BellDecoy : MonoBehaviour
    {
        private RunController run;
        private float remaining, pulse;
        private Vector2 launch, landing;
        private float flightProgress = 1;
        private SpriteRenderer visual;

        public static BellDecoy Create(RunController run, Vector2 position)
        {
            var visual = PixelArt.Create("BellDecoy", "bell", position, GameArt.FootOrder(position.y) + 1);
            var bell = visual.gameObject.AddComponent<BellDecoy>();
            bell.visual = visual;
            bell.run = run;
            bell.remaining = run.Config.decoyDuration + run.Player.Passives.DecoyDuration;
            return bell;
        }

        public static void Throw(RunController run, Vector2 origin, Vector2 target)
        {
            var bell = Create(run, origin);
            bell.launch = origin;
            bell.landing = target;
            bell.flightProgress = 0;
            bell.UpdateFlightPose();
        }

        private void UpdateFlightPose()
        {
            float t = flightProgress;
            var ground = Vector2.Lerp(launch, landing, t);
            float height = .55f * (1 - t) + run.Config.decoyArcHeight * 4 * t * (1 - t);
            transform.position = ground + Vector2.up * height;
            float spin = landing.x >= launch.x ? -360 : 360;
            transform.rotation = Quaternion.Euler(0, 0, spin * t);
            visual.sortingOrder = GameArt.FootOrder(ground.y) + (t < 1 ? 3 : 1);
        }

        private void Update()
        {
            if (run.State != RunState.Playing) return;
            if (flightProgress < 1)
            {
                flightProgress = Mathf.Min(1, flightProgress + Time.deltaTime / Mathf.Max(.05f, run.Config.decoyFlightSeconds));
                UpdateFlightPose();
                if (flightProgress < 1) return;
            }
            remaining -= Time.deltaTime; pulse -= Time.deltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            if (pulse <= 0)
            {
                pulse = .8f;
                run.Noise.Emit(transform.position, run.Config.decoyRadius + run.Player.Passives.DecoyRadius, NoiseKind.Decoy);
                ArtEffect.Play((Vector2)transform.position + Vector2.up * .2f, false, PixelArt.Cyan);
            }
        }
    }
}

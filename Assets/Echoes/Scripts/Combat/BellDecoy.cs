using UnityEngine;

namespace Echoes
{
    public sealed class BellDecoy : MonoBehaviour
    {
        private RunController run;
        private float remaining, pulse;
        public static void Create(RunController run, Vector2 position)
        {
            var bell = PixelArt.Create("BellDecoy", "bell", position, 8).gameObject.AddComponent<BellDecoy>();
            bell.run = run; bell.remaining = run.Config.decoyDuration;
        }
        private void Update()
        {
            if (run.State != RunState.Playing) return;
            remaining -= Time.deltaTime; pulse -= Time.deltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            if (pulse <= 0)
            { pulse = .8f; run.Noise.Emit(transform.position, run.Config.decoyRadius, NoiseKind.Decoy); }
        }
    }
}

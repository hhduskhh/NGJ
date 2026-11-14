using UnityEngine;

namespace Echoes
{
    public sealed class RoomTrap : MonoBehaviour
    {
        private RunController run;
        private DungeonMap map;
        private SpriteRenderer icon;
        private Sprite loweredSpikes, raisedSpikes;
        private LineRenderer radiusRing;
        private bool bomb, armed, struck;
        private float clock;

        public static void Create(RunController run, DungeonMap map, Vector2Int tile, bool bomb, Transform parent)
        {
            var icon = PixelArt.Create(bomb ? "FloorBomb" : "SpikePlate", bomb ? "floorBomb" : "spikePlate",
                tile, -860, parent);
            var trap = icon.gameObject.AddComponent<RoomTrap>();
            trap.icon = icon;
            trap.run = run;
            trap.map = map;
            trap.bomb = bomb;
            if (!bomb)
            {
                trap.loweredSpikes = icon.sprite;
                trap.raisedSpikes = GameArt.Get("raisedSpikes");
            }
            trap.clock = bomb ? 0 : Mathf.Repeat(tile.x + tile.y * .7f, run.Config.spikeRest);
            trap.radiusRing = PixelArt.Circle("DangerRadius", icon.transform, PixelArt.Gold, .06f);
            trap.radiusRing.sortingOrder = -859;
            PixelArt.SetRadius(trap.radiusRing, bomb ? run.Config.bombRadius : run.Config.spikeRadius);
            trap.radiusRing.enabled = false;
        }

        private void Update()
        {
            if (!run.Player || run.State != RunState.Playing || run.Map != map) return;
            Vector2 player = run.Player.transform.position;
            var config = run.Config;
            float radius = bomb ? config.bombRadius : config.spikeRadius;
            bool inRange = (player - (Vector2)transform.position).sqrMagnitude <= radius * radius
                && map.HasLineOfSight(transform.position, player);

            if (bomb)
            {
                if (!armed && !inRange) return;
                armed = true;
                clock += Time.deltaTime;
                Warn(clock);
                if (clock < config.bombFuse) return;
                if (inRange) run.Player.TakeDamage(config.bombDamage);
                ArtEffect.Play(transform.position, true, PixelArt.Red);
                Destroy(gameObject);
                return;
            }

            clock += Time.deltaTime;
            float rise = config.spikeRest + config.spikeWarning;
            if (clock >= rise + config.spikeActive)
            {
                clock = 0;
                struck = false;
            }
            bool raised = clock >= rise;
            icon.sprite = raised ? raisedSpikes : loweredSpikes;
            if (clock >= config.spikeRest && !raised) Warn(clock);
            else
            {
                radiusRing.enabled = raised;
                icon.color = Color.white;
                radiusRing.startColor = radiusRing.endColor = PixelArt.Red;
            }
            if (raised && inRange && !struck)
            {
                struck = true;
                run.Player.TakeDamage(config.spikeDamage);
            }
        }

        private void Warn(float time)
        {
            radiusRing.enabled = true;
            var color = Color.Lerp(PixelArt.Gold, PixelArt.Red, Mathf.PingPong(time * 4, 1));
            radiusRing.startColor = radiusRing.endColor = icon.color = color;
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    // Captures the actual game camera, art and HUD; no white probe surfaces or replacement maps.
    public sealed class GameplayVisionCaptureTests
    {
        private RunController run;

        private IEnumerator CaptureAt(Vector2 source, Vector2 aim, string file)
        {
            run.Visibility.ClearPulses();
            run.Player.PlaceAt(source);
            yield return new WaitForFixedUpdate();
            float elapsed = run.Elapsed;
            int frame = Time.frameCount;
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire(aim, true));
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .03f);
            Assert.Greater(run.Elapsed, elapsed, "The game must advance before capture");
            Assert.Greater(Time.frameCount, frame + 1);
            Assert.IsTrue(run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>().enabled);
            run.Visibility.RefreshMask();
            RuntimeSmokeTests.Capture("TestResults/GameplayVerification/" + file + ".png");
            Debug.Log("VISION_CAPTURE " + file + " source=" + source + " aim=" + aim);
        }

        [UnityTest]
        public IEnumerator DoorCornerAndSightThroughFurniture()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            Directory.CreateDirectory("TestResults/GameplayVerification");
            var door = DungeonTestGeometry.FindDoor(run);
            yield return CaptureAt(DungeonTestGeometry.World(door.Source),
                DungeonTestGeometry.World(door.Through), "01-doorway");
            Assert.IsTrue(run.Visibility.IsVisible(DungeonTestGeometry.World(door.Through)));
            Assert.IsFalse(run.Visibility.IsVisible(DungeonTestGeometry.World(door.Around)));

            var b = run.Map.Rooms[0].Bounds;
            Vector2 corner = new Vector2(b.x, b.y);
            Vector2 cornerSource = corner + Vector2.one * 3;
            Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(cornerSource)));
            yield return CaptureAt(cornerSource, corner, "02-wall-corner");
            Assert.IsFalse(run.Visibility.IsVisible(corner - Vector2.one));

            var obstacles = Object.FindObjectsOfType<BoxCollider2D>()
                .Where(box => box.name.StartsWith("Obstacle_"))
                .OrderBy(box => Vector2.Distance(box.bounds.center, run.Map.StartPosition));
            Vector2 obstacleSource = Vector2.zero, hidden = Vector2.zero;
            bool found = false;
            foreach (var box in obstacles)
            {
                foreach (var direction in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), Vector2.one })
                {
                    Vector2 center = box.bounds.center;
                    Vector2 offset = new Vector2(box.bounds.extents.x + 2, box.bounds.extents.y + 2);
                    Vector2 from = run.Map.NearestFloor(center + Vector2.Scale(direction, offset));
                    Vector2 to = run.Map.NearestFloor(center - Vector2.Scale(direction, offset));
                    float distance = Vector2.Distance(from, to);
                    if (distance > run.Player.NoiseRadius + run.Config.gradientVisionRange - .5f || distance < 3) continue;
                    if (!box.bounds.IntersectRay(new Ray(from, to - from)) || !run.Map.HasLineOfSight(from, to)) continue;
                    obstacleSource = from; hidden = to; found = true; break;
                }
                if (found) break;
            }
            Assert.IsTrue(found, "Find visible floor across furniture");
            yield return CaptureAt(obstacleSource, hidden, "03-through-furniture");
            Assert.IsTrue(run.Visibility.IsVisible(hidden), "Floor behind furniture stays visible");
            Assert.IsFalse(run.FeelDebugMode);
        }
    }
}

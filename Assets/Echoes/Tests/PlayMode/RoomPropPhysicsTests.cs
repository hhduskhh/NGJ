using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class RoomPropPhysicsTests
    {
        [UnityTest]
        public IEnumerator ScaledPropsMatchBlockedGridAndLeaveWalkableTilesClear()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            Physics2D.SyncTransforms();
            int checkedProps = 0, scaledProps = 0;
            foreach (var box in run.Map.GetComponentsInChildren<BoxCollider2D>())
            {
                if (!box.name.StartsWith("Obstacle_")) continue;
                checkedProps++;
                if (Mathf.Abs(box.transform.lossyScale.x - 1) > .01f) scaledProps++;
                var bounds = box.bounds;
                Assert.That(bounds.size.x, Is.EqualTo(Mathf.Round(bounds.size.x)).Within(.001f));
                Assert.That(bounds.size.y, Is.EqualTo(Mathf.Round(bounds.size.y)).Within(.001f));
                for (int x = Mathf.CeilToInt(bounds.min.x); x <= Mathf.FloorToInt(bounds.max.x); x++)
                    for (int y = Mathf.CeilToInt(bounds.min.y); y <= Mathf.FloorToInt(bounds.max.y); y++)
                    {
                        var cell = new Vector2Int(x, y);
                        Assert.IsTrue(run.Map.IsFloorTile(cell), box.name + " collider extends off the floor");
                        Assert.IsFalse(run.Map.IsWalkable(cell), box.name + " collider blocks a walkable grid tile");
                    }
                // The physical edge must coincide with the grid boundary, so
                // a walkable neighbour remains physically outside this prop.
                foreach (var side in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    var point = (Vector2)bounds.center + Vector2.Scale(side,
                        (Vector2)bounds.extents + Vector2.one * .05f);
                    Assert.IsFalse(box.OverlapPoint(point), "Outside footprint must remain outside physics");
                }
            }
            Assert.Greater(checkedProps, 20);
            // Floor 1 uses native P1 sprites; floor 2 still exercises the P0
            // 1.5x-5x scale path that originally multiplied world collisions.
            run.Map.Generate(run, 1);
            Physics2D.SyncTransforms();
            foreach (var box in run.Map.GetComponentsInChildren<BoxCollider2D>())
            {
                if (!box.name.StartsWith("Obstacle_") || !box.gameObject.activeInHierarchy) continue;
                if (Mathf.Abs(box.transform.lossyScale.x - 1) <= .01f) continue;
                scaledProps++;
                var bounds = box.bounds;
                for (int x = Mathf.CeilToInt(bounds.min.x); x <= Mathf.FloorToInt(bounds.max.x); x++)
                    for (int y = Mathf.CeilToInt(bounds.min.y); y <= Mathf.FloorToInt(bounds.max.y); y++)
                        Assert.IsFalse(run.Map.IsWalkable(new Vector2Int(x, y)), "Scaled collider blocks a walkable tile");
            }
            Assert.Greater(scaledProps, 0, "Exercise remaining P0 themes too");
        }
    }
}

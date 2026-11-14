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
        public IEnumerator NativePropsOnAllFloorsMatchBlockedGridAndLeaveWalkableTilesClear()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            Physics2D.SyncTransforms();
            int checkedProps = 0;
            var measured = new System.Collections.Generic.HashSet<string>();
            for (int floor = 0; floor < FloorTheme.Count; floor++)
            {
                if (floor > 0) run.Map.Generate(run, floor);
                yield return null;
                Physics2D.SyncTransforms();
                foreach (var box in run.Map.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (!box.name.StartsWith("Obstacle_")) continue;
                    checkedProps++;
                    Assert.AreEqual(Vector3.one, box.transform.lossyScale, "P1 props retain native size");
                    var bounds = box.bounds;
                    string kind = box.GetComponent<ArtBinding>().Kind;
                    if (measured.Add(kind))
                    {
                        var entry = RoomArt.Definition(kind == StartStall.Kind ? "room_stall" : kind);
                        var sprite = box.GetComponent<SpriteRenderer>().sprite;
                        var image = new Texture2D(2, 2);
                        image.LoadImage(System.IO.File.ReadAllBytes("Assets/Echoes/Resources/" + entry.Path + ".png"));
                        Vector2 localMin = box.offset - box.size * .5f;
                        Vector2 localMax = box.offset + box.size * .5f;
                        Vector2Int min = Vector2Int.RoundToInt(sprite.pivot + localMin * sprite.pixelsPerUnit);
                        Vector2Int max = Vector2Int.RoundToInt(sprite.pivot + localMax * sprite.pixelsPerUnit);
                        int solid = 0;
                        for (int py = min.y; py < max.y; py++)
                            for (int px = min.x; px < max.x; px++)
                                if (image.GetPixel(px, py).a >= .5f) solid++;
                        Object.Destroy(image);
                        Assert.Greater((float)solid / ((max.x - min.x) * (max.y - min.y)), .8f,
                            kind + " must sit on the visible base");
                        if (kind == "room_column")
                        {
                            // The previous 2-unit box blocked both paths along this column.
                            Vector2 beside = (Vector2)box.transform.position + new Vector2(.85f, -1);
                            foreach (float radius in new[] { .33f, .085f })
                                foreach (var hit in Physics2D.CircleCastAll(beside, radius, Vector2.up, 3))
                                    Assert.AreNotSame(box, hit.collider, "Player and shot fit beside the visible column");
                            Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt((Vector2)bounds.center + Vector2.right * 1.5f)));
                        }
                    }
                    for (int x = Mathf.CeilToInt(bounds.min.x); x <= Mathf.FloorToInt(bounds.max.x); x++)
                        for (int y = Mathf.CeilToInt(bounds.min.y); y <= Mathf.FloorToInt(bounds.max.y); y++)
                        {
                            var cell = new Vector2Int(x, y);
                            Assert.IsTrue(run.Map.IsFloorTile(cell), box.name + " collider extends off the floor");
                            Assert.IsFalse(run.Map.IsWalkable(cell), box.name + " collider blocks a walkable grid tile");
                        }
                    // Probe just outside each physical edge.
                    foreach (var side in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                    {
                        var point = (Vector2)bounds.center + Vector2.Scale(side,
                            (Vector2)bounds.extents + Vector2.one * .05f);
                        Assert.IsFalse(box.OverlapPoint(point), "Outside footprint must remain outside physics");
                    }
                }
            }
            Assert.Greater(checkedProps, 20);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class ObstacleVisibilityTests
    {
        private RunController run;

        private IEnumerator LoadFloor()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            run.Player.enabled = false;
            var park = run.Map.Rooms[run.Map.Rooms.Count - 1].Center;
            foreach (var enemy in run.Enemies)
            {
                enemy.enabled = false;
                enemy.GetComponent<Rigidbody2D>().position = park;
            }
            Physics2D.SyncTransforms();
        }

        private void PlacePlayer(Vector2 position)
        {
            var body = run.Player.GetComponent<Rigidbody2D>();
            body.position = position;
            run.Player.transform.position = position;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator StationaryGradientStopsAtTheDoorJamb()
        {
            yield return LoadFloor();
            var geometry = DungeonTestGeometry.FindDoor(run);
            PlacePlayer(DungeonTestGeometry.World(geometry.Source));
            float gradient = run.Config.gradientVisionRange;
            run.Config.gradientVisionRange = 10;
            try
            {
                Assert.Greater(run.Visibility.VisibilityAt(DungeonTestGeometry.World(geometry.Wall)), 0,
                    "The facing wall remains visible");
                Assert.Greater(run.Visibility.VisibilityAt(DungeonTestGeometry.World(geometry.Through)), 0,
                    "The open doorway remains visible");
                Assert.AreEqual(0, run.Visibility.VisibilityAt(DungeonTestGeometry.World(geometry.Around)),
                    "The gradient cannot see around the jamb");
                Assert.AreEqual(0, run.Visibility.Pulses.Count);
            }
            finally { run.Config.gradientVisionRange = gradient; }
        }

        [UnityTest]
        public IEnumerator PulsesRespectWallsAndDoorsAndMatchRenderedDarkness()
        {
            yield return LoadFloor();
            var geometry = DungeonTestGeometry.FindDoor(run);
            PlacePlayer(DungeonTestGeometry.World(geometry.Source));
            run.Visibility.Reveal(DungeonTestGeometry.World(geometry.Source), 10);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .03f);
            Assert.IsTrue(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Through)));
            Assert.IsTrue(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Wall)));
            Assert.IsFalse(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Around)));
            run.Visibility.RefreshMask();

            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var probes = new[]
            {
                DungeonTestGeometry.World(geometry.Through),
                DungeonTestGeometry.World(geometry.Around),
                DungeonTestGeometry.World(geometry.Wall)
            };
            var markers = new GameObject[probes.Length];
            for (int i = 0; i < probes.Length; i++)
            {
                markers[i] = new GameObject("VisibilityProbe");
                markers[i].transform.position = probes[i];
                markers[i].transform.localScale = Vector3.one * .25f;
                var renderer = markers[i].AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = PixelArt.LineMaterial;
                renderer.sortingOrder = 39;
            }
            var camera = new GameObject("VisibilityTestCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(probes[0].x, probes[0].y, -10);
            camera.orthographic = true;
            camera.orthographicSize = 8;
            var target = new RenderTexture(600, 400, 24);
            var image = new Texture2D(600, 400, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 600, 400), 0, 0);
                image.Apply();
                for (int i = 0; i < probes.Length; i++)
                {
                    Vector3 pixel = camera.WorldToScreenPoint(probes[i]);
                    float brightness = image.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y)).maxColorComponent;
                    if (run.Visibility.IsVisible(probes[i])) Assert.Greater(brightness, .9f, "Visible probe " + probes[i]);
                    else Assert.Less(brightness, .01f, "Hidden probe " + probes[i]);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                Object.Destroy(camera.gameObject);
                Object.Destroy(target); Object.Destroy(image); Object.Destroy(sprite);
                foreach (var marker in markers) Object.Destroy(marker);
            }
        }

        [UnityTest]
        public IEnumerator FacingWallTilesHaveNoTriangularDarkGaps()
        {
            yield return LoadFloor();
            var room = run.Map.Rooms[0];
            Vector2 source = room.Center;
            PlacePlayer(source);
            run.Visibility.Reveal(source, 30);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            run.Visibility.RefreshMask();

            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var backing = new GameObject("WallSurfaceProbe").AddComponent<SpriteRenderer>();
            backing.sprite = sprite;
            backing.sharedMaterial = PixelArt.LineMaterial;
            backing.sortingOrder = 39;
            backing.transform.position = source;
            backing.transform.localScale = new Vector3(room.Bounds.width + 20, room.Bounds.height + 20, 1);
            var camera = new GameObject("WallSurfaceTestCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(source.x, source.y, -10);
            camera.orthographic = true;
            camera.orthographicSize = 28;
            var target = new RenderTexture(800, 600, 24);
            var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                image.Apply();
                Directory.CreateDirectory("TestResults");
                File.WriteAllBytes("TestResults/11-wall-surface-probes.png", image.EncodeToPNG());
                float Brightness(Vector2 point)
                {
                    Vector3 pixel = camera.WorldToScreenPoint(point);
                    if (pixel.x < 0 || pixel.y < 0 || pixel.x >= 800 || pixel.y >= 600) return 1;
                    return image.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y)).maxColorComponent;
                }
                int wallSamples = 0;
                var b = room.Bounds;
                var wallTiles = new List<Vector2Int>();
                for (int x = b.x; x < b.xMax; x++)
                {
                    wallTiles.Add(new Vector2Int(x, b.y));
                    wallTiles.Add(new Vector2Int(x, b.yMax - 1));
                }
                for (int y = b.y + 1; y < b.yMax - 1; y++)
                {
                    wallTiles.Add(new Vector2Int(b.x, y));
                    wallTiles.Add(new Vector2Int(b.xMax - 1, y));
                }
                foreach (var tile in wallTiles)
                {
                    if (run.Map.IsFloorTile(tile)) continue;
                    wallSamples++;
                    Vector2 point = DungeonTestGeometry.World(tile);
                    foreach (float offsetX in new[] { -.35f, .35f })
                        foreach (float offsetY in new[] { -.35f, .35f })
                        {
                            var sample = point + new Vector2(offsetX, offsetY);
                            // Only inspect the inner wall face; points beyond the room are correctly dark.
                            if (sample.x < b.x || sample.x > b.xMax - 1 ||
                                sample.y < b.y || sample.y > b.yMax - 1) continue;
                            Assert.Greater(Brightness(sample), .95f, "Wall surface " + tile);
                        }
                }
                Assert.Greater(wallSamples, 0);
                Vector2 outside = DungeonTestGeometry.World(new Vector2Int(b.x + 3, b.y - 5));
                Assert.Less(Brightness(outside), .01f, "Space beyond the wall stays dark");
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                Object.Destroy(camera.gameObject); Object.Destroy(backing.gameObject);
                Object.Destroy(target); Object.Destroy(image); Object.Destroy(sprite);
            }
        }

        [UnityTest]
        public IEnumerator SeparateSourcesRevealTheirOwnSideWhileSoundCrossesWalls()
        {
            yield return LoadFloor();
            var geometry = DungeonTestGeometry.FindDoor(run);
            PlacePlayer(DungeonTestGeometry.World(geometry.Source));
            var enemy = run.Enemies[0];
            enemy.GetComponent<Rigidbody2D>().position = DungeonTestGeometry.World(geometry.Around);
            Physics2D.SyncTransforms();
            run.Noise.Emit(DungeonTestGeometry.World(geometry.Source), 15, NoiseKind.Gunshot);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .1f);
            Assert.IsTrue(enemy.Awake, "Sound still wakes enemies through walls");
            Assert.IsFalse(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Around)));
            run.Visibility.Reveal(DungeonTestGeometry.World(geometry.Around), 3);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .03f);
            Assert.IsTrue(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Around)), "Each source uses its own origin for occlusion");
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire(DungeonTestGeometry.World(geometry.Around), false));
            Assert.IsNull(run.Player.AimTarget, "A remote reveal cannot make a shot pass through a wall");
            yield return new WaitForSeconds(run.Config.VisionPulseDuration + .1f);
            Assert.IsFalse(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Around)), "Occluded pulses still expire");
        }

        [UnityTest]
        public IEnumerator AutoAimChoosesTheDoorwayEnemyInsteadOfAnOccludedEnemy()
        {
            yield return LoadFloor();
            var geometry = DungeonTestGeometry.FindDoor(run);
            PlacePlayer(DungeonTestGeometry.World(geometry.Source));
            var hidden = run.Enemies[0];
            var visible = run.Enemies[1];
            hidden.GetComponent<Rigidbody2D>().position = DungeonTestGeometry.World(geometry.Around);
            visible.GetComponent<Rigidbody2D>().position = DungeonTestGeometry.World(geometry.Through);
            Physics2D.SyncTransforms();
            run.Visibility.Reveal(DungeonTestGeometry.World(geometry.Source), 10);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            Assert.IsFalse(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Around)));
            Assert.IsTrue(run.Visibility.IsVisible(DungeonTestGeometry.World(geometry.Through)));
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire(DungeonTestGeometry.World(geometry.Source) + Vector2.up * 3));
            Assert.AreEqual(visible, run.Player.AimTarget);
        }
    }
}

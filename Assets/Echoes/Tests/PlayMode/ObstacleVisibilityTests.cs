using System.Collections;
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

        private IEnumerator LoadFloor(Vector2 player)
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            // Position the player without generating test movement pulses.
            run.Player.enabled = false;
            run.Player.GetComponent<Rigidbody2D>().position = player;
            run.Player.transform.position = player;
            foreach (var enemy in run.Enemies)
            {
                enemy.enabled = false;
                enemy.GetComponent<Rigidbody2D>().position = run.Map.Rooms[4].Center;
            }
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator StationaryGradientStopsAtTheDoorJamb()
        {
            Vector2 source = new Vector2(16.25f, 4);
            yield return LoadFloor(source);
            Vector2 wall = new Vector2(17, 4);
            Vector2 aroundCorner = new Vector2(17, 5);
            Assert.Greater(run.Visibility.VisibilityAt(wall), 0, "The facing wall remains visible");
            Assert.Greater(VisionPulse.Coverage(aroundCorner, source, 0, run.Config.gradientVisionRange), 0);
            Assert.IsFalse(run.Visibility.IsVisible(aroundCorner), "The gradient cannot see around the jamb");
            Assert.AreEqual(0, run.Visibility.Pulses.Count);
        }

        [UnityTest]
        public IEnumerator PulsesRespectWallsAndDoorsAndMatchRenderedDarkness()
        {
            Vector2 source = new Vector2(16, 6);
            yield return LoadFloor(source);
            run.Visibility.Reveal(source, 8);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            Vector2 doorway = new Vector2(20, 6);
            Vector2 behindWall = new Vector2(20, 2);
            Vector2 wall = new Vector2(17, 4);
            Assert.IsTrue(run.Visibility.IsVisible(doorway));
            Assert.IsTrue(run.Visibility.IsVisible(wall));
            Assert.IsFalse(run.Visibility.IsVisible(behindWall));
            run.Visibility.RefreshMask();
            Directory.CreateDirectory("TestResults");
            RuntimeSmokeTests.Capture("TestResults/10-obstacle-visibility.png");

            // White probes under the darkness mask isolate GPU coverage from the floor artwork.
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var probes = new[] { doorway, behindWall, wall };
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
            camera.transform.position = new Vector3(18, 4, -10);
            camera.orthographic = true;
            camera.orthographicSize = 6;
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
        public IEnumerator SeparateSourcesRevealTheirOwnSideWhileSoundCrossesWalls()
        {
            Vector2 source = new Vector2(16, 3);
            Vector2 otherSide = new Vector2(20, 3);
            yield return LoadFloor(source);
            var enemy = run.Enemies[0];
            enemy.GetComponent<Rigidbody2D>().position = otherSide;
            Physics2D.SyncTransforms();
            run.Noise.Emit(source, 7, NoiseKind.Gunshot);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            Assert.IsTrue(enemy.Awake, "Sound still wakes enemies through walls");
            Assert.IsFalse(run.Visibility.IsVisible(otherSide));
            run.Visibility.Reveal(otherSide, 3);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            Assert.IsTrue(run.Visibility.IsVisible(otherSide), "Each source uses its own origin for occlusion");
            Assert.IsTrue(run.Player.TryFire(source + Vector2.down, false));
            Assert.IsNull(run.Player.AimTarget, "A remote reveal cannot make a shot pass through a wall");
            yield return new WaitForSeconds(run.Config.VisionPulseDuration + .1f);
            Assert.IsFalse(run.Visibility.IsVisible(otherSide), "Occluded pulses still expire");
        }

        [UnityTest]
        public IEnumerator AutoAimChoosesTheDoorwayEnemyInsteadOfTheCloserOccludedEnemy()
        {
            Vector2 source = new Vector2(16, 6);
            yield return LoadFloor(source);
            var hidden = run.Enemies[0];
            var visible = run.Enemies[1];
            hidden.GetComponent<Rigidbody2D>().position = new Vector2(19, 4);
            visible.GetComponent<Rigidbody2D>().position = new Vector2(20, 6);
            Physics2D.SyncTransforms();
            run.Visibility.Reveal(source, 7);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .02f);
            Assert.IsFalse(run.Visibility.IsVisible(hidden.transform.position));
            Assert.IsTrue(run.Visibility.IsVisible(visible.transform.position));
            Assert.IsTrue(run.Player.TryFire(source + Vector2.up));
            Assert.AreEqual(visible, run.Player.AimTarget);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    // Produces bright per-floor screenshots for art review; not a gameplay assertion suite.
    public sealed class RoomArtAcceptanceTests
    {
        [UnityTest]
        public IEnumerator CaptureEveryFloorAndRoomShape()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var camera = Camera.main;
            var pixel = FindPixelPerfect(camera);
            bool pixelWasEnabled = pixel && pixel.enabled;
            float originalSize = camera.orthographicSize;
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            cover.enabled = false;
            run.Player.enabled = false;
            Directory.CreateDirectory("TestResults/Art");

            var captured = new HashSet<string>();
            for (int floor = 1; floor <= FloorTheme.Count; floor++)
            {
                foreach (var enemy in run.Enemies) enemy.enabled = false;
                var roomByShape = new Dictionary<RoomShape, DungeonMap.Room>();
                foreach (var room in run.Map.Rooms)
                    if (room.Role == RoomRole.Middle && !roomByShape.ContainsKey(room.Shape))
                        roomByShape[room.Shape] = room;

                yield return Capture(run, camera, pixel, run.Map.Rooms[0],
                    "floor" + floor + "-start", originalSize, captured);
                foreach (var pair in roomByShape)
                    yield return Capture(run, camera, pixel, pair.Value,
                        "floor" + floor + "-" + pair.Key, originalSize, captured);
                yield return Capture(run, camera, pixel, run.Map.Rooms[run.Map.Rooms.Count - 1],
                    "floor" + floor + "-boss", originalSize, captured);

                if (floor >= FloorTheme.Count) break;
                run.ActivateBoss();
                run.Boss.TakeDamage(99999);
                run.AdvanceFloor();
                yield return null;
            }

            Assert.Greater(captured.Count, 3);
            cover.enabled = true;
            run.Player.enabled = true;
            if (pixel) pixel.enabled = pixelWasEnabled;
            camera.orthographicSize = originalSize;
        }

        private static Behaviour FindPixelPerfect(Camera camera)
        {
            foreach (var behaviour in camera.GetComponents<Behaviour>())
                if (behaviour.GetType().Name == "PixelPerfectCamera") return behaviour;
            return null;
        }

        private static IEnumerator Capture(RunController run, Camera camera,
            Behaviour pixel, DungeonMap.Room room, string name,
            float originalSize, HashSet<string> captured)
        {
            if (pixel) pixel.enabled = false;
            camera.orthographicSize = Mathf.Max(10, 3 + Mathf.Max(
                room.Bounds.height * .5f, room.Bounds.width * .5f * 9f / 16f));
            var body = run.Player.GetComponent<Rigidbody2D>();
            body.position = room.Center;
            run.Player.transform.position = room.Center;
            Physics2D.SyncTransforms();
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/Art/" + name + ".png");
            captured.Add(name);
            if (pixel) pixel.enabled = true;
            camera.orthographicSize = originalSize;
        }
    }
}

using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Echoes.Tests
{
    public sealed class StartStallTests
    {
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, System.UIntPtr extra);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(System.IntPtr window);

        private static void Key(byte key, bool down) => keybd_event(key, 0, down ? 0u : 2u, System.UIntPtr.Zero);

        [UnityTearDown]
        public IEnumerator ReleaseKeys()
        {
            foreach (byte key in new byte[] { 0x57, 0x41, 0x53, 0x44, 0x45, 0x1B }) Key(key, false);
            Time.timeScale = 1;
            yield return null;
        }

        private static IEnumerator Walk(RunController run, Vector2 target)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (Vector2.Distance(run.Player.transform.position, target) > .25f && Time.realtimeSinceStartup < deadline)
            {
                Vector2 delta = target - (Vector2)run.Player.transform.position;
                Key(0x44, delta.x > .15f); Key(0x41, delta.x < -.15f);
                Key(0x57, delta.y > .15f); Key(0x53, delta.y < -.15f);
                yield return null;
            }
            foreach (byte key in new byte[] { 0x57, 0x41, 0x53, 0x44 }) Key(key, false);
            yield return new WaitForSeconds(.1f);
            Assert.Less(Vector2.Distance(run.Player.transform.position, target), .4f, "WASD moves the real player to the counter");
        }

        private static IEnumerator Press(byte key)
        {
            Key(key, true);
            yield return new WaitForSecondsRealtime(.15f);
            Key(key, false);
            yield return null;
        }

        private static IEnumerator Capture(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            yield return null;
        }

#if UNITY_EDITOR
        private static void SetCaptureResolution(UnityEditor.EditorWindow gameView)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { System.Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = System.Activator.CreateInstance(sizeType,
                System.Enum.Parse(modeType, "FixedResolution"), 1600, 900, "Stall acceptance 1600x900");
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            gameView.GetType().GetProperty("selectedSizeIndex").SetValue(gameView, index);
        }
#endif

        [UnityTest]
        public IEnumerator ThreeFloorsWalkPromptKeyShopExitAndScreenshots()
        {
            if (Application.isBatchMode) Assert.Ignore("Requires the Editor Game View and native keyboard input.");
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            Application.runInBackground = true;
#if UNITY_EDITOR
            var gameView = UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor"));
            gameView.maximized = true;
            SetCaptureResolution(gameView);
            gameView.Focus();
#endif
            // Permit foreground activation after a command-line Editor launch on Windows.
            Key(0x12, true); Key(0x12, false);
            SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);
#if UNITY_EDITOR
            gameView.Focus();
#endif
            yield return new WaitForSeconds(.5f);
            Directory.CreateDirectory("TestResults/StallAcceptance");
            Sprite shared = null;
            for (int floor = 1; floor <= 3; floor++)
            {
                Assert.AreEqual(floor, run.Floor);
                var stall = Object.FindObjectsOfType<ArtBinding>().Single(binding => binding.Kind == StartStall.Kind);
                var sprite = stall.GetComponent<SpriteRenderer>().sprite;
                if (shared) Assert.AreSame(shared, sprite); else shared = sprite;
                Assert.AreEqual(new Vector2(96, 64), sprite.rect.size);
                Assert.AreEqual(16, sprite.pixelsPerUnit);
                Assert.AreEqual(new Vector2(48, 0), sprite.pivot);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                Assert.AreEqual(1, sprite.texture.mipmapCount);
                Assert.AreEqual(Vector3.one, stall.transform.localScale);
                Assert.IsNull(GameObject.Find("ShopPlaceholder"));
                Assert.IsNull(GameObject.Find("ShopAwning"));
                Assert.IsNull(GameObject.Find("ShopSign"));
                var spec = StartStall.Layout(run.Map.Rooms[0]);
                Assert.AreEqual(new Vector2(5, 3), stall.GetComponent<BoxCollider2D>().size);
                for (int x = spec.X; x < spec.X + spec.Width; x++)
                    for (int y = spec.Y; y < spec.Y + spec.Height; y++)
                        Assert.IsFalse(run.Map.IsWalkable(new Vector2Int(x, y)), "The entire 5x3 footprint blocks movement and sight");
                Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(run.Map.StartPosition)), "Spawn remains clear");
                Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(run.Shop.Position)), "Counter anchor is reachable floor");
                Assert.Greater(run.Map.FindPath(run.Map.StartPosition, run.Shop.Position).Count, 0);
                foreach (var door in run.Map.Rooms[0].DoorOpenings)
                    Assert.Greater(run.Map.FindPath(run.Map.StartPosition, run.Map.NearestFloor(door.center)).Count, 0, "Main routes and doorways remain connected");
                Assert.IsFalse(run.Shop.InRange, "Approach the shop from spawn");
                float elapsed = run.Elapsed;
                int frame = Time.frameCount;
                yield return Walk(run, run.Shop.Position);
                Assert.Greater(Time.frameCount, frame + 1);
                Assert.Greater(run.Elapsed, elapsed);
                Assert.IsTrue(run.Shop.InRange, "The existing distance and line-of-sight check permits E from outside");
                Assert.IsFalse(run.Map.HasLineOfSight(run.Shop.Position,
                    run.Shop.Position + Vector2.up * 5), "The kiosk still blocks sight to the ground behind it");
                yield return new WaitForSeconds(.2f);
                Assert.IsTrue(run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>().enabled);
                run.Visibility.RefreshMask();
                yield return Capture("TestResults/StallAcceptance/floor-" + floor + "-counter.png");
                var stock = run.Shop.Offers[0];
                yield return Press(0x45);
                Assert.AreEqual(RunState.Shop, run.State, "A real E key opens the existing shop");
                yield return Capture("TestResults/StallAcceptance/floor-" + floor + "-shop.png");
                yield return Press(0x1B);
                Assert.AreEqual(RunState.Playing, run.State, "Escape exits the shop");
                Assert.AreSame(stock, run.Shop.Offers[0], "Exiting preserves stock");
                Vector2 before = run.Player.transform.position;
                yield return Walk(run, before + Vector2.down);
                Assert.Greater(Vector2.Distance(before, run.Player.transform.position), .6f, "Movement resumes after closing");
                UnityEngine.Debug.Log("STALL_ACCEPTED floor=" + floor + " spawn=" + run.Map.StartPosition + " counter=" + run.Shop.Position + " input=WASD,E,Escape");
                if (floor == 3) break;
                run.SetState(RunState.FloorCleared);
                run.AdvanceFloor();
                yield return new WaitForSeconds(.3f);
            }
        }
    }
}

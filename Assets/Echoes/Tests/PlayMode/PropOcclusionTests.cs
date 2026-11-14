using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class PropOcclusionTests
    {
        [UnityTest]
        public IEnumerator NativeSanctuaryPropsDoNotReserveInvisibleExtraWidth()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            // All three floors now use P1 art; check actual sanctuary bases.
            for (int floor = 1; floor < 3; floor++)
            {
                run.SetState(RunState.FloorCleared); run.AdvanceFloor();
                yield return null;
            }
            yield return null;
            Physics2D.SyncTransforms();
            var props = run.Map.GetComponentsInChildren<ArtBinding>()
                .Where(a => a.Kind.StartsWith("room_f3_") && a.GetComponent<BoxCollider2D>()).ToArray();
            Assert.IsNotEmpty(props);
            foreach (var prop in props)
            {
                var box = prop.GetComponent<BoxCollider2D>();
                var sprite = prop.GetComponent<SpriteRenderer>();
                float canvasWidth = sprite.sprite.rect.width / sprite.sprite.pixelsPerUnit * sprite.transform.lossyScale.x;
                Debug.Log("PROP_BOUNDS " + prop.transform.position + " physics=" + box.bounds.size +
                    " canvasWidth=" + canvasWidth);
                Assert.LessOrEqual(box.bounds.size.x, Mathf.Ceil(canvasWidth), "Props must not reserve empty floor beyond their image");
            }
            var selected = props.First(a => a.Kind == "room_f3_organ").GetComponent<BoxCollider2D>();
            Vector2 front = run.Map.NearestFloor((Vector2)selected.bounds.center + Vector2.down * 3);
            run.Player.PlaceAt(front);
            run.Player.Weapons.Slots[0].Cooldown = 0;
            run.Visibility.ClearPulses();
            Assert.IsTrue(run.Player.TryFire(selected.bounds.center, true));
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .03f);
            AssertOpaquePropPixelsVisible(run, selected.GetComponent<SpriteRenderer>());
            Camera.main.Render();
            var frame = DrawFootprint(selected.bounds);
            run.Hud.Notify("青框：管风琴阻挡移动的占地", 5);
            Directory.CreateDirectory("TestResults/PropOcclusion");
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            cover.enabled = false;
            RuntimeSmokeTests.Capture("TestResults/PropOcclusion/sanctuary-reference.png");
            cover.enabled = true;
            Vector2 anchor = new Vector2(Mathf.Clamp(front.x, selected.bounds.min.x - .01f, selected.bounds.max.x + .01f),
                Mathf.Clamp(front.y, selected.bounds.min.y - .01f, selected.bounds.max.y + .01f));
            Assert.IsTrue(run.Map.HasLineOfSight(front, anchor), "The prop's front remains visible after floor changes");
            RuntimeSmokeTests.Capture("TestResults/PropOcclusion/sanctuary-footprint.png");
            Object.Destroy(frame.gameObject);
        }

        [UnityTest]
        public IEnumerator VisibleTallPropIsNotCutByItsOwnGroundFootprint()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            // Dense interior walls can box in the first column; pick the one
            // with the most open floor around its upper body so the transparent
            // pixels still exercise a visible floor patch.
            var columns = run.Map.GetComponentsInChildren<ArtBinding>()
                .Where(a => a.Kind == "room_column").ToArray();
            Assert.IsNotEmpty(columns);
            var column = columns[0];
            int bestOpen = -1;
            foreach (var candidate in columns)
            {
                var candidateBox = candidate.GetComponent<BoxCollider2D>();
                int open = 0;
                for (int x = Mathf.FloorToInt(candidateBox.bounds.min.x) - 3;
                     x <= Mathf.CeilToInt(candidateBox.bounds.max.x) + 3; x++)
                    for (int y = Mathf.FloorToInt(candidateBox.bounds.min.y);
                         y <= Mathf.CeilToInt(candidateBox.bounds.max.y) + 8; y++)
                        if (run.Map.IsWalkable(new Vector2Int(x, y))) open++;
                if (open <= bestOpen) continue;
                bestOpen = open;
                column = candidate;
            }
            var renderer = column.GetComponent<SpriteRenderer>();
            var box = column.GetComponent<BoxCollider2D>();
            Vector2 source = run.Map.NearestFloor((Vector2)box.bounds.center +
                new Vector2(-box.bounds.extents.x - 2, -box.bounds.extents.y - 2));
            run.Player.PlaceAt(source);
            run.Visibility.ClearPulses(); run.Visibility.Reveal(source, 20);
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .03f);
            run.Visibility.RefreshMask();
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            var camera = new GameObject("PropOcclusionCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(renderer.bounds.center.x, renderer.bounds.center.y, -10);
            camera.orthographic = true; camera.orthographicSize = 6;
            var target = new RenderTexture(800, 600, 24);
            var clear = new Texture2D(800, 600, TextureFormat.RGB24, false);
            var shaded = new Texture2D(800, 600, TextureFormat.RGB24, false);
            var art = new Texture2D(2, 2);
            var previousActive = RenderTexture.active;
            try
            {
                art.LoadImage(File.ReadAllBytes("Assets/Echoes/Resources/" + RoomArt.Definition("room_column").Path + ".png"));
                camera.targetTexture = target;
                // Initialize this diagnostic camera's URP resources before measuring.
                camera.Render();
                void Read(Texture2D image)
                {
                    camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); image.Apply();
                }
                Read(shaded);
                cover.enabled = false; Read(clear); cover.enabled = true;
                Directory.CreateDirectory("TestResults/PropOcclusion");
                File.WriteAllBytes("TestResults/PropOcclusion/tall-prop.png", shaded.EncodeToPNG());
                int checkedPixels = 0, cutPixels = 0, clearGaps = 0, litGaps = 0;
                for (int y = 1; y < art.height; y += 2)
                    for (int x = 1; x < art.width; x += 2)
                    {
                        Vector2 local = (new Vector2(x + .5f, y + .5f) - renderer.sprite.pivot) / renderer.sprite.pixelsPerUnit;
                        Vector3 world = renderer.transform.TransformPoint(local);
                        Vector3 p = camera.WorldToScreenPoint(world);
                        int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y);
                        Vector2 sampleWorld = camera.ScreenToWorldPoint(new Vector3(px + .5f, py + .5f, 10));
                        float reference = clear.GetPixel(px, py).maxColorComponent;
                        if (reference < .12f) continue;
                        float alpha = art.GetPixel(x, y).a;
                        bool clearPatch = alpha < .05f;
                        // Leave a one-art-pixel margin so the camera's pixel sample
                        // cannot land on the neighbouring opaque silhouette edge.
                        for (int dy = -1; dy <= 1 && clearPatch; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if (x + dx < 0 || y + dy < 0 || x + dx >= art.width || y + dy >= art.height ||
                                    art.GetPixel(x + dx, y + dy).a >= .05f) clearPatch = false;
                        if (clearPatch && run.Map.IsWalkable(Vector2Int.FloorToInt(sampleWorld + Vector2.one * .5f)) &&
                            run.Map.HasLineOfSight(source, sampleWorld))
                        {
                            clearGaps++;
                            if (shaded.GetPixel(px, py).maxColorComponent > .01f) litGaps++;
                        }
                        if (alpha < .95f) continue;
                        checkedPixels++;
                        if (shaded.GetPixel(px, py).maxColorComponent < reference * .8f) cutPixels++;
                    }
                Debug.Log("PROP_SELF_OCCLUSION checked=" + checkedPixels + " cut=" + cutPixels + " gaps=" + clearGaps + " lit=" + litGaps);
                Assert.Greater(checkedPixels, 50);
                Assert.Greater(clearGaps, 0, "Exercise transparent pixels over visible floor");
                Assert.AreEqual(clearGaps, litGaps, "Floor stays visible through transparent gaps");
                Assert.AreEqual(0, cutPixels, "Opaque upper-body pixels must not be cut by the prop's own footprint");
            }
            finally
            {
                cover.enabled = true; RenderTexture.active = previousActive; camera.targetTexture = null;
                Object.Destroy(camera.gameObject); Object.Destroy(target); Object.Destroy(clear);
                Object.Destroy(shaded); Object.Destroy(art);
            }
            run.Visibility.ClearPulses();
            run.Player.Weapons.Slots[0].Cooldown = 0;
            Assert.IsTrue(run.Player.TryFire(box.bounds.center, true));
            yield return new WaitForSeconds(run.Config.visionExpandDuration + .03f);
            run.Visibility.RefreshMask();
            RuntimeSmokeTests.Capture("TestResults/PropOcclusion/tall-prop-fixed.png");
            var outline = DrawFootprint(box.bounds);
            run.Hud.Notify("青框：阻挡移动的占地", 5);
            RuntimeSmokeTests.Capture("TestResults/PropOcclusion/tall-prop-footprint.png");
            Object.Destroy(outline.gameObject);
        }

        private static LineRenderer DrawFootprint(Bounds bounds)
        {
            var outline = PixelArt.Circle("SharedCollisionAndVisionBounds", null, PixelArt.Cyan, .035f);
            outline.useWorldSpace = true; outline.loop = true; outline.positionCount = 4; outline.sortingOrder = 50;
            outline.SetPositions(new[] { new Vector3(bounds.min.x, bounds.min.y), new Vector3(bounds.max.x, bounds.min.y),
                new Vector3(bounds.max.x, bounds.max.y), new Vector3(bounds.min.x, bounds.max.y) });
            return outline;
        }

        private static void AssertOpaquePropPixelsVisible(RunController run, SpriteRenderer renderer)
        {
            var camera = new GameObject("BenchPixelCamera").AddComponent<Camera>();
            camera.transform.position = renderer.bounds.center + Vector3.back * 10;
            camera.orthographic = true; camera.orthographicSize = 4;
            var target = new RenderTexture(800, 600, 24);
            var clear = new Texture2D(800, 600, TextureFormat.RGB24, false);
            var shaded = new Texture2D(800, 600, TextureFormat.RGB24, false);
            var sheet = new Texture2D(2, 2);
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            var previous = RenderTexture.active;
            try
            {
                string kind = renderer.GetComponent<ArtBinding>().Kind;
                sheet.LoadImage(File.ReadAllBytes("Assets/Echoes/Resources/" + RoomArt.Definition(kind).Path + ".png"));
                camera.targetTexture = target; camera.Render();
                void Read(Texture2D image)
                {
                    camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); image.Apply();
                }
                cover.enabled = false; Read(clear); cover.enabled = true; Read(shaded);
                int count = 0, cut = 0;
                var sprite = renderer.sprite;
                for (int y = 1; y < sprite.rect.height; y += 2)
                    for (int x = 1; x < sprite.rect.width; x += 2)
                    {
                        if (sheet.GetPixel((int)sprite.rect.x + x, (int)sprite.rect.y + y).a < .95f) continue;
                        Vector2 local = (new Vector2(x + .5f, y + .5f) - sprite.pivot) / sprite.pixelsPerUnit;
                        Vector3 p = camera.WorldToScreenPoint(renderer.transform.TransformPoint(local));
                        int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y);
                        float reference = clear.GetPixel(px, py).maxColorComponent;
                        if (reference < .12f) continue;
                        count++;
                        if (shaded.GetPixel(px, py).maxColorComponent < reference * .8f) cut++;
                    }
                Debug.Log("PROP_PIXELS checked=" + count + " cut=" + cut);
                Assert.Greater(count, 20); Assert.AreEqual(0, cut, "The organ pipes must not disappear into their own shadow");
            }
            finally
            {
                cover.enabled = true; RenderTexture.active = previous; camera.targetTexture = null;
                Object.Destroy(camera.gameObject); Object.Destroy(target); Object.Destroy(clear);
                Object.Destroy(shaded); Object.Destroy(sheet);
            }
        }
    }
}

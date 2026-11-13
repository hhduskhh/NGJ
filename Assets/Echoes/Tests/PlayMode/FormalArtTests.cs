using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class FormalArtTests
    {
        [UnityTest]
        public IEnumerator FirstFloorSpritesAndAnimationAreUsedInGame()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var art = run.Player.GetComponent<CharacterArt>();
            var sprite = run.Player.GetComponent<SpriteRenderer>().sprite;
            Assert.AreEqual(32, sprite.rect.width);
            Assert.AreEqual(16, sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            Assert.AreEqual(20, Resources.LoadAll<Sprite>("Art/Common/hero").Length);
            Assert.AreEqual(6, Resources.LoadAll<Sprite>("Art/Floor1/walker").Length);
            Assert.AreEqual(64, run.Boss.GetComponent<SpriteRenderer>().sprite.rect.width);
            Assert.AreEqual(Vector2.zero, run.Player.GetComponent<CircleCollider2D>().offset);
            Assert.AreEqual(Vector2.zero, run.Enemies[0].GetComponent<CircleCollider2D>().offset);
            Assert.AreEqual(Vector2.zero, run.Boss.GetComponent<CircleCollider2D>().offset);
            Assert.IsTrue(Object.FindObjectsOfType<ArtBinding>().Length > 1000);
            float elapsed = run.Elapsed;
            var enemy = run.Enemies[0];
            enemy.GetComponent<Rigidbody2D>().position = (Vector2)run.Player.transform.position + Vector2.right * 4;
            run.Enemies[1].GetComponent<Rigidbody2D>().position = (Vector2)run.Player.transform.position + new Vector2(5,2);
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.12f);
            Assert.AreEqual(0, enemy.GetComponent<CharacterArt>().CurrentFrame, "Sleeping artwork must close the core");
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            cover.enabled = false; // Diagnostic lighting only; normal combat screenshot restores darkness.
            yield return null;
            Capture("01-floor1-overview");
            cover.enabled = true;
            enemy.Hear((Vector2)run.Player.transform.position + Vector2.up * 2);
            yield return new WaitForSeconds(.2f);
            Assert.That(enemy.GetComponent<CharacterArt>().CurrentFrame, Is.InRange(1,3));
            Assert.Greater(run.Elapsed, elapsed, "Screenshot must come from an advancing simulation");
            Assert.IsTrue(run.Player.TryFire((Vector2)run.Player.transform.position + Vector2.left * 20, true));
            yield return new WaitForSeconds(.04f);
            Assert.That(art.CurrentFrame, Is.InRange(8,11), "A real shot selects attack frames");
            Capture("02-floor1-sonar-combat");
            run.Player.TakeDamage(1);
            yield return new WaitForSeconds(.04f);
            Assert.That(art.CurrentFrame, Is.InRange(12,15), "Damage selects hurt frames");
            enemy.TakeDamage(99999);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(5, enemy.GetComponent<CharacterArt>().CurrentFrame);
            Assert.AreEqual(Vector3.one, enemy.transform.localScale, "A corpse keeps its pixel density and foot origin");
            yield return new WaitForSeconds(run.Config.playerInvulnerability + .1f);
            run.Player.TakeDamage(99999);
            Assert.AreEqual(RunState.Lost, run.State);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(19, art.CurrentFrame, "Death finishes even after gameplay time is paused");
            Capture("03-player-defeat");
        }

        [UnityTest]
        public IEnumerator ExpandedThemesAndBossUsePreparedSpritesWithoutNewMechanics()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            int population = run.Enemies.Count;
            int rooms = run.Map.Rooms.Count;
            var cover = run.transform.Find("UnseenDungeon").GetComponent<SpriteRenderer>();
            cover.enabled = false;
            for (int theme = 1; theme <= 2; theme++)
            {
                run.Map.ApplyArtTheme(theme);
                run.Enemies[0].GetComponent<Rigidbody2D>().position = (Vector2)run.Player.transform.position + Vector2.right * 4;
                Physics2D.SyncTransforms();
                yield return new WaitForSeconds(.1f);
                Assert.AreEqual("walker", run.Enemies[0].GetComponent<CharacterArt>().Species);
                Assert.That(run.Enemies[0].GetComponent<SpriteRenderer>().sprite.texture.name, Is.EqualTo("walker"));
                foreach (var species in new[] { "walker", "guard", "elite", "boss" })
                    Assert.AreEqual(6, Resources.LoadAll<Sprite>("Art/Floor" + (theme+1) + "/" + species).Length);
                Assert.AreEqual(population, run.Enemies.Count);
                Assert.AreEqual(16, Resources.LoadAll<Sprite>("Art/Floor" + (theme+1) + "/props").Length);
                Assert.AreEqual(8, Resources.LoadAll<Sprite>("Art/Floor" + (theme+1) + "/tiles").Length);
                Assert.AreEqual(rooms, run.Map.Rooms.Count);
                Capture("0" + (theme+3) + "-floor" + (theme+1) + "-runtime-art-review");
            }
            run.Map.ApplyArtTheme(0);
            run.Player.GetComponent<Rigidbody2D>().position = run.Map.AltarPosition;
            Physics2D.SyncTransforms();
            run.ActivateBoss();
            yield return new WaitForSeconds(run.Config.bossAttackInterval + .1f);
            Assert.IsTrue(run.Boss.IsWindingUp);
            Assert.AreEqual(4, run.Boss.GetComponent<CharacterArt>().CurrentFrame);
            Capture("06-boss-windup");
            cover.enabled = true;
        }

        private static void Capture(string name)
        {
            Directory.CreateDirectory("TestResults/Art");
            RuntimeSmokeTests.Capture("TestResults/Art/" + name + ".png");
        }
    }
}

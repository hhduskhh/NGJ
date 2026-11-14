using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class CombatAudioTests
    {
        [UnityTest]
        public IEnumerator EmptyCityLoopsAcrossAllFloors()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var clip = Resources.Load<AudioClip>("EmptyCity");
            Assert.IsNotNull(clip);
            var music = run.GetComponents<AudioSource>().Single(source => source.clip == clip);
            Assert.IsTrue(music.loop);
            Assert.IsTrue(music.isPlaying);
            run.Player.PlaceAt(run.Map.BossPosition);
            yield return null;
            Assert.IsTrue(music.isPlaying);
            for (int floor = 2; floor <= FloorTheme.Count; floor++)
            {
                run.SetState(RunState.FloorCleared);
                run.AdvanceFloor();
                yield return null;
                Assert.AreEqual(floor, run.Floor);
                Assert.IsTrue(music.isPlaying);
                Assert.AreSame(music, run.GetComponents<AudioSource>().Single(source => source.clip == clip));
            }
        }

        [UnityTest]
        public IEnumerator EnemyStepsFollowDistanceAndDoNotRevealOrWake()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            var enemy = run.Enemies.First(e => !e.IsGuard && !e.IsElite && !e.IsBoss);
            var sleeper = run.Enemies.First(e => e != enemy && !e.IsBoss);
            var body = enemy.GetComponent<Rigidbody2D>();
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Vector2 start = (Vector2)run.Player.transform.position + Vector2.left * 6;
            body.position = start;
            sleeper.GetComponent<Rigidbody2D>().position = start + Vector2.up;
            yield return new WaitForFixedUpdate();
            enemy.Hear(start);
            var voices = run.Noise.GetComponents<AudioSource>();
            var step = Resources.Load<AudioClip>("CombatAudio/EnemyStep");
            body.position += Vector2.right * run.Config.enemyStepDistance * .6f;
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(voices.Any(v => v.clip == step));
            body.position += Vector2.right * run.Config.enemyStepDistance * .6f;
            yield return new WaitForFixedUpdate();
            var voice = voices.Single(v => v.clip == step);
            Assert.Less(voice.panStereo, 0);
            voice.Stop();
            voice.clip = null;
            yield return new WaitForSeconds(.65f);
            Assert.IsFalse(voices.Any(v => v.clip == step), "Stationary enemies stay quiet");
            Assert.IsFalse(sleeper.Awake);
            Assert.AreEqual(0, run.Visibility.Pulses.Count);
        }

        [UnityTest]
        public IEnumerator ShortClipsLoadAndLethalHitReplacesSameFrameImpact()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (string name in new[] { "Pistol", "Shotgun", "Bow", "Hit", "Pickup",
                "Wake", "EnemyStep", "GuardReady", "BossReady", "Ambience" })
            {
                var clip = Resources.Load<AudioClip>("CombatAudio/" + name);
                Assert.IsNotNull(clip, name);
                Assert.That(clip.length, Is.InRange(.05f, 1f), name);
            }

            var target = run.Enemies[0];
            target.transform.position = run.Player.transform.position + Vector3.right * 2;
            target.TakeDamage(1);
            target.TakeDamage(target.Health);
            yield return null;
            var voices = run.Noise.GetComponents<AudioSource>()
                .Where(source => source.clip && source.clip.name == "Hit").ToArray();
            Assert.AreEqual(1, voices.Length);
            Assert.Less(voices[0].pitch, 1f);
            Assert.Greater(voices[0].panStereo, 0f);
        }
    }
}

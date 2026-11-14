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
        public IEnumerator ShortClipsLoadAndLethalHitReplacesSameFrameImpact()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (string name in new[] { "Pistol", "Shotgun", "Bow", "Hit", "Pickup",
                "Wake", "GuardReady", "BossReady", "Ambience" })
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

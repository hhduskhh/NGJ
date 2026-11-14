using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class HearingAndVisionPulseTests
    {
        private IEnumerator LoadFloor()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (var enemy in run.Enemies)
                enemy.GetComponent<Rigidbody2D>().position = run.Map.Rooms[1].Center;
            yield return new WaitForSeconds(.04f);
        }

        [Test]
        public void ExpansionSlowsAndContractionAcceleratesAroundHalfSecondHold()
        {
            float Radius(float age) => VisionPulse.EvaluateRadius(age, 4, 1, .5f, 1);
            Assert.AreEqual(0, Radius(0));
            Assert.Greater(Radius(.25f), Radius(.5f) - Radius(.25f));
            Assert.Greater(Radius(.5f) - Radius(.25f), Radius(.75f) - Radius(.5f));
            Assert.AreEqual(4, Radius(1));
            Assert.AreEqual(4, Radius(1.49f));
            Assert.AreEqual(4, Radius(1.5f));
            Assert.Less(4 - Radius(1.75f), Radius(1.75f) - Radius(2));
            Assert.Less(Radius(1.75f) - Radius(2), Radius(2) - Radius(2.25f));
            Assert.AreEqual(0, Radius(2.5f));
        }

        [Test]
        public void GradientWidthStaysFixedOutsideMainVision()
        {
            Assert.AreEqual(1, VisionPulse.Coverage(Vector2.zero, Vector2.zero, 0, 1.5f));
            Assert.AreEqual(.5f, VisionPulse.Coverage(new Vector2(.75f, 0), Vector2.zero, 0, 1.5f), .001f);
            Assert.AreEqual(0, VisionPulse.Coverage(new Vector2(1.5f, 0), Vector2.zero, 0, 1.5f));
            Assert.AreEqual(1, VisionPulse.Coverage(new Vector2(3, 0), Vector2.zero, 3, 1.5f));
            Assert.AreEqual(.5f, VisionPulse.Coverage(new Vector2(3.75f, 0), Vector2.zero, 3, 1.5f), .001f);
            Assert.AreEqual(0, VisionPulse.Coverage(new Vector2(4.5f, 0), Vector2.zero, 3, 1.5f));
        }

        [UnityTest]
        public IEnumerator EnemiesInvestigateSourcesUntilTheyAreCloseEnough()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            var enemy = run.Enemies[0];
            Vector2 player = run.Player.transform.position;
            enemy.GetComponent<Rigidbody2D>().position = player + Vector2.right * (run.Config.WalkingDetectionRadius + 1);
            yield return new WaitForSeconds(.04f);
            Vector2 source = new Vector2(15, 10);
            enemy.Hear(source);
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual(EnemyState.Investigating, enemy.State);
            Assert.AreEqual(run.Map.NearestFloor(source), enemy.InvestigationTarget);
            enemy.GetComponent<Rigidbody2D>().position = player + Vector2.right * (run.Config.WalkingDetectionRadius - .5f);
            yield return new WaitForSeconds(.08f);
            Assert.AreEqual(EnemyState.Chasing, enemy.State);
            run.Player.GetComponent<Rigidbody2D>().position = new Vector2(5, 3);
            yield return new WaitForSeconds(.08f);
            Vector2 lastSeen = enemy.LastSeenPosition;
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual(lastSeen, enemy.LastSeenPosition, "Leaving detection range stops live position tracking");
            Assert.Greater(Vector2.Distance(lastSeen, run.Player.transform.position), 1);
            enemy.Hear(source);
            Assert.AreEqual(EnemyState.Investigating, enemy.State);
            Assert.AreEqual(run.Map.NearestFloor(source), enemy.InvestigationTarget);
        }

        [UnityTest]
        public IEnumerator WalkingStartsAtPointTwoSecondsAndRepeatsEveryHalfSecond()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            var body = run.Player.GetComponent<Rigidbody2D>();
            Assert.AreEqual(0, run.Visibility.Pulses.Count);
            Vector2 player = run.Player.transform.position;
            Assert.AreEqual(3, run.Config.gradientVisionRange, "The initial gradient width is doubled from 1.5");
            Assert.AreEqual(.5f, run.Visibility.VisibilityAt(player + Vector2.up * (run.Config.gradientVisionRange / 2)), .01f);
            Assert.IsFalse(run.Visibility.IsVisible(player + Vector2.up * (run.Config.gradientVisionRange + .1f)));
            var nearby = run.Enemies[0];
            var gradientOnly = run.Enemies[1];
            nearby.GetComponent<Rigidbody2D>().position = player + Vector2.up * 2;
            gradientOnly.GetComponent<Rigidbody2D>().position = player + Vector2.up * (run.Config.playerVisionRadius + 1.25f);
            yield return new WaitForSeconds(.04f);
            Assert.IsFalse(nearby.Awake, "Remaining still must not emit footsteps");
            AudioListener.volume = 0; // Muting sound must not disable gameplay hearing.
            Directory.CreateDirectory("TestResults");
            RuntimeSmokeTests.Capture("TestResults/07-stationary-gradient.png");
            int initialSteps = Mathf.CeilToInt(run.Config.walkFirstPulseDelay / Time.fixedDeltaTime);
            for (int i = 0; i < initialSteps - 1; i++)
            {
                body.position += Vector2.right * .05f;
                yield return new WaitForFixedUpdate();
                Assert.AreEqual(0, run.Visibility.Pulses.Count, "No pulse before the initial walking delay");
                Assert.IsFalse(nearby.Awake, "No footstep can arrive before the first walking pulse");
            }
            body.position += Vector2.right * .05f;
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, run.Visibility.Pulses.Count);
            var first = run.Visibility.Pulses[0];
            Vector2 origin = first.Origin;
            int intervalSteps = Mathf.RoundToInt(run.Config.walkPulseInterval / Time.fixedDeltaTime);
            for (int i = 0; i < intervalSteps; i++)
            {
                body.position += Vector2.right * .05f;
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(2, run.Visibility.Pulses.Count);
            Assert.AreEqual(origin, first.Origin);
            Assert.Greater(Vector2.Distance(first.Origin, run.Visibility.Pulses[1].Origin), 1);
            Assert.AreEqual(run.Config.walkPulseInterval,
                run.Visibility.Pulses[1].StartedAt - first.StartedAt, Time.fixedDeltaTime * 2);
            Assert.IsTrue(nearby.Awake, "Actual walking must emit a sound wave that wakes a nearby enemy");
            Assert.IsTrue(run.Visibility.IsVisible(gradientOnly.transform.position));
            Assert.IsFalse(gradientOnly.Awake, "The gradient provides vision without extending footstep hearing");
            for (int i = 1; i < run.Enemies.Count; i++) Assert.IsFalse(run.Enemies[i].Awake);
            nearby.TakeDamage(99999); // Prevent contact pushes from affecting the stopping/cadence check.
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/08-walking-pulses.png");
            yield return new WaitForSeconds(run.Config.VisionPulseDuration + .1f);
            Assert.AreEqual(0, run.Visibility.Pulses.Count, "Stopping adds no pulses and old pulses finish shrinking");
            for (int i = 0; i < initialSteps - 1; i++)
            {
                body.position += Vector2.right * .05f;
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(0, run.Visibility.Pulses.Count, "Starting again restarts the initial delay");
        }

        [UnityTest]
        public IEnumerator ShootingCreatesAnAnchoredPulseThatFreezesWhilePaused()
        {
            yield return LoadFloor();
            var run = Object.FindObjectOfType<RunController>();
            Vector2 source = run.Player.transform.position;
            Assert.IsTrue(run.Player.TryFire(source + Vector2.right * 5));
            Assert.AreEqual(1, run.Visibility.Pulses.Count);
            var pulse = run.Visibility.Pulses[0];
            Assert.AreEqual(source, pulse.Origin);
            Assert.AreEqual(run.Player.NoiseRadius, pulse.MaximumRadius);
            run.Player.GetComponent<Rigidbody2D>().position += Vector2.right * 2;
            yield return new WaitForSeconds(run.Config.visionExpandDuration);
            Assert.AreEqual(source, pulse.Origin, "The shooting circle must not follow the player");
            Assert.AreEqual(pulse.MaximumRadius, pulse.RadiusAt(Time.time, run.Config), .001f);
            run.SetState(RunState.Paused);
            float radius = pulse.RadiusAt(Time.time, run.Config);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(radius, pulse.RadiusAt(Time.time, run.Config));
            run.SetState(RunState.Playing);
            yield return null;
            Directory.CreateDirectory("TestResults");
            RuntimeSmokeTests.Capture("TestResults/09-shooting-pulse.png");
        }
    }
}

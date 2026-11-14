using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class EnemyRoleTests
    {
        private RunController run;
        private EnemyController guard;
        private float previousStep;

        [UnitySetUp] public IEnumerator Load()
        {
            previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = .02f;
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            run = Object.FindObjectOfType<RunController>();
            guard = run.Enemies.First(e => e.IsGuard);
        }

        [TearDown] public void RestoreClock() => Time.captureDeltaTime = previousStep;

        internal static void PlaceEnemy(EnemyController enemy, Vector2 point)
        {
            enemy.GetComponent<Rigidbody2D>().position = point;
            enemy.transform.position = point;
            Physics2D.SyncTransforms();
        }

        private Projectile Shot() => Object.FindObjectsOfType<Projectile>().SingleOrDefault();

        [UnityTest] public IEnumerator GuardAcquiresAtShortRangeTracksDuringWindupAndFiresStraight()
        {
            Vector2 center = run.Player.transform.position;
            PlaceEnemy(guard, center + Vector2.right * 7);
            yield return new WaitForSeconds(.12f);
            Assert.AreEqual(EnemyState.Sleeping, guard.State);
            guard.Hear(guard.transform.position + Vector3.left * 2);
            yield return new WaitForSeconds(.12f);
            Assert.AreEqual(EnemyState.Investigating, guard.State);
            Assert.IsFalse(guard.IsWindingUp);
            PlaceEnemy(guard, center + Vector2.right * 5);
            yield return new WaitForSeconds(.12f);
            Assert.IsTrue(guard.IsWindingUp);
            run.Player.PlaceAt(center + new Vector2(-2, 2));
            yield return new WaitForSeconds(.30f);
            Assert.IsTrue(guard.IsWindingUp);
            Assert.AreEqual(4, guard.GetComponent<CharacterArt>().CurrentFrame);
            Assert.Less(guard.GetComponent<Rigidbody2D>().velocity.magnitude, .01f);
            Assert.IsFalse(run.Visibility.IsVisible(guard.transform.position));
            var warning = guard.transform.Find("GuardMuzzleWarning").GetComponent<SpriteRenderer>();
            Assert.IsTrue(warning.enabled);
            Assert.Greater(warning.sortingOrder, 40);
            RuntimeSmokeTests.Capture("TestResults/guard-windup.png");
            float timeout = Time.time + 1;
            while (!Shot() && Time.time < timeout) yield return null;
            var shot = Shot();
            Assert.IsNotNull(shot);
            yield return null;
            var sprite = shot.GetComponent<SpriteRenderer>();
            Assert.IsTrue(sprite.enabled);
            Assert.Greater(sprite.sortingOrder, 40);
            RuntimeSmokeTests.Capture("TestResults/guard-shot.png");
            Vector2 origin = shot.transform.position;
            Vector2 expected = ((Vector2)run.Player.transform.position - (Vector2)guard.transform.position).normalized;
            run.Player.PlaceAt(center + new Vector2(-2, -2));
            float hp = run.Player.Health;
            yield return new WaitForSeconds(.24f);
            Assert.Less(Vector2.Angle(expected, (Vector2)shot.transform.position - origin), 1);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(hp, run.Player.Health);
        }

        [UnityTest] public IEnumerator CoverAndCameraCancelWindupAndWallsStopHostileRounds()
        {
            var door = DungeonTestGeometry.FindDoor(run);
            run.Player.PlaceAt(door.Through);
            PlaceEnemy(guard, door.Source);
            guard.Hear(door.Through);
            yield return new WaitForSeconds(.18f);
            Assert.IsTrue(guard.IsWindingUp);
            run.Player.PlaceAt(door.Around);
            yield return new WaitForSeconds(.06f);
            Assert.IsFalse(guard.IsWindingUp);
            Assert.IsFalse(run.Visibility.CanSeeDanger(guard.transform.position));
            run.Player.PlaceAt(door.Through);
            yield return new WaitForSeconds(.18f);
            Assert.IsTrue(guard.IsWindingUp);
            var camera = Camera.main;
            camera.GetComponent<CameraFollow>().enabled = false;
            camera.transform.position += Vector3.right * 80;
            yield return new WaitForSeconds(run.Config.guardWindup + .1f);
            Assert.IsFalse(guard.IsWindingUp);
            Assert.IsNull(Shot());
            run.Player.PlaceAt(door.Around);
            camera.transform.position = (Vector3)(Vector2)door.Around + Vector3.back * 10;
            Vector2 start = door.Source, end = door.Around;
            Projectile.Create(run, start, end - start, run.Config.guardBulletSpeed, 9, true);
            Assert.IsFalse(Shot().GetComponent<SpriteRenderer>().enabled);
            yield return new WaitForSeconds(Vector2.Distance(start, end) / run.Config.guardBulletSpeed + .2f);
            Assert.IsNull(Shot(), "Wall intercepts the round before its lifetime expires");
            Assert.AreEqual(run.Player.MaxHealth, run.Player.Health);
        }

        [UnityTest] public IEnumerator RolesMatchFloorArtAndWalkerCanBeOutrun()
        {
            for (int floor = 1; floor <= 3; floor++)
            {
                foreach (var e in run.Enemies)
                {
                    Assert.AreEqual(EnemyState.Sleeping, e.State);
                    Assert.AreEqual(e.IsBoss ? "boss" : e.IsGuard ? "guard" : "walker", e.GetComponent<CharacterArt>().Species);
                }
                if (floor < 3)
                {
                    run.SetState(RunState.FloorCleared);
                    run.AdvanceFloor();
                    yield return null;
                }
            }
            var walker = run.Enemies.First(e => !e.IsBoss && !e.IsGuard);
            var start = (Vector2)run.Player.transform.position;
            PlaceEnemy(walker, start + Vector2.left * 3);
            walker.Hear(start);
            yield return new WaitForSeconds(.16f);
            Assert.AreEqual(3.8f, walker.GetComponent<Rigidbody2D>().velocity.magnitude, .01f);
            float gap = Vector2.Distance(walker.transform.position, start);
            var movement = typeof(PlayerController).GetField("movement",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < 30; i++)
            {
                movement.SetValue(run.Player, Vector2.right);
                yield return null;
            }
            Assert.Greater(Vector2.Distance(walker.transform.position, run.Player.transform.position), gap + .15f);
        }
    }
}

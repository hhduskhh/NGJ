using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class RoomCombatTrialTests
    {
        [UnityTest] public IEnumerator PistolOneGroupAtATime() => Trial(false, false);
        [UnityTest] public IEnumerator PistolBothGroups() => Trial(true, false);
        [UnityTest] public IEnumerator PistolAndShotgunBothGroups() => Trial(true, true);

        private static IEnumerator Trial(bool together, bool shotgun)
        {
            var previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = .02f;
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype");
                yield return null;
                var run = Object.FindObjectOfType<RunController>();
                var player = run.Player;
                Assert.AreEqual(713, run.Config.randomSeed);
                var room = run.Map.Rooms[1];
                Assert.AreEqual(RoomRole.Middle, room.Role);
                Assert.AreEqual(RoomShape.Line2, room.Shape);
                Vector2 lane = new Vector2(15, 58);
                var residents = run.Enemies.Where(e => run.Map.RoomAtPosition(e.transform.position) == room).ToArray();
                Assert.AreEqual(4, residents.Length);
                var walkers = residents.Where(e => !e.IsGuard).ToArray();
                var guards = residents.Where(e => e.IsGuard).ToArray();
                var group = new[] { walkers[0], guards[0], walkers[1], guards[1] };
                var offsets = new[] { new Vector2(5, 0), new Vector2(6, 1), new Vector2(11, 0), new Vector2(12, 1) };
                for (int i = 0; i < group.Length; i++)
                {
                    var point = lane + offsets[i];
                    Assert.IsTrue(run.Map.IsWalkable(Vector2Int.RoundToInt(point)));
                    Assert.AreSame(room, run.Map.RoomAtPosition(point));
                    EnemyRoleTests.PlaceEnemy(group[i], point);
                    Assert.AreEqual(32, group[i].Health);
                }
                // Keep adjacent rooms out of this comparison.
                foreach (var other in run.Enemies.Except(group)) other.gameObject.SetActive(false);
                player.PlaceAt(lane + Vector2.right * 2);
                if (shotgun) Assert.IsTrue(player.Weapons.Add(1, 0));
                yield return new WaitForSeconds(.55f);
                if (together) BellDecoy.Create(run, lane + Vector2.right * 7);
                else Assert.IsTrue(player.TryFire(group[0].transform.position, true));
                var movement = typeof(PlayerController).GetField("movement", BindingFlags.Instance | BindingFlags.NonPublic);
                float start = -1, clock = Time.time, firstPair = -1;
                int peakNearby = 0;
                float peakDamage = 1;
                bool allAwake = false, isolated = false;
                while (group.Any(e => e.Alive) && Time.time - clock < 45 && run.State != RunState.Lost)
                {
                    if (run.State == RunState.Upgrade)
                        run.ChooseUpgrade(run.Choices.FindIndex(u => u.Title != "应急修复"));
                    if (start < 0 && group.Any(e => e.Awake)) start = Time.time;
                    if (group.All(e => e.Awake)) allAwake = true;
                    bool firstDead = !group[0].Alive && !group[1].Alive;
                    if (firstDead && firstPair < 0)
                    {
                        firstPair = Time.time - start;
                        isolated = group.Skip(2).All(e => e.State == EnemyState.Sleeping);
                    }
                    var target = group.Where(e => e.Alive && (together || firstDead || e == group[0] || e == group[1]))
                        .OrderBy(e => Vector2.Distance(e.transform.position, player.transform.position)).First();
                    Vector2 delta = target.transform.position - player.transform.position;
                    Vector2 pos = (Vector2)player.transform.position - lane;
                    // Advance into range, yield ground to close walkers and strafe along the lane.
                    float x = delta.magnitude > 4.8f ? Mathf.Sign(delta.x) :
                        !target.IsGuard && delta.magnitude < 2.5f ? -Mathf.Sign(delta.x) : 0;
                    if ((pos.x < 1 && x < 0) || (pos.x > 13 && x > 0)) x = 0;
                    float side = Mathf.PingPong(Time.time - clock, 1.8f) - .9f;
                    movement.SetValue(player, new Vector2(x, Mathf.Clamp((side - pos.y) * 2, -1, 1)).normalized);
                    if (delta.magnitude < 7 && run.Map.HasLineOfSight(player.transform.position, target.transform.position))
                        player.TryFire(target.transform.position, true);
                    peakNearby = Mathf.Max(peakNearby, run.NearbyEnemies);
                    peakDamage = Mathf.Max(peakDamage, run.DamageMultiplier);
                    yield return null;
                }
                Assert.GreaterOrEqual(start, 0);
                Debug.Log($"ROOM_RESULT together={together} shotgun={shotgun} duration={Time.time-start:F2} damage={100-player.Health} firstPair={firstPair:F2} isolated={isolated} allAwake={allAwake} peakNearby={peakNearby} multiplier={peakDamage:F2} kills={run.Kills}");
                Assert.IsTrue(group.All(e => !e.Alive), "Room did not clear");
                if (together) Assert.IsTrue(allAwake);
                else Assert.IsTrue(isolated, "Second pair must sleep until the first pair dies");
            }
            finally { Time.captureDeltaTime = previousStep; }
        }
    }
}

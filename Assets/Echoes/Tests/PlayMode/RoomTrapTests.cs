using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echoes.Tests
{
    public sealed class RoomTrapTests
    {
        [UnityTest]
        public IEnumerator ThreeFloorsHaveReachableTrapsAndSolidPartitions()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (var kind in new[] { "spikePlate", "raisedSpikes", "floorBomb" })
                Assert.AreEqual(new Vector2(32, 32), GameArt.Get(kind).rect.size, kind);
            for (int floor = 1; floor <= 3; floor++)
            {
                var map = run.Map;
                var traps = map.GetComponentsInChildren<RoomTrap>();
                Physics2D.SyncTransforms();
                foreach (var room in map.Rooms)
                {
                    foreach (var wall in room.Walls)
                    {
                        var tile = wall.min;
                        Assert.IsFalse(map.IsWalkable(tile));
                        Assert.IsFalse(map.IsFloorTile(tile));
                        Assert.IsNotNull(Physics2D.OverlapPoint(tile));
                    }
                    var local = traps.Where(t => map.RoomAtPosition(t.transform.position) == room).ToList();
                    var anchors = RoomInterior.TrapAnchors(room);
                    Assert.AreEqual(anchors.Count(a => !a.Bomb), local.Count(t => t.name == "SpikePlate"),
                        room.Name + " spike count follows the room schematic");
                    Assert.AreEqual(anchors.Count(a => a.Bomb), local.Count(t => t.name == "FloorBomb"),
                        room.Name + " bomb count follows the room schematic");
                    foreach (var trap in local)
                    {
                        Assert.IsTrue(map.IsWalkable(Vector2Int.RoundToInt(trap.transform.position)));
                        var tile = Vector2Int.RoundToInt(trap.transform.position);
                        var offset = tile - trap.PlannedTile;
                        Assert.LessOrEqual(Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)),
                            run.Config.trapAnchorSearchRadius, "Trap stays near its planned position");
                        foreach (var door in room.DoorOpenings)
                        {
                            int margin = run.Config.trapDoorClearance;
                            Assert.IsFalse(new RectInt(door.x - margin, door.y - margin,
                                door.width + margin * 2, door.height + margin * 2).Contains(tile),
                                "Trap must leave the doorway clear");
                        }
                    }
                    if (room.Role != RoomRole.Start)
                        Assert.IsNotEmpty(map.FindPath(map.StartPosition, room.Center));
                    foreach (var spawn in map.SpawnCells(room.Index))
                        foreach (var zone in RoomInterior.NoSpawnZones(room))
                            Assert.IsFalse(zone.Contains(Vector2Int.RoundToInt(spawn)),
                                room.Name + " keeps enemies out of its safe pocket");
                }
                if (floor == 3) break;
                run.ActivateBoss();
                run.Boss.TakeDamage(99999);
                run.AdvanceFloor();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BombCanBeEvadedAndSpikesDamageAStationaryPlayer()
        {
            yield return SceneManager.LoadSceneAsync("Prototype");
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            foreach (var enemy in run.Enemies) enemy.enabled = false;
            var traps = run.Map.GetComponentsInChildren<RoomTrap>();
            var bomb = traps.First(t => t.name == "FloorBomb");
            float health = run.Player.Health;
            run.Player.PlaceAt(bomb.transform.position);
            yield return null;
            yield return null;
            Assert.IsTrue(bomb.GetComponentInChildren<LineRenderer>().enabled);
            run.Player.PlaceAt(run.Map.StartPosition);
            yield return new WaitForSeconds(run.Config.bombFuse + .2f);
            Assert.IsFalse(bomb);
            Assert.AreEqual(health, run.Player.Health);
            var second = traps.First(t => t && t.name == "FloorBomb");
            run.Player.PlaceAt(second.transform.position);
            yield return new WaitForSeconds(run.Config.bombFuse + .2f);
            Assert.AreEqual(health - run.Config.bombDamage, run.Player.Health);
            yield return new WaitForSeconds(run.Config.playerInvulnerability);
            run.Player.PlaceAt(traps.First(t => t && t.name == "SpikePlate").transform.position);
            float timeout = Time.time + run.Config.spikeRest + run.Config.spikeWarning + run.Config.spikeActive + .3f;
            while (Time.time < timeout && run.Player.Health == health - run.Config.bombDamage) yield return null;
            Assert.AreEqual(health - run.Config.bombDamage - run.Config.spikeDamage, run.Player.Health);
        }
    }
}

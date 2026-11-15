using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Echoes.Tests
{
    public sealed class OpeningCinematicTests
    {
        [UnityTest]
        public IEnumerator TitleMenuOpensImmediatelyAndStartEntersFirstFloor()
        {
            yield return SceneManager.LoadSceneAsync("Opening");
            var timeout = Time.realtimeSinceStartup + 40;
            var opening = Object.FindObjectOfType<OpeningCinematic>();
            Assert.IsNotNull(opening);
            yield return null;
            Assert.IsNull(Object.FindObjectOfType<VideoPlayer>());
            Assert.AreEqual("Opening", SceneManager.GetActiveScene().name);
            var menu = opening.GetComponent<TitleMenu>();
            Assert.IsNotNull(menu);
            Assert.IsNotNull(menu.GetComponentInChildren<UnityEngine.UI.RawImage>().texture);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var poster = menu.GetComponentInChildren<RawImage>().rectTransform;
            var names = new[] { "开始游戏", "设置", "退出游戏" };
            var centers = new[] { 458, 557, 654 };
            var pointer = new PointerEventData(EventSystem.current);
            for (int i = 0; i < names.Length; i++)
            {
                var bounds = poster.rect;
                var local = bounds.min + Vector2.Scale(bounds.size,
                    new Vector2(200f / 1672, 1 - centers[i] / 941f));
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, poster.TransformPoint(local));
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.IsNotEmpty(hits, $"No raycast at {pointer.position}, poster {bounds}, screen {Screen.width}x{Screen.height}");
                Assert.AreEqual(names[i], hits[0].gameObject.name);
                var button = hits[0].gameObject.GetComponent<TitleMenuButton>();
                button.OnPointerEnter(pointer);
                Assert.AreEqual(button.gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.Greater(button.targetGraphic.color.a, 0);
                button.OnPointerExit(pointer);
                Assert.AreEqual(0f, button.targetGraphic.color.a);
            }
            GameObject.Find("设置").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            GameObject.Find("返回").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            GameObject.Find("开始游戏").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            ClickCard("返回标题");
            yield return null;
            ClickCard("开始游戏");
            yield return null;
            ClickCard("主角一");
            while (opening && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual("Prototype", SceneManager.GetActiveScene().name);
            yield return null;
            var run = Object.FindObjectOfType<RunController>();
            Assert.AreEqual(Hero.Explorer, run.Character);
            Assert.AreEqual(100, run.Player.MaxHealth);
        }

        private static void ClickCard(string name)
        {
            Canvas.ForceUpdateCanvases();
            var card = GameObject.Find(name).GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, card.TransformPoint(card.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.AreEqual(card.gameObject, hits[0].gameObject);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator RunnerPlaysFirstFloorAndConvertsPermanentSpeed()
        {
            float previousStep = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;
            try { yield return RunnerTrial(); }
            finally { Time.captureDeltaTime = previousStep; }
        }

        private static IEnumerator RunnerTrial()
        {
            yield return SceneManager.LoadSceneAsync("Opening");
            yield return null;
            ClickCard("开始游戏");
            yield return null;
            RuntimeSmokeTests.Capture("TestResults/runner-selection.png", Object.FindObjectOfType<TitleMenu>().GetComponentInChildren<Canvas>());
            yield return null;
            ClickCard("主角二");
            yield return new WaitUntil(() => Object.FindObjectOfType<RunController>()?.Player != null);
            var run = Object.FindObjectOfType<RunController>();
            var player = run.Player;
            Assert.AreEqual(1, run.Floor);
            Assert.AreEqual(90, player.Health);
            Assert.AreEqual(10.8f, player.MovementSpeed, .0001f);
            Assert.AreEqual(.85f, player.TotalDamageScale, .0001f);
            var art = player.GetComponent<CharacterArt>();
            var renderer = player.GetComponent<SpriteRenderer>();
            var origin = (Vector2)player.transform.position;
            var movement = typeof(PlayerController).GetField("movement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool sawStride = false;
            for (int i = 0; i < 12; i++)
            {
                movement.SetValue(player, Vector2.right);
                yield return null;
                sawStride |= art.CurrentFrame >= 4 && art.CurrentFrame < 8;
            }
            Assert.IsTrue(sawStride);
            Assert.Greater(Vector2.Distance(origin, player.transform.position), .1f);
            Assert.IsTrue(player.TryFire(player.transform.position + Vector3.right * 3, true));
            yield return null;
            Assert.That(art.CurrentFrame, Is.InRange(8, 11));
            Assert.IsTrue(player.TryThrowDecoy(player.transform.position));
            Assert.IsTrue(run.Visibility.TryLightScreen());
            yield return new WaitForSeconds(.32f);
            StringAssert.StartsWith("runnerLaugh_", renderer.sprite.name);
            RuntimeSmokeTests.Capture("TestResults/runner-cast.png");
            yield return new WaitForSeconds(2);
            Assert.That(art.CurrentFrame, Is.InRange(4, 7));
            yield return new WaitForSeconds(2);
            StringAssert.StartsWith("runner_", renderer.sprite.name);
            var lightStep = System.Array.Find(UpgradeDefinition.Catalog, u => u.Title == "轻装行进");
            lightStep.Apply(player);
            Assert.AreEqual(.9775f, player.TotalDamageScale, .0001f);
            for (int i = 1; i < 7; i++) lightStep.Apply(player);
            Assert.AreEqual(21.6f, player.MovementSpeed, .0001f);
            Assert.AreEqual(1.70f, player.TotalDamageScale, .0001f);
            var walker = run.Enemies.Find(e => !e.IsBoss && !e.IsElite && !e.IsGuard);
            var lane = (Vector2)run.Map.Rooms[0].Center - Vector2.right * 4;
            player.PlaceAt(lane);
            EnemyRoleTests.PlaceEnemy(walker, lane + Vector2.right * 3);
            float until = Time.time + 6;
            while (walker.Alive && Time.time < until)
            {
                player.TryFire(walker.transform.position, true);
                yield return null;
            }
            Assert.GreaterOrEqual(run.Kills, 1);
            Assert.IsFalse(walker.Alive);
            yield return new WaitForSeconds(run.Config.playerInvulnerability + .1f);
            player.TakeDamage(9);
            yield return null;
            Assert.That(art.CurrentFrame, Is.InRange(12, 15));
            yield return new WaitForSeconds(run.Config.playerInvulnerability + .1f);
            player.TakeDamage(200);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(RunState.Lost, run.State);
            Assert.AreEqual("runner_19", renderer.sprite.name);
            Assert.AreEqual("runner_00", GameObject.Find("RunResult/Explorer").GetComponent<Image>().sprite.name);
            run.Restart();
            yield return null;
            yield return null;
            var restarted = Object.FindObjectOfType<RunController>();
            Assert.AreEqual(Hero.Runner, restarted.Character);
            Assert.AreEqual(.85f, restarted.Player.TotalDamageScale, .0001f);
        }
    }
}

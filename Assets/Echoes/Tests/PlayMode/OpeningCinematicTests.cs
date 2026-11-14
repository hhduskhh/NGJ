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
        public IEnumerator VideoOpensTitleMenuAndStartEntersFirstFloor()
        {
            yield return SceneManager.LoadSceneAsync("Opening");
            var timeout = Time.realtimeSinceStartup + 40;
            var opening = Object.FindObjectOfType<OpeningCinematic>();
            Assert.IsNotNull(opening);
            bool sawFrame = false;
            while (!opening.GetComponent<TitleMenu>() && Time.realtimeSinceStartup < timeout)
            {
                var video = opening.GetComponent<VideoPlayer>();
                if (video && video.frame > 0 && video.texture) sawFrame = true;
                yield return null;
            }
            Assert.IsTrue(sawFrame, "Expected decoded video frames before entering the dungeon.");
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
            while (opening && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual("Prototype", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindObjectOfType<RunController>());
        }
    }
}

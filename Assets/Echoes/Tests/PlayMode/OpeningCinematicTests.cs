using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

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
            GameObject.Find("设置").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            GameObject.Find("返回").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            GameObject.Find("开始游戏").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            while (opening && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual("Prototype", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindObjectOfType<RunController>());
        }
    }
}

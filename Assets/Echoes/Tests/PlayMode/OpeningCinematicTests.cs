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
        public IEnumerator VideoDecodesAndEntersFirstFloor()
        {
            yield return SceneManager.LoadSceneAsync("Opening");
            var timeout = Time.realtimeSinceStartup + 40;
            var opening = Object.FindObjectOfType<OpeningCinematic>();
            Assert.IsNotNull(opening);
            bool sawFrame = false;
            while (opening && Time.realtimeSinceStartup < timeout)
            {
                var video = opening.GetComponent<VideoPlayer>();
                if (video && video.frame > 0 && video.texture) sawFrame = true;
                yield return null;
            }
            Assert.IsTrue(sawFrame, "Expected decoded video frames before entering the dungeon.");
            Assert.AreEqual("Prototype", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindObjectOfType<RunController>());
        }
    }
}

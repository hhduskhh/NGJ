using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Echoes
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class OpeningCinematic : MonoBehaviour
    {
        private VideoPlayer player;
        private float prepareDeadline;
        private bool leaving;
        private bool menuShown;

        private void Start()
        {
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = Path.Combine(Application.streamingAssetsPath, "EchoesBelow-Opening.mp4");
            player.renderMode = VideoRenderMode.APIOnly;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.waitForFirstFrame = true;
            player.loopPointReached += OnEnd;
            player.errorReceived += OnError;
            prepareDeadline = Time.realtimeSinceStartup + 8f;
            player.Play();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint && player && player.texture)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), player.texture,
                    ScaleMode.ScaleToFit, false);
        }

        private void Update()
        {
            if (menuShown) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) ||
                (player && !player.isPrepared && Time.realtimeSinceStartup > prepareDeadline))
                ShowMenu();
        }

        private void OnEnd(VideoPlayer source) => ShowMenu();

        private void OnError(VideoPlayer source, string message)
        {
            Debug.LogWarning("Opening cinematic: " + message);
            ShowMenu();
        }

        private void ShowMenu()
        {
            if (menuShown) return;
            menuShown = true;
            player.Stop();
            gameObject.AddComponent<TitleMenu>().Initialize(EnterDungeon);
        }

        private void EnterDungeon()
        {
            if (leaving) return;
            leaving = true;
            if (player) player.Stop();
            SceneManager.LoadSceneAsync("Prototype");
        }

        private void OnDestroy()
        {
            if (!player) return;
            player.loopPointReached -= OnEnd;
            player.errorReceived -= OnError;
        }
    }
}

using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private VideoPlayer endingPlayer;
        private UnityEngine.UI.RawImage endingImage;
        private AudioSource pausedMusic;
        private bool endingDone;
        private long endingFrame;
        private float endingDeadline;

        private void BeginEnding()
        {
            modalBackdrop.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            var screen = new GameObject("EndingFilm", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            screen.transform.SetParent(modalBackdrop, false);
            endingImage = screen.GetComponent<UnityEngine.UI.RawImage>();
            endingImage.color = Color.black;
            endingImage.raycastTarget = false;
            var fit = screen.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9f;

            foreach (var source in run.GetComponents<AudioSource>())
            {
                if (!source.loop || !source.isPlaying) continue;
                pausedMusic = source;
                pausedMusic.Pause();
                break;
            }
            var audio = screen.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            endingPlayer = screen.AddComponent<VideoPlayer>();
            endingPlayer.playOnAwake = false;
            endingPlayer.renderMode = VideoRenderMode.APIOnly;
            endingPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            endingPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            endingPlayer.SetTargetAudioSource(0, audio);
            endingPlayer.source = VideoSource.Url;
            endingPlayer.url = Path.Combine(Application.streamingAssetsPath, "EchoesBelow-Ending.mp4");
            endingPlayer.loopPointReached += OnEndingComplete;
            endingPlayer.errorReceived += OnEndingError;
            endingDone = false;
            endingFrame = -1;
            endingDeadline = Time.realtimeSinceStartup + 8f;
            endingPlayer.Play();
        }

        private bool EndingFinished()
        {
            if (endingPlayer.texture && endingPlayer.frame != endingFrame)
            {
                endingImage.texture = endingPlayer.texture;
                endingImage.color = Color.white;
                endingFrame = endingPlayer.frame;
                endingDeadline = Time.realtimeSinceStartup + 8f;
            }
            return endingDone || Time.realtimeSinceStartup >= endingDeadline;
        }

        private void OnEndingComplete(VideoPlayer source) => endingDone = true;

        private void OnEndingError(VideoPlayer source, string message)
        {
            Debug.LogWarning("Ending cinematic: " + message);
            endingDone = true;
        }

        private void CloseEnding()
        {
            if (!endingPlayer) return;
            endingPlayer.loopPointReached -= OnEndingComplete;
            endingPlayer.errorReceived -= OnEndingError;
            endingPlayer.Stop();
            endingImage.gameObject.SetActive(false);
            Destroy(endingImage.gameObject);
            endingPlayer = null;
            endingImage = null;
            if (pausedMusic) pausedMusic.UnPause();
            pausedMusic = null;
        }
    }
}

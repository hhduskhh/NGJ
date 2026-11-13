using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    // The darkness shader and targeting share this list so a hidden enemy cannot be auto-targeted.
    public sealed class DungeonVisibility : MonoBehaviour
    {
        private const int MaxEchoes = 32;
        private struct Echo
        {
            public Vector2 Origin;
            public float Range, StartedAt;
        }

        private RunController run;
        private Camera viewCamera;
        private Material darkness;
        private Transform cover;
        private readonly List<Echo> echoes = new List<Echo>();
        private readonly Vector4[] shaderEchoes = new Vector4[MaxEchoes];

        public void Initialize(RunController controller)
        {
            run = controller;
            viewCamera = Camera.main;
            darkness = new Material(Resources.Load<Shader>("DungeonDarkness"));
            var renderer = PixelArt.Create("UnseenDungeon", "solid", Vector2.zero, 40, transform);
            renderer.sharedMaterial = darkness;
            cover = renderer.transform;
            RefreshMask();
        }

        public void Reveal(Vector2 origin, float range)
        {
            RemoveExpiredEchoes();
            if (echoes.Count == MaxEchoes) echoes.RemoveAt(0);
            echoes.Add(new Echo { Origin = origin, Range = range, StartedAt = Time.time });
        }

        public bool IsVisible(Vector2 position)
        {
            if (((Vector2)run.Player.transform.position - position).sqrMagnitude <=
                run.Config.playerVisionRadius * run.Config.playerVisionRadius) return true;
            foreach (var echo in echoes)
                if (!Expired(echo) && (echo.Origin - position).sqrMagnitude <= Radius(echo) * Radius(echo)) return true;
            return false;
        }

        private float Radius(Echo echo) => Mathf.Min(echo.Range,
            Mathf.Max(0, Time.time - echo.StartedAt) * run.Config.waveSpeed);

        private bool Expired(Echo echo) => Time.time - echo.StartedAt >
            echo.Range / run.Config.waveSpeed + run.Config.echoRevealDuration;

        private void RemoveExpiredEchoes()
        {
            for (int i = echoes.Count - 1; i >= 0; i--)
                if (Expired(echoes[i])) echoes.RemoveAt(i);
        }

        private void LateUpdate() => RefreshMask();

        public void RefreshMask()
        {
            RemoveExpiredEchoes();
            Vector2 player = run.Player.transform.position;
            darkness.SetVector("_PlayerVision", new Vector4(player.x, player.y, run.Config.playerVisionRadius, 0));
            for (int i = 0; i < echoes.Count; i++)
                shaderEchoes[i] = new Vector4(echoes[i].Origin.x, echoes[i].Origin.y, Radius(echoes[i]), 0);
            darkness.SetVectorArray("_Echoes", shaderEchoes);
            darkness.SetInt("_EchoCount", echoes.Count);
            cover.position = new Vector3(viewCamera.transform.position.x, viewCamera.transform.position.y, 0);
            // PixelPerfectCamera can adjust the projection immediately before rendering.
            // Cover the whole floor as well as the current viewport to prevent exposed edge strips.
            cover.localScale = new Vector3(Mathf.Max(DungeonMap.Width * 2, viewCamera.orthographicSize * viewCamera.aspect * 2 + 2),
                Mathf.Max(DungeonMap.Height * 2, viewCamera.orthographicSize * 2 + 2), 1);
        }

        private void OnDestroy()
        {
            if (darkness) Destroy(darkness);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    // The darkness shader and targeting share this list so a hidden enemy cannot be auto-targeted.
    public sealed class DungeonVisibility : MonoBehaviour
    {
        private const int MaxEchoes = 32;
        private RunController run;
        private Camera viewCamera;
        private Material darkness;
        private Texture2D walkableTexture;
        private Transform cover;
        private readonly List<VisionPulse> echoes = new List<VisionPulse>();
        private readonly Vector4[] shaderEchoes = new Vector4[MaxEchoes];
        private Vector2 lastWalkPosition;
        private float walkingTime, nextWalkPulse;
        public IReadOnlyList<VisionPulse> Pulses => echoes;

        public void Initialize(RunController controller)
        {
            run = controller;
            viewCamera = Camera.main;
            darkness = new Material(Resources.Load<Shader>("DungeonDarkness"));
            CreateObstacleMask();
            var renderer = PixelArt.Create("UnseenDungeon", "solid", Vector2.zero, 40, transform);
            renderer.sharedMaterial = darkness;
            cover = renderer.transform;
            lastWalkPosition = run.Player.GetComponent<Rigidbody2D>().position;
            nextWalkPulse = run.Config.walkFirstPulseDelay;
            RefreshMask();
        }

        public void Reveal(Vector2 origin, float range, Color? tint = null)
        {
            RemoveExpiredEchoes();
            if (echoes.Count == MaxEchoes) RemoveEcho(0);
            var pulse = new VisionPulse(origin, range, Time.time);
            Color color = tint ?? PixelArt.Cyan;
            color.a = run.Config.visionRingAlpha;
            pulse.Outline = PixelArt.Circle("VisionPulse", transform, color, .02f);
            pulse.Outline.transform.position = origin;
            // Let the same darkness mask clip the ring behind walls.
            pulse.Outline.sortingOrder = 35;
            PixelArt.SetRadius(pulse.Outline, 0);
            echoes.Add(pulse);
        }

        // Called at the physics cadence: holding a movement key against a wall is not walking.
        public void UpdateWalking(Vector2 position, float deltaTime)
        {
            bool moved = (position - lastWalkPosition).sqrMagnitude > .000001f;
            lastWalkPosition = position;
            if (run.State != RunState.Playing) return;
            if (!moved)
            {
                walkingTime = 0;
                nextWalkPulse = run.Config.walkFirstPulseDelay;
                return;
            }
            walkingTime += deltaTime;
            if (walkingTime + .00001f < nextWalkPulse) return;
            Reveal(position, run.Config.playerVisionRadius);
            nextWalkPulse += run.Config.walkPulseInterval;
        }

        public bool IsVisible(Vector2 position) => VisibilityAt(position) > .001f;

        public float VisibilityAt(Vector2 position)
        {
            float visible = CoverageFrom(position, run.Player.transform.position, 0);
            foreach (var echo in echoes)
            {
                if (visible >= 1) break;
                if (!Expired(echo)) visible = Mathf.Max(visible, CoverageFrom(position,
                    echo.Origin, echo.RadiusAt(Time.time, run.Config)));
            }
            return visible;
        }

        private float CoverageFrom(Vector2 position, Vector2 origin, float radius)
        {
            float coverage = VisionPulse.Coverage(position, origin, radius, run.Config.gradientVisionRange);
            return coverage > 0 && run.Map.HasLineOfSight(origin, position, true) ? coverage : 0;
        }

        private void CreateObstacleMask()
        {
            // The floor is static. Upload its collision grid once, with one texel per cell.
            walkableTexture = new Texture2D(DungeonMap.Width, DungeonMap.Height, TextureFormat.RGBA32, false, true)
                { name = "VisionWalkableCells", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[DungeonMap.Width * DungeonMap.Height];
            for (int y = 0; y < DungeonMap.Height; y++)
                for (int x = 0; x < DungeonMap.Width; x++)
                    pixels[y * DungeonMap.Width + x] = new Color32(
                        run.Map.IsWalkable(new Vector2Int(x, y)) ? (byte)255 : (byte)0, 0, 0, 255);
            walkableTexture.SetPixels32(pixels);
            walkableTexture.Apply(false, true);
            darkness.SetTexture("_Walkable", walkableTexture);
            darkness.SetVector("_MapSize", new Vector4(DungeonMap.Width, DungeonMap.Height,
                1f / DungeonMap.Width, 1f / DungeonMap.Height));
        }

        private bool Expired(VisionPulse echo) => Time.time - echo.StartedAt >= run.Config.VisionPulseDuration;

        private void RemoveEcho(int index)
        {
            Destroy(echoes[index].Outline.gameObject);
            echoes.RemoveAt(index);
        }

        private void RemoveExpiredEchoes()
        {
            for (int i = echoes.Count - 1; i >= 0; i--)
                if (Expired(echoes[i])) RemoveEcho(i);
        }

        private void LateUpdate() => RefreshMask();

        public void RefreshMask()
        {
            RemoveExpiredEchoes();
            Vector2 player = run.Player.transform.position;
            darkness.SetVector("_PlayerVision", new Vector4(player.x, player.y, 0, run.Config.gradientVisionRange));
            for (int i = 0; i < echoes.Count; i++)
            {
                var pulse = echoes[i];
                float radius = pulse.RadiusAt(Time.time, run.Config);
                shaderEchoes[i] = new Vector4(pulse.Origin.x, pulse.Origin.y, radius, run.Config.gradientVisionRange);
                PixelArt.SetRadius(pulse.Outline, radius);
            }
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
            if (walkableTexture) Destroy(walkableTexture);
        }
    }
}

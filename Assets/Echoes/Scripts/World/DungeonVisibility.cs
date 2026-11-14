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
        private RenderTexture propSurfaces;
        private Transform cover;
        private float screenLightEndsAt, screenLightReadyAt;
        private float screenLightStartedAt;
        public bool ScreenLightActive => Time.time < screenLightEndsAt;
        public float ScreenLightCooldownRemaining => Mathf.Max(0, screenLightReadyAt - Time.time);

        public bool TryLightScreen()
        {
            if (run.State != RunState.Playing || ScreenLightCooldownRemaining > 0) return false;
            screenLightStartedAt = Time.time;
            screenLightEndsAt = screenLightStartedAt + run.Config.screenLightDuration;
            screenLightReadyAt = Time.time + run.Config.screenLightCooldown;
            run.Noise.PlayScreenLight();
            Vector2 source = run.Player.transform.position;
            foreach (var enemy in run.Enemies)
            {
                if (enemy && enemy.State == EnemyState.Sleeping && InCamera(enemy.transform.position))
                    enemy.Hear(source);
            }
            return true;
        }

        private readonly List<VisionPulse> echoes = new List<VisionPulse>();
        private readonly Vector4[] shaderEchoes = new Vector4[MaxEchoes];
        public sealed class ShotTrail
        {
            public Vector2 Origin, Direction, End;
            public float Speed, LastMove;
            public void Advance(Vector2 end) { End = end; LastMove = Time.time; }
            public Vector2 Tail(float duration) => Vector2.MoveTowards(End, Origin,
                Speed * Mathf.Max(0, duration - (Time.time - LastMove)));
        }

        private readonly List<ShotTrail> trails = new List<ShotTrail>();
        private readonly Vector4[] shaderTrails = new Vector4[32];
        public ShotTrail BeginTrail(Vector2 origin, Vector2 direction, float speed)
        {
            var trail = new ShotTrail { Origin = origin, Direction = direction, Speed = speed };
            trail.Advance(origin);
            if (trails.Count == shaderTrails.Length) trails.RemoveAt(0);
            trails.Add(trail);
            return trail;
        }

        private Vector2 lastWalkPosition;
        private float walkingTime, nextWalkPulse;
        public IReadOnlyList<VisionPulse> Pulses => echoes;
        public const int DangerOrder = 41; // Immediately above the darkness cover (40).

        public bool InCamera(Vector2 point)
        {
            var p = viewCamera.WorldToViewportPoint(point);
            return p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
        }

        public bool CanSeeDanger(Vector2 point) => InCamera(point) &&
            (ScreenLightActive || run.Map.HasLineOfSight(run.Player.transform.position, point));

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

        // Called after a floor transition: the layout and its walkable texture are rebuilt.
        public void Rebuild()
        {
            screenLightEndsAt = 0;
            ClearPulses();
            trails.Clear();
            CreateObstacleMask();
            lastWalkPosition = run.Player.GetComponent<Rigidbody2D>().position;
            nextWalkPulse = run.Config.walkFirstPulseDelay;
            RefreshMask();
        }

        public void ClearPulses()
        {
            for (int i = echoes.Count - 1; i >= 0; i--) RemoveEcho(i);
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
            run.Noise.Emit(position, run.Config.playerVisionRadius, NoiseKind.Footstep);
            nextWalkPulse += run.Config.walkPulseInterval;
        }

        public bool IsVisible(Vector2 position) => VisibilityAt(position) > .001f;

        public float VisibilityAt(Vector2 position)
        {
            if (ScreenLightActive && InCamera(position)) return 1;
            float visible = CoverageFrom(position, run.Player.transform.position, 0);
            foreach (var echo in echoes)
            {
                if (visible >= 1) break;
                if (!Expired(echo)) visible = Mathf.Max(visible, CoverageFrom(position,
                    echo.Origin, echo.RadiusAt(Time.time, run.Config)));
            }
            foreach (var trail in trails)
            {
                if (Time.time - trail.LastMove >= run.Config.bulletTrailSeconds) continue;
                Vector2 tail = trail.Tail(run.Config.bulletTrailSeconds);
                float along = Mathf.Clamp(Vector2.Dot(position - tail, trail.Direction), 0, Vector2.Distance(tail, trail.End));
                Vector2 closest = tail + trail.Direction * along;
                float light = VisionPulse.Coverage(position, closest, 0, run.Config.bulletTrailRadius);
                if (light > visible && run.Map.HasLineOfSight(closest, position, true)) visible = light;
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
            // The floor is static per floor. Upload its collision grid with one texel per cell.
            int width = run.Map.MapWidth;
            int height = run.Map.MapHeight;
            if (walkableTexture) Destroy(walkableTexture);
            walkableTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { name = "VisionWalkableCells", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(
                        run.Map.IsFloorTile(new Vector2Int(x, y)) ? (byte)255 : (byte)0, 0, 0, 255);
            walkableTexture.SetPixels32(pixels);
            walkableTexture.Apply(false, true);
            darkness.SetTexture("_Walkable", walkableTexture);
            darkness.SetVector("_MapSize", new Vector4(width, height, 1f / width, 1f / height));
            if (propSurfaces) Destroy(propSurfaces);
            propSurfaces = PropSurfaceMap.Create(run.Map);
            darkness.SetTexture("_PropSurfaces", propSurfaces);
            darkness.SetInt("_UsePropSurfaces", 1);
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
            // During the opening sweep the cover draws the wave instead of darkness.
            float waveProgress = ScreenLightActive
                ? Mathf.Clamp01((Time.time - screenLightStartedAt) / Mathf.Max(.05f, run.Config.screenLightWaveDuration))
                : -1;
            darkness.SetFloat("_ScreenLightWave", waveProgress);
            cover.gameObject.SetActive(waveProgress < 1);
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
            int count = 0;
            float duration = run.Config.bulletTrailSeconds;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                var trail = trails[i];
                if (Time.time - trail.LastMove >= duration) { trails.RemoveAt(i); continue; }
                Vector2 tail = trail.Tail(duration);
                shaderTrails[count++] = new Vector4(tail.x, tail.y, trail.End.x, trail.End.y);
            }
            darkness.SetVectorArray("_Trails", shaderTrails);
            darkness.SetInt("_TrailCount", count);
            darkness.SetFloat("_TrailRadius", run.Config.bulletTrailRadius);
            darkness.SetVectorArray("_Echoes", shaderEchoes);
            darkness.SetInt("_EchoCount", echoes.Count);
            cover.position = new Vector3(viewCamera.transform.position.x, viewCamera.transform.position.y, 0);
            // PixelPerfectCamera can adjust the projection immediately before rendering.
            // Cover the whole floor as well as the current viewport to prevent exposed edge strips.
            cover.localScale = new Vector3(Mathf.Max(run.Map.MapWidth * 2, viewCamera.orthographicSize * viewCamera.aspect * 2 + 2),
                Mathf.Max(run.Map.MapHeight * 2, viewCamera.orthographicSize * 2 + 2), 1);
        }

        private void OnDestroy()
        {
            if (darkness) Destroy(darkness);
            if (walkableTexture) Destroy(walkableTexture);
            if (propSurfaces) Destroy(propSurfaces);
        }
    }
}

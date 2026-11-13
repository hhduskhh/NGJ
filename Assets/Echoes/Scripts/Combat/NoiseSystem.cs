using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public enum NoiseKind { Gunshot, Decoy }

    public sealed class NoiseSystem : MonoBehaviour
    {
        private RunController run;
        private AudioSource audioSource;
        private AudioClip shot, bell;
        public void Initialize(RunController controller)
        {
            run = controller;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = .12f;
            shot = Tone("ShotPlaceholder", 100, .06f);
            bell = Tone("BellPlaceholder", 640, .2f);
        }
        private static AudioClip Tone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            var samples = new float[(int)(sampleRate * duration)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(i * frequency * 2 * Mathf.PI / sampleRate) * (1 - (float)i / samples.Length);
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0); return clip;
        }
        public void PlayShot() => audioSource.PlayOneShot(shot);
        public void Emit(Vector2 origin, float radius, NoiseKind kind)
        {
            var go = new GameObject(kind + "SoundWave");
            go.transform.position = origin;
            go.AddComponent<NoiseWave>().Initialize(run, radius, kind);
            if (kind == NoiseKind.Decoy) audioSource.PlayOneShot(bell);
        }
    }

    public sealed class NoiseWave : MonoBehaviour
    {
        private RunController run;
        private float maxRadius, radius, age;
        private Color color;
        private LineRenderer wave, boundary;
        private readonly HashSet<EnemyController> heard = new HashSet<EnemyController>();

        public void Initialize(RunController controller, float range, NoiseKind kind)
        {
            run = controller; maxRadius = range;
            color = kind == NoiseKind.Gunshot ? PixelArt.Gold : PixelArt.Cyan;
            wave = PixelArt.Circle("ExpandingFront", transform, color);
            boundary = PixelArt.Circle("ActualMaximumRange", transform, color * new Color(1, 1, 1, .18f), .025f);
            PixelArt.SetRadius(boundary, range);
            PixelArt.SetRadius(wave, 0);
        }
        private void Update()
        {
            if (run.State != RunState.Playing) return;
            age += Time.deltaTime;
            radius = Mathf.Min(maxRadius, age * run.Config.waveSpeed);
            PixelArt.SetRadius(wave, radius);
            foreach (var enemy in run.Enemies)
                if (enemy && enemy.Alive && !heard.Contains(enemy) &&
                    Vector2.Distance(enemy.transform.position, transform.position) <= radius)
                { heard.Add(enemy); enemy.Hear(transform.position); }
            float duration = maxRadius / run.Config.waveSpeed + .16f;
            var fading = color; fading.a = Mathf.Clamp01((duration - age) / .2f) * .55f;
            wave.startColor = wave.endColor = fading;
            if (age >= duration) Destroy(gameObject);
        }
    }
}

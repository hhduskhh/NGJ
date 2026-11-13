using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public enum NoiseKind { Gunshot, Decoy, Footstep }

    public sealed class NoiseSystem : MonoBehaviour
    {
        private RunController run;
        private AudioSource audioSource;
        private AudioClip shot, bell, footstep;
        public void Initialize(RunController controller)
        {
            run = controller;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = .12f;
            shot = Tone("ShotPlaceholder", 100, .06f);
            bell = Tone("BellPlaceholder", 640, .2f);
            footstep = Tone("FootstepPlaceholder", 75, .045f);
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
            run.Visibility.Reveal(origin, radius, kind == NoiseKind.Gunshot ? PixelArt.Gold : PixelArt.Cyan);
            var go = new GameObject(kind + "SoundWave");
            go.transform.position = origin;
            go.AddComponent<NoiseWave>().Initialize(run, radius);
            if (kind == NoiseKind.Decoy) audioSource.PlayOneShot(bell);
            else if (kind == NoiseKind.Footstep) audioSource.PlayOneShot(footstep, .4f);
        }
    }

    public sealed class NoiseWave : MonoBehaviour
    {
        private RunController run;
        private float maxRadius, radius, age;
        private readonly HashSet<EnemyController> heard = new HashSet<EnemyController>();

        public void Initialize(RunController controller, float range)
        {
            run = controller; maxRadius = range;
        }
        private void Update()
        {
            if (run.State != RunState.Playing) return;
            age += Time.deltaTime;
            // Match the visible pulse during expansion; its gradient does not wake enemies.
            radius = VisionPulse.EvaluateRadius(Mathf.Min(age, run.Config.visionExpandDuration),
                maxRadius, run.Config.visionExpandDuration, run.Config.visionHoldDuration, run.Config.visionShrinkDuration);
            foreach (var enemy in run.Enemies)
                if (enemy && enemy.Alive && !heard.Contains(enemy) &&
                    Vector2.Distance(enemy.transform.position, transform.position) <= radius)
                { heard.Add(enemy); enemy.Hear(transform.position); }
            float duration = run.Config.visionExpandDuration + .16f;
            if (age >= duration) Destroy(gameObject);
        }
    }
}

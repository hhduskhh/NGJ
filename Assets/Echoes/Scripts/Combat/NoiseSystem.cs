using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public enum NoiseKind { Gunshot, Decoy, Footstep }

    public sealed class NoiseSystem : MonoBehaviour
    {
        private RunController run;
        private AudioSource audioSource;
        private AudioClip bell, footstep, click, coin;
        private AudioClip pickup, hit, wake, enemyStep, guardReady, ambience;
        private readonly AudioClip[] shots = new AudioClip[3];
        private readonly AudioSource[] shotVoices = new AudioSource[3];
        private readonly float[] shotLevels = new float[3];
        private readonly AudioSource[] worldVoices = new AudioSource[12];
        private AudioSource pickupVoice, hurtVoice, bossVoice;
        private int pickupFrame = -1, pickupIndex;
        private float nextAmbience, nextWake, duckUntil;
        private readonly Dictionary<EnemyController, bool> impacts = new Dictionary<EnemyController, bool>();
        public void Initialize(RunController controller)
        {
            run = controller;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = .12f;
            click = Tone("UI press", 180, .025f);
            coin = Tone("UI coin", 1240, .12f);
            shots[0] = Load("Pistol");
            shots[1] = Load("Shotgun");
            shots[2] = Load("Bow");
            pickup = Load("Pickup");
            hit = Load("Hit");
            wake = Load("Wake");
            enemyStep = Load("EnemyStep");
            guardReady = Load("GuardReady");
            ambience = Load("Ambience");
            for (int i = 0; i < worldVoices.Length; i++) worldVoices[i] = Voice();
            for (int i = 0; i < shots.Length; i++) shotVoices[i] = Voice();
            pickupVoice = Voice();
            hurtVoice = Voice();
            bossVoice = Voice();
            bossVoice.clip = Load("BossReady");
            nextAmbience = Time.time + Random.Range(8f, 16f);
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
        public void PlayUiPress() => audioSource.PlayOneShot(click, .36f);
        public void PlayPurchase() => audioSource.PlayOneShot(coin, .65f);
        private void OnDestroy()
        {
            Destroy(click); Destroy(coin);
            Destroy(bell); Destroy(footstep);
        }
        private static AudioClip Load(string name) => Resources.Load<AudioClip>("CombatAudio/" + name);

        private AudioSource Voice()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            return voice;
        }

        public void PlayShot(int weaponMask = 1)
        {
            int count = 0;
            for (int i = 0; i < shots.Length; i++)
                if ((weaponMask & (1 << i)) != 0) count++;
            for (int i = 0; i < shots.Length; i++)
            {
                if ((weaponMask & (1 << i)) == 0) continue;
                var voice = shotVoices[i];
                shotLevels[i] = run.Config.weaponVolume / Mathf.Max(1, count);
                voice.volume = shotLevels[i];
                if (Time.time < duckUntil) voice.volume *= .5f;
                voice.clip = shots[i];
                voice.Play();
            }
        }

        public void PlayPickup()
        {
            if (pickupFrame == Time.frameCount) return;
            pickupFrame = Time.frameCount;
            pickupVoice.pitch = .96f + .04f * (pickupIndex++ % 3);
            pickupVoice.PlayOneShot(pickup, run.Config.pickupVolume);
        }

        public void PlayHurt()
        {
            duckUntil = Time.time + .14f;
            for (int i = 0; i < shotVoices.Length; i++) shotVoices[i].volume = shotLevels[i] * .5f;
            hurtVoice.pitch = .65f;
            hurtVoice.PlayOneShot(hit, run.Config.hitVolume * 2f);
        }

        public void PlayWake(Vector2 position)
        {
            if (Time.time >= nextWake && PlayWorld(wake, position, run.Config.wakeVolume))
                nextWake = Time.time + .16f;
        }

        public void PlayGuardReady(Vector2 position) => PlayWorld(guardReady, position, run.Config.warningVolume);

        // A moving crowd shares three voices so it cannot crowd out combat warnings.
        public void PlayEnemyStep(Vector2 position) =>
            PlayWorld(enemyStep, position, run.Config.enemyStepVolume, firstVoice: worldVoices.Length - 3);

        public void PlayBossReady(float duration)
        {
            bossVoice.pitch = bossVoice.clip.length / duration;
            bossVoice.volume = run.Config.warningVolume;
            bossVoice.Play();
        }
        public void StopBossReady() => bossVoice.Stop();
        public void PauseBossReady(bool pause)
        {
            if (pause) bossVoice.Pause();
            else bossVoice.UnPause();
        }

        private bool PlayWorld(AudioClip clip, Vector2 position, float volume, float pitch = 1, int firstVoice = 0)
        {
            Vector2 offset = position - (Vector2)run.Player.transform.position;
            float attenuation = Mathf.Clamp01(1 - offset.magnitude / 22f);
            if (attenuation <= 0) return false;
            for (int i = firstVoice; i < worldVoices.Length; i++)
            {
                var voice = worldVoices[i];
                if (voice.isPlaying) continue;
                voice.panStereo = Mathf.Clamp(offset.x / 8f, -.85f, .85f);
                voice.volume = volume * attenuation;
                voice.pitch = pitch;
                voice.clip = clip;
                voice.Play();
                return true;
            }
            return false;
        }

        public void QueueImpact(EnemyController enemy, bool lethal)
        {
            impacts[enemy] = lethal;
        }

        private void LateUpdate()
        {
            if (duckUntil > 0 && Time.time >= duckUntil)
            {
                for (int i = 0; i < shotVoices.Length; i++) shotVoices[i].volume = shotLevels[i];
                duckUntil = 0;
            }
            EnemyController closestKill = null;
            float closestDistance = float.PositiveInfinity;
            foreach (var impact in impacts)
            {
                var enemy = impact.Key;
                if (!enemy) continue;
                if (!impact.Value)
                    PlayWorld(hit, enemy.transform.position, run.Config.hitVolume);
                else
                {
                    float distance = (enemy.transform.position - run.Player.transform.position).sqrMagnitude;
                    if (distance < closestDistance) { closestDistance = distance; closestKill = enemy; }
                }
            }
            impacts.Clear();
            if (closestKill)
                PlayWorld(hit, closestKill.transform.position, run.Config.hitVolume * 1.45f, .78f);
            if (run.State == RunState.Playing && Time.time >= nextAmbience)
            {
                nextAmbience = Time.time + Random.Range(8f, 16f);
                audioSource.PlayOneShot(ambience, .18f);
            }
        }
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

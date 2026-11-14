using UnityEngine;

namespace Echoes
{
    public sealed class BombExplosion : MonoBehaviour
    {
        private SpriteRenderer flash;
        private LineRenderer wave;
        private float radius, duration, age;

        public static void Play(Vector2 position, Transform parent, float radius, float seconds)
        {
            var root = new GameObject("BombExplosion").transform;
            root.SetParent(parent, false);
            root.position = position;
            var fx = root.gameObject.AddComponent<BombExplosion>();
            fx.radius = radius;
            fx.duration = Mathf.Max(.05f, seconds);
            fx.flash = PixelArt.Create("Flash", "guardMuzzle", position, 33, root);
            fx.wave = PixelArt.Circle("BlastWave", root, PixelArt.Gold, .12f);
            fx.wave.sortingOrder = 34;
            fx.Draw(0);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= duration) Destroy(gameObject);
            else Draw(age / duration);
        }

        private void Draw(float t)
        {
            float spread = 1 - (1 - t) * (1 - t);
            float fade = 1 - t;
            var color = Color.Lerp(PixelArt.Gold, PixelArt.Red, t);
            color.a = fade;
            wave.startColor = wave.endColor = color;
            wave.startWidth = wave.endWidth = Mathf.Lerp(.16f, .025f, t);
            wave.transform.localScale = Vector3.one * (radius * Mathf.Lerp(.12f, 1, spread));
            flash.sprite = GameArt.Frame("Common/effects", 4 + Mathf.Min(3, (int)(t * 4)));
            flash.transform.localScale = Vector3.one * (radius * Mathf.Lerp(.65f, 1.4f, spread));
            flash.color = Color.Lerp(Color.white, color, spread);
        }
    }
}

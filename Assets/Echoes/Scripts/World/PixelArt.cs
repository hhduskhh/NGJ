using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    // Small code-generated placeholder sprites. Replace these independently of gameplay later.
    public static class PixelArt
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static readonly Color Cyan = new Color32(92, 226, 220, 255);
        public static readonly Color Gold = new Color32(250, 194, 96, 255);
        public static readonly Color Red = new Color32(245, 109, 111, 255);
        private static Material lineMaterial;
        public static Material LineMaterial => lineMaterial ? lineMaterial :
            (lineMaterial = Resources.Load<Material>("EchoesSprite"));

        public static Sprite Get(string kind)
        {
            if (sprites.TryGetValue(kind, out var sprite) && sprite) return sprite;
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = kind };
            var pixels = new Color[256];
            Color dark = new Color32(24, 32, 47, 255), stone = new Color32(48, 59, 73, 255);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                Color c = Color.clear;
                if (kind == "floor" || kind == "floorAlt")
                {
                    c = kind == "floor" ? new Color32(31, 40, 54, 255) : new Color32(34, 43, 57, 255);
                    if (x == 0 || y == 0) c = new Color32(26, 34, 47, 255);
                    if ((x == 5 && y == 11) || (x == 12 && y == 4)) c *= 1.18f;
                }
                else if (kind == "wall")
                {
                    c = stone;
                    if (y > 11) c = new Color32(73, 86, 99, 255);
                    if (y < 3 || x == 0 || (y == 9 && x < 14)) c = dark;
                    if (x == 2 && y > 3 && y < 9) c = new Color32(59, 71, 85, 255);
                }
                else if (kind == "player")
                {
                    if (x >= 4 && x <= 11 && y >= 3 && y <= 10) c = Cyan;
                    if (x >= 3 && x <= 12 && y >= 9 && y <= 13) c = new Color32(212, 232, 220, 255);
                    if (x >= 6 && x <= 11 && y >= 10 && y <= 11) c = dark;
                    if (y < 4 && y > 0 && (x == 5 || x == 6 || x == 10 || x == 11)) c = stone;
                    if ((x == 3 || x == 12) && y >= 5 && y <= 8) c = Gold;
                }
                else if (kind == "enemy" || kind == "boss")
                {
                    if (x >= 3 && x <= 12 && y >= 3 && y <= 11) c = kind == "boss" ? Red : new Color32(166, 124, 167, 255);
                    if (y >= 11 && y <= 14 && (x == 3 || x == 4 || x == 11 || x == 12)) c = stone * 1.8f;
                    if (y == 8 && (x == 5 || x == 6 || x == 10 || x == 11)) c = Gold;
                    if (y == 4 && x >= 6 && x <= 9) c = dark;
                    if (y == 2 && (x == 4 || x == 11)) c = stone;
                }
                else if (kind == "gun")
                {
                    if (y >= 6 && y <= 9 && x >= 2 && x <= 14) c = new Color32(206, 216, 210, 255);
                    if (x >= 3 && x <= 5 && y >= 3 && y <= 6) c = Gold;
                    if (x >= 12 && y >= 6 && y <= 9) c = dark;
                }
                else if (kind == "bell")
                {
                    if (y >= 3 && y <= 10 && x >= 4 && x <= 11) c = Gold;
                    if (y >= 3 && y <= 5 && x >= 2 && x <= 13) c = Gold;
                    if (y == 11 && x >= 6 && x <= 9) c = Cyan;
                }
                else if (kind == "altar")
                {
                    if (x >= 1 && x <= 14 && y >= 1 && y <= 14) c = stone;
                    if ((x == 3 || x == 12 || y == 3 || y == 12) && x > 2 && x < 13 && y > 2 && y < 13) c = Gold;
                    if (x >= 6 && x <= 9 && y >= 6 && y <= 9) c = Cyan;
                }
                else c = Color.white;
                pixels[y * 16 + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
            sprites[kind] = sprite;
            return sprite;
        }

        public static SpriteRenderer Create(string name, string sprite, Vector2 position,
            int order, Transform parent = null, float scale = 1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Get(sprite);
            renderer.sharedMaterial = LineMaterial;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static LineRenderer Circle(string name, Transform parent, Color color, float width = .045f)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = LineMaterial;
            line.useWorldSpace = false;
            line.loop = true; line.positionCount = 80;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = 30;
            SetRadius(line, 1);
            return line;
        }

        public static void SetRadius(LineRenderer line, float radius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
        }
    }
}

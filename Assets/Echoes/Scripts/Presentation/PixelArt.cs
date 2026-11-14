using UnityEngine;

namespace Echoes
{
    // Shared imported sprite creation and exact gameplay-range line rendering.
    public static class PixelArt
    {
        public static readonly Color Cyan = new Color32(92, 226, 220, 255);
        public static readonly Color Gold = new Color32(250, 194, 96, 255);
        public static readonly Color Red = new Color32(245, 109, 111, 255);
        private static Material lineMaterial;
        public static Material LineMaterial => lineMaterial ? lineMaterial :
            (lineMaterial = Resources.Load<Material>("EchoesSprite"));

        public static Sprite Get(string kind) => GameArt.Get(kind);

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
            renderer.sortingLayerName = "Default";
            go.AddComponent<ArtBinding>().Kind = sprite;
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

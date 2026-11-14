using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Echoes.Editor
{
    // Deterministic technical export: source alpha crop -> native canvas ->
    // shared material palette. Source illustrations remain unmodified.
    public static class RoomArtPreparation
    {
        public const string SourceRoot = "美术/房间资源-第一层-20261114";
        [Serializable] private sealed class Sources { public Source[] items; }
        [Serializable] private sealed class Source { public string kind, file, prompt; }
        private static readonly string[] Palette = {
            "191d2c", "302d41", "4a455b", "625b74", "898098", "aaa0ad",
            "46343b", "64464b", "885952", "aa765b", "be8d56", "ebc482",
            "303d43", "3c5a5c", "567c7e", "78958c", "33533e", "587449", "91a85a",
            "1e2434", "38414e", "536879", "b8aaa0", "ded1b8"
        };

        [MenuItem("Echoes/Art/Prepare First Floor Room Sprites")]
        public static void Prepare()
        {
            var sources = JsonUtility.FromJson<Sources>(File.ReadAllText(SourceRoot + "/sources.json"));
            var palette = Array.ConvertAll(Palette, hex => {
                ColorUtility.TryParseHtmlString("#" + hex, out var color); return (Color32)color;
            });
            foreach (var source in sources.items)
            {
                var entry = RoomArt.Definition(source.kind);
                // The shared three-floor tuning-fork stall has its own approved source/export.
                if (source.kind == "room_stall") continue;
                var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                original.LoadImage(File.ReadAllBytes(SourceRoot + "/原始图/" + source.file));
                var pixels = original.GetPixels32();
                int left = original.width, right = 0, bottom = original.height, top = 0;
                for (int y = 0; y < original.height; y++)
                    for (int x = 0; x < original.width; x++)
                        if (pixels[y * original.width + x].a >= 180)
                        { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
                if (right <= left || top <= bottom) throw new InvalidOperationException("Empty source: " + source.file);
                int w = entry.Canvas.x, h = entry.Canvas.y;
                int foot = entry.Kind == "room_stall" ? 0 : entry.Ground ? 1 : h / 8;
                float scale = Mathf.Min((w - 2f) / (right - left + 1), (h - foot - 1f) / (top - bottom + 1));
                int dw = Mathf.Max(1, Mathf.RoundToInt((right - left + 1) * scale));
                int dh = Mathf.Max(1, Mathf.RoundToInt((top - bottom + 1) * scale));
                int dx = (w - dw) / 2, dy = entry.Ground ? (h - dh) / 2 : foot;
                var output = new Color32[w * h];
                for (int y = 0; y < dh; y++)
                    for (int x = 0; x < dw; x++)
                    {
                        int sx = left + Mathf.Min(right - left, (int)((x + .5f) * (right - left + 1) / dw));
                        int sy = bottom + Mathf.Min(top - bottom, (int)((y + .5f) * (top - bottom + 1) / dh));
                        var color = pixels[sy * original.width + sx];
                        if (color.a < 180) continue;
                        int best = int.MaxValue; Color32 nearest = palette[0];
                        foreach (var candidate in palette)
                        {
                            int dr = color.r - candidate.r, dg = color.g - candidate.g, db = color.b - candidate.b;
                            int distance = dr * dr + dg * dg + db * db;
                            if (distance < best) { best = distance; nearest = candidate; }
                        }
                        nearest.a = 255;
                        output[(y + dy) * w + x + dx] = nearest;
                    }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                texture.SetPixels32(output); texture.Apply();
                string path = "Assets/Echoes/Resources/" + entry.Path + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("ECHOES_ROOM_ART_PREPARED " + sources.items.Length + " native sprites");
            CreatePreview();
        }

        [MenuItem("Echoes/Art/Refresh Room Sprite Preview")]
        public static void CreatePreview()
        {
            const int cell = 160, columns = 4, rows = 4;
            var preview = new Texture2D(cell * columns, cell * rows, TextureFormat.RGBA32, false);
            var pixels = new Color32[preview.width * preview.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(23, 29, 42, 255);
            for (int i = 0; i < RoomArt.Entries.Length; i++)
            {
                var entry = RoomArt.Entries[i];
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                image.LoadImage(File.ReadAllBytes("Assets/Echoes/Resources/" + entry.Path + ".png"));
                var source = image.GetPixels32();
                int ox = (i % columns) * cell + (cell - image.width) / 2;
                int oy = (rows - 1 - i / columns) * cell + (cell - image.height) / 2;
                for (int y = 0; y < image.height; y++)
                    for (int x = 0; x < image.width; x++)
                        if (source[y * image.width + x].a != 0)
                            pixels[(oy + y) * preview.width + ox + x] = source[y * image.width + x];
                UnityEngine.Object.DestroyImmediate(image);
            }
            preview.SetPixels32(pixels); preview.Apply();
            File.WriteAllBytes(SourceRoot + "/第一层资源预览.png", preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);
        }
    }
}

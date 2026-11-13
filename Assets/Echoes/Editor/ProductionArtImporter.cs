using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Echoes.Editor
{
    public sealed class ProductionArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Echoes/Resources/Art/") || !assetPath.EndsWith(".png")) return;
            Configure((TextureImporter)assetImporter);
        }

        private static void Configure(TextureImporter importer)
        {
            string name = Path.GetFileNameWithoutExtension(importer.assetPath);
            int size = name == "hero" || name == "props" || name == "walker" || name == "guard" || name == "elite" ? 32 :
                name == "boss" ? 64 : 16;
            int cols = name == "hero" ? 4 : name == "walker" || name == "guard" || name == "elite" || name == "boss" ? 6 : 8;
            int rows = name == "hero" ? 5 : name == "props" ? 2 : 1;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            bool actor = name == "hero" || name == "walker" || name == "guard" || name == "elite" || name == "boss";
            Vector2 pivot = actor || name == "props" ? new Vector2(.5f, .125f) : new Vector2(.5f,.5f);
            if (name == "solid") { importer.spriteImportMode = SpriteImportMode.Single; return; }
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = Enumerable.Range(0, cols * rows).Select(i => new SpriteMetaData
            {
                name = name + "_" + i.ToString("D2"),
                rect = new Rect(i % cols * size, (rows - 1 - i / cols) * size, size, size),
                alignment = (int)SpriteAlignment.Custom,
                pivot = name == "props" && i == 5 ? new Vector2(.5f,.5f) : pivot,
                border = Vector4.zero
            }).ToArray();
        }

        [MenuItem("Echoes/Art/Import Formal Sprites")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in Directory.GetFiles("Assets/Echoes/Resources/Art", "*.png", SearchOption.AllDirectories))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                Configure(importer); importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ECHOES_FORMAL_ART_IMPORTED");
        }

        [MenuItem("Echoes/Art/Preview Theme/1 - Silent Bell District")]
        private static void PreviewFirst() => Preview(0);
        [MenuItem("Echoes/Art/Preview Theme/2 - Rust Resonance Works")]
        private static void PreviewSecond() => Preview(1);
        [MenuItem("Echoes/Art/Preview Theme/3 - Original Tone Sanctuary")]
        private static void PreviewThird() => Preview(2);
        private static void Preview(int theme)
        {
            var run = Object.FindObjectOfType<RunController>();
            if (!EditorApplication.isPlaying || !run || !run.Map)
            {
                Debug.Log("Open Prototype and enter Play Mode to review an art theme.");
                return;
            }
            run.Map.ApplyArtTheme(theme);
            run.Hud.Notify("美术主题预览 · 不改变当前关卡与敌人行为", 3);
        }
    }
}

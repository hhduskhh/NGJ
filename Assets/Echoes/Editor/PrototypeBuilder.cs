using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echoes.Editor
{
    public static class PrototypeBuilder
    {
        public const string ScenePath = "Assets/Echoes/Scenes/Prototype.unity";
        public const string ConfigPath = "Assets/Echoes/Settings/DefaultGameConfig.asset";

        [MenuItem("Echoes/Create or Update Prototype Scene")]
        public static void Create()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                TMP_PackageResourceImporter.ImportResources(true, false, false);
            Directory.CreateDirectory("Assets/Echoes/Scenes");
            Directory.CreateDirectory("Assets/Echoes/Settings");
            Directory.CreateDirectory("Assets/Echoes/Resources");
            AssetDatabase.Refresh();
            const string fontPath = "Assets/Echoes/Resources/PrototypeFont.asset";
            if (!AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath))
            {
                var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Echoes/Fonts/NotoSansSC.ttf");
                var font = TMP_FontAsset.CreateFontAsset(sourceFont, 36, 4,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
                if (!font) throw new System.Exception("Unable to create the bundled CJK font.");
                font.name = "PrototypeFont";
                AssetDatabase.CreateAsset(font, fontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(font);
            }
            var generatedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (!generatedFont.material)
                generatedFont.material = AssetDatabase.LoadAllAssetsAtPath(fontPath).OfType<Material>().First();
            if (!generatedFont.atlasTextures[0])
                generatedFont.atlasTextures = new[] { AssetDatabase.LoadAllAssetsAtPath(fontPath).OfType<Texture2D>().First() };
            // Prewarm the actual source strings and persist subasset references after assigning local IDs.
            string characters = string.Concat(Directory.GetFiles("Assets/Echoes/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            generatedFont.TryAddCharacters(characters);
            EditorUtility.SetDirty(generatedFont);
            EditorUtility.SetDirty(generatedFont.material);
            foreach (var texture in generatedFont.atlasTextures) EditorUtility.SetDirty(texture);
            const string materialPath = "Assets/Echoes/Resources/EchoesSprite.mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(materialPath))
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (!shader) throw new System.Exception("URP 2D sprite shader is unavailable.");
                AssetDatabase.CreateAsset(new Material(shader), materialPath);
            }
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            // Keep the template's matching URP 2D renderer; do not replace the rendering pipeline.
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            pipeline.msaaSampleCount = 1;
            EditorUtility.SetDirty(pipeline);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 11.25f;
            camera.backgroundColor = new Color32(12, 18, 29, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.transform.position = new Vector3(8.5f, 6.5f, -10);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            var pixelCamera = camera.gameObject.AddComponent<UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera>();
            pixelCamera.assetsPPU = 16; pixelCamera.refResolutionX = 640; pixelCamera.refResolutionY = 360;
            pixelCamera.gridSnapping = UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixelCamera.cropFrame = UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera.CropFrame.None;
            var light = new GameObject("Global Light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1;
            var controller = new GameObject("RunController").AddComponent<RunController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName = "NGJ";
            PlayerSettings.productName = "Echoes Below";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            Time.fixedDeltaTime = 1f / 60;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("ECHOES_SETUP_OK: " + ScenePath);
        }

        [MenuItem("Echoes/Build Windows Prototype")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) Create();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/EchoesBelow.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
            Debug.Log("ECHOES_BUILD_OK: " + report.summary.totalSize);
        }
    }
}

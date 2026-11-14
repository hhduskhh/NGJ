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
        public const string OpeningPath = "Assets/Echoes/Scenes/Opening.unity";
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
            BakeInterfaceFont();
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(OpeningPath, true), new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("ECHOES_SETUP_OK: " + ScenePath);
        }

        [MenuItem("Echoes/Bake Interface Font")]
        public static void BakeInterfaceFont()
        {
            const string path = "Assets/Echoes/Resources/FusionPixelUI.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (!font)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Echoes/Fonts/FusionPixel12.ttf");
                font = TMP_FontAsset.CreateFontAsset(source, 12, 1,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER, 1024, 1024,
                    AtlasPopulationMode.Dynamic, false);
                font.name = "FusionPixelUI";
                font.material.name = "Fusion Pixel Bitmap";
                font.atlasTexture.name = "Fusion Pixel Atlas";
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            }

            // Include authored names/descriptions and the characters used to format live counters.
            var files = Directory.GetFiles("Assets/Echoes/Scripts", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Echoes/Settings", "*.asset", SearchOption.AllDirectories));
            var characters = string.Concat(files.Select(File.ReadAllText)) + "0123456789";
            string repertoire = new string(characters.Where(c => !char.IsControl(c)).Distinct().OrderBy(c => c).ToArray());
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Echoes/Resources/PrototypeFont.asset");
            font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
            if (!font.TryAddCharacters(repertoire, out var missing))
                Debug.LogWarning("Interface font fallback characters: " + missing);
            font.atlasTexture.filterMode = FilterMode.Point;
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font.atlasTexture);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Echoes/Build Windows Prototype")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) Create();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { OpeningPath, ScenePath },
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

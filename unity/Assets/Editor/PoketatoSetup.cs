using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Poketato.EditorTools
{
    /// <summary>
    /// Rebuilds the 2D project from code:
    ///   Unity -batchmode -projectPath unity -executeMethod Poketato.EditorTools.PoketatoSetup.All -quit
    /// </summary>
    public static class PoketatoSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Poketato/Setup Everything")]
        public static void All()
        {
            ConfigurePlayer();
            ConfigureQuality();
            ImportSprites();
            ImportFont();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Poketato] setup complete");
        }

        [MenuItem("Poketato/Steps/Player Settings")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Poketato";
            PlayerSettings.productName = "Poketato";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            // Gzip + JS decompression fallback: a plain static host (Render) needs no Content-Encoding headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:Poketato";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.06f, 0.1f, 0.06f);
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 640;
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 640;
        }

        [MenuItem("Poketato/Steps/Quality")]
        public static void ConfigureQuality()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsDir + "/URP_2D.asset");
            if (!pipeline) { Debug.LogError("[Poketato] URP_2D.asset missing"); return; }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            qs.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Poketato/Steps/Import Sprites")]
        public static void ImportSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Pokemon" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".md")) continue;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!ti) continue;
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Point;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.isReadable = true;
                ti.SaveAndReimport();
            }
        }

        [MenuItem("Poketato/Steps/Import Font")]
        public static void ImportFont()
        {
            var fi = AssetImporter.GetAtPath("Assets/Resources/Fonts/FusionPixel12.ttf") as TrueTypeFontImporter;
            if (!fi) { Debug.LogWarning("[Poketato] font missing; run tools/subset_font.py"); return; }
            fi.fontSize = 12;
            fi.fontRenderingMode = FontRenderingMode.HintedRaster;
            fi.fontTextureCase = FontTextureCase.Dynamic;
            fi.includeFontData = true;
            fi.SaveAndReimport();
        }

        [MenuItem("Poketato/Steps/Build Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = PixelScreen.Height / 2f / PixelArt.WorldPPU;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.47f, 0.72f, 0.36f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow2D>();
            var screen = camGo.AddComponent<PixelScreen>();
            screen.worldCamera = cam;

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var world = new GameObject("World");
            var map = world.AddComponent<ArenaMap>();
            var view = world.AddComponent<ArenaView>();
            view.cam = follow;

            var game = new GameObject("Game");
            var ui = game.AddComponent<GameScreens>();
            var perf = game.AddComponent<PerfStats>();
            var gm = game.AddComponent<GameManager>();
            gm.map = map;
            gm.view = view;
            gm.ui = ui;
            gm.perf = perf;
            gm.screen = screen;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}

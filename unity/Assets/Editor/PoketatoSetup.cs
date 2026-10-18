using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Poketato.EditorTools
{
    /// <summary>
    /// Rebuilds the 2.5D URP stage:
    ///   Unity -batchmode -projectPath unity -executeMethod Poketato.EditorTools.PoketatoSetup.All -quit
    /// </summary>
    public static class PoketatoSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string PipelinePath = SettingsDir + "/URP_25D.asset";
        const string RendererPath = SettingsDir + "/ForwardRenderer.asset";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Poketato/Setup Everything")]
        public static void All()
        {
            ConfigurePlayer();
            ConfigureQuality();
            ImportFont();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Poketato] setup complete");
        }

        [InitializeOnLoadMethod]
        static void Bootstrap()
        {
            EditorApplication.delayCall += delegate
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var current = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (current != null && current.name == "URP_25D") return;
                ConfigureQuality();
            };
        }

        [MenuItem("Poketato/Steps/Player Settings")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Poketato";
            PlayerSettings.productName = "Poketato";
            PlayerSettings.bundleVersion = "0.3.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:Poketato";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
        }

        [MenuItem("Poketato/Steps/Quality")]
        public static void ConfigureQuality()
        {
            var pipeline = EnsurePipeline();
            if (pipeline == null) return;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            qs.ApplyModifiedPropertiesWithoutUndo();
        }

        public static UniversalRenderPipelineAsset EnsurePipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "ForwardRenderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "URP_25D";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            if (list != null && list.arraySize > 0)
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_ReflectionProbeBlending", true);
            SetBool(so, "m_ReflectionProbeBoxProjection", true);
            SetBool(so, "m_SupportsHDR", true);
            SetFloat(so, "m_ShadowDistance", 80f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            so.ApplyModifiedPropertiesWithoutUndo();
            return pipeline;
        }

        static void SetBool(SerializedObject so, string name, bool value)
        {
            var property = so.FindProperty(name);
            if (property != null) property.boolValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            var property = so.FindProperty(name);
            if (property != null) property.floatValue = value;
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            var property = so.FindProperty(name);
            if (property != null) property.intValue = value;
        }

        [MenuItem("Poketato/Steps/Import Font")]
        public static void ImportFont()
        {
            var fi = AssetImporter.GetAtPath("Assets/Resources/Fonts/FusionPixel12.ttf") as TrueTypeFontImporter;
            if (fi == null) return;
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
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = new Vector3(0f, 16f, -12f);
            camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>().renderShadows = true;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;

            var probeGo = new GameObject("Reflection Probe");
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.boxProjection = true;
            probe.size = new Vector3(80f, 16f, 40f);
            probe.resolution = 128;

            var game = new GameObject("Mission");
            game.AddComponent<MissionDriver>();
            game.AddComponent<PerfStats>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}

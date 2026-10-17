using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Poketato.EditorTools
{
    /// <summary>
    /// Builds the whole MVP from code so it is reproducible in batch mode:
    ///   Unity -batchmode -projectPath unity -executeMethod Poketato.EditorTools.PoketatoSetup.All -quit
    /// Steps: player settings, three URP quality tiers, generated textures/meshes, sprite + font import,
    /// materials, post-processing volume, and the Main scene.
    /// </summary>
    public static class PoketatoSetup
    {
        const string GenDir = "Assets/Art/Generated";
        const string MatDir = "Assets/Materials";
        const string SettingsDir = "Assets/Settings";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Poketato/Setup Everything")]
        public static void All()
        {
            ConfigurePlayer();
            ConfigureQuality();
            GenerateTextures();
            ImportSprites();
            ImportFont();
            CreateMaterials();
            CreateVolume();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Poketato] setup complete");
        }

        // ------------------------------------------------------------------ player settings

        [MenuItem("Poketato/Steps/Player Settings")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Poketato";
            PlayerSettings.productName = "Poketato";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            // Gzip + decompression fallback: the loader can decompress in JS, so any static host (Render Static
            // Site included) works without custom Content-Encoding headers. PoketatoBuild can switch to Brotli.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:Poketato";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
        }

        // ------------------------------------------------------------------ URP quality tiers

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[Poketato] property {name} not found on {so.targetObject.name}"); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case Object o: p.objectReferenceValue = o; break;
            }
        }

        static T CopyAsset<T>(string from, string to) where T : Object
        {
            if (!File.Exists(to)) AssetDatabase.CopyAsset(from, to);
            return AssetDatabase.LoadAssetAtPath<T>(to);
        }

        [MenuItem("Poketato/Steps/Quality Tiers")]
        public static void ConfigureQuality()
        {
            string mobileRenderer = SettingsDir + "/Mobile_Renderer.asset";
            string pcRenderer = SettingsDir + "/PC_Renderer.asset";
            string mobileAsset = SettingsDir + "/Mobile_RPAsset.asset";
            string pcAsset = SettingsDir + "/PC_RPAsset.asset";
            bool haveTemplate = File.Exists(mobileAsset) && File.Exists(pcAsset);

            var fast = haveTemplate ? CopyAsset<UniversalRendererData>(mobileRenderer, SettingsDir + "/Renderer_Forward.asset") : AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/Renderer_Forward.asset");
            var ssao = haveTemplate ? CopyAsset<UniversalRendererData>(pcRenderer, SettingsDir + "/Renderer_ForwardSSAO.asset") : AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/Renderer_ForwardSSAO.asset");
            foreach (var r in new[] { fast, ssao })
            {
                var so = new SerializedObject(r);
                Set(so, "m_RenderingMode", 0);
                Set(so, "m_DepthPrimingMode", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var tiers = new UniversalRenderPipelineAsset[3];
            string[] names = { "Low", "Medium", "High" };
            for (int i = 0; i < 3; i++)
            {
                string path = $"{SettingsDir}/URP_{names[i]}.asset";
                tiers[i] = haveTemplate ? CopyAsset<UniversalRenderPipelineAsset>(i == 2 ? pcAsset : mobileAsset, path) : AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var so = new SerializedObject(tiers[i]);
                var list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = i == 2 ? ssao : fast;
                Set(so, "m_DefaultRendererIndex", 0);
                Set(so, "m_SupportsHDR", true);
                Set(so, "m_MSAA", 1);
                Set(so, "m_RenderScale", i == 0 ? 0.8f : 1f);
                Set(so, "m_RequireDepthTexture", i == 2);
                Set(so, "m_RequireOpaqueTexture", false);
                Set(so, "m_MainLightRenderingMode", 1);
                Set(so, "m_MainLightShadowsSupported", true);
                Set(so, "m_MainLightShadowmapResolution", i == 0 ? 1024 : 2048);
                Set(so, "m_AdditionalLightsRenderingMode", 1);
                Set(so, "m_AdditionalLightsPerObjectLimit", i == 0 ? 2 : i == 1 ? 4 : 8);
                Set(so, "m_AdditionalLightShadowsSupported", i == 2);
                Set(so, "m_AdditionalLightsShadowmapResolution", 1024);
                Set(so, "m_ReflectionProbeBlending", i == 2);
                Set(so, "m_ReflectionProbeBoxProjection", true);
                Set(so, "m_ShadowDistance", i == 0 ? 32f : i == 1 ? 42f : 55f);
                Set(so, "m_ShadowCascadeCount", i == 0 ? 1 : i == 1 ? 2 : 4);
                Set(so, "m_Cascade2Split", 0.3f);
                Set(so, "m_ShadowDepthBias", 0.6f);
                Set(so, "m_ShadowNormalBias", 0.4f);
                Set(so, "m_SoftShadowsSupported", i > 0);
                Set(so, "m_SoftShadowQuality", i == 2 ? 3 : 2);
                Set(so, "m_UseSRPBatcher", true);
                Set(so, "m_SupportsDynamicBatching", false);
                Set(so, "m_UseAdaptivePerformance", false);
                Set(so, "m_EnableLODCrossFade", false);
                Set(so, "m_SupportsLightLayers", false);
                Set(so, "m_SupportDataDrivenLensFlare", false);
                Set(so, "m_SupportScreenSpaceLensFlare", false);
                Set(so, "m_GPUResidentDrawerMode", 0);
                Set(so, "m_ColorGradingMode", i == 2 ? 1 : 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tiers[i]);
            }

            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            levels.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var e = levels.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = names[i];
                e.FindPropertyRelative("customRenderPipeline").objectReferenceValue = tiers[i];
                var vsync = e.FindPropertyRelative("vSyncCount");
                if (vsync != null) vsync.intValue = 0;
                var aa = e.FindPropertyRelative("antiAliasing");
                if (aa != null) aa.intValue = 0;
            }
            var defaults = qs.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var e = defaults.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("second").intValue = e.FindPropertyRelative("first").stringValue == "Standalone" ? 2 : 1;
            }
            qs.FindProperty("m_CurrentQuality").intValue = 1;
            qs.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = tiers[1];

            if (haveTemplate)
            {
                foreach (var p in new[] { mobileAsset, pcAsset, mobileRenderer, pcRenderer, SettingsDir + "/SampleSceneProfile.asset" })
                    AssetDatabase.DeleteAsset(p);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ generated textures

        static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
        }

        static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float H(int i, int j) => Hash(((i % period) + period) % period, ((j % period) + period) % period, seed);
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), sx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), sx), sy);
        }

        static float Fbm(int px, int py, int size, int baseCells, int octaves, int seed)
        {
            float v = 0, amp = 0.5f, total = 0;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                v += TileNoise(px * cells / (float)size, py * cells / (float)size, cells, seed + o * 17) * amp;
                total += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return v / total;
        }

        static void SavePng(string path, Texture2D tex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        [MenuItem("Poketato/Steps/Generate Textures")]
        public static void GenerateTextures()
        {
            // Pixel-art ground tiles in the palette of poke-rouge's code-drawn route tiles.
            var grass = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] gp = { Hex("#3f7f33"), Hex("#4a8c3a"), Hex("#548f40"), Hex("#38722e"), Hex("#2e6327") };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float n = Fbm(x, y, 64, 4, 3, 3);
                int k = Mathf.Clamp(Mathf.FloorToInt(n * 4.2f - 0.6f), 0, 3);
                var c = gp[k];
                if (Hash(x, y, 9) > 0.93f) c = gp[4];
                if (Hash(x / 2, y / 2, 11) > 0.985f) c = Hex("#7fb35a");
                grass.SetPixel(x, y, c);
            }
            // Little grass tufts (two-pixel "v" shapes) like the 16px route tiles.
            for (int i = 0; i < 26; i++)
            {
                int tx = (int)(Hash(i, 1, 5) * 62), ty = (int)(Hash(i, 2, 5) * 61) + 1;
                grass.SetPixel(tx, ty, gp[4]); grass.SetPixel(tx + 1, ty + 1, gp[4]); grass.SetPixel(tx + 2, ty, gp[4]);
                grass.SetPixel(tx + 1, ty + 2, Hex("#6aa64c"));
            }
            grass.Apply();
            SavePng(GenDir + "/grass.png", grass);

            var dirt = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] dp = { Hex("#b08a5a"), Hex("#c09a66"), Hex("#a07a4c"), Hex("#8c6a40") };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float n = Fbm(x, y, 64, 4, 3, 21);
                var c = dp[Mathf.Clamp(Mathf.FloorToInt(n * 3.6f - 0.3f), 0, 2)];
                if (Hash(x, y, 23) > 0.95f) c = dp[3];
                if (Hash(x, y, 29) > 0.985f) c = Hex("#e0c89a");
                dirt.SetPixel(x, y, c);
            }
            dirt.Apply();
            SavePng(GenDir + "/dirt.png", dirt);

            var stone = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int row = y / 16;
                int ox = (row % 2) * 8;
                bool grout = y % 16 == 0 || (x + ox) % 16 == 0;
                int tile = ((x + ox) / 16) * 7 + row * 13;
                float tone = 0.82f + Hash(tile, row, 31) * 0.12f + (Fbm(x, y, 64, 8, 2, 37) - 0.5f) * 0.12f;
                var c = grout ? Hex("#5d6268") : new Color(0.68f * tone, 0.7f * tone, 0.74f * tone);
                if (!grout && (y % 16 == 1 || (x + ox) % 16 == 1)) c *= 1.12f;
                c.a = 1;
                stone.SetPixel(x, y, c);
            }
            stone.Apply();
            SavePng(GenDir + "/stone.png", stone);

            var noise = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float n = Fbm(x, y, 128, 4, 5, 41);
                noise.SetPixel(x, y, new Color(n, n, n, 1));
            }
            noise.Apply();
            SavePng(GenDir + "/noise.png", noise);

            var heights = new float[128, 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++) heights[x, y] = Fbm(x, y, 128, 8, 4, 53);
            var nrm = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = heights[(x + 1) % 128, y] - heights[(x + 127) % 128, y];
                float dy = heights[x, (y + 1) % 128] - heights[x, (y + 127) % 128];
                var n = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                nrm.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1));
            }
            nrm.Apply();
            SavePng(GenDir + "/water_normal.png", nrm);

            var spark = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                spark.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            spark.Apply();
            SavePng(GenDir + "/spark.png", spark);

            // PMD-style dialog frame: tintable fill + separate white double border.
            var fill = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            var border = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(23 - x, 23 - y));
                bool corner = (x < 2 || x > 21) && (y < 2 || y > 21);
                fill.SetPixel(x, y, corner || e < 1 ? Color.clear : Color.white);
                Color b = Color.clear;
                if (!corner)
                {
                    if (e == 0) b = new Color(0.02f, 0.03f, 0.1f, 1);
                    else if (e == 1 || e == 2) b = new Color(0.97f, 0.98f, 1f, 1);
                    else if (e == 4) b = new Color(0.75f, 0.82f, 1f, 0.9f);
                }
                border.SetPixel(x, y, b);
            }
            fill.Apply();
            border.Apply();
            SavePng("Assets/Resources/UI/PanelFill.png", fill);
            SavePng("Assets/Resources/UI/PanelBorder.png", border);

            AssetDatabase.Refresh();
            ConfigureTexture(GenDir + "/grass.png", FilterMode.Point, TextureWrapMode.Repeat, true, true);
            ConfigureTexture(GenDir + "/dirt.png", FilterMode.Point, TextureWrapMode.Repeat, true, true);
            ConfigureTexture(GenDir + "/stone.png", FilterMode.Point, TextureWrapMode.Repeat, true, true);
            ConfigureTexture(GenDir + "/noise.png", FilterMode.Bilinear, TextureWrapMode.Repeat, false, true);
            ConfigureTexture(GenDir + "/spark.png", FilterMode.Bilinear, TextureWrapMode.Clamp, true, false);
            var ni = (TextureImporter)AssetImporter.GetAtPath(GenDir + "/water_normal.png");
            ni.textureType = TextureImporterType.NormalMap;
            ni.wrapMode = TextureWrapMode.Repeat;
            ni.filterMode = FilterMode.Bilinear;
            ni.SaveAndReimport();
            foreach (var p in new[] { "Assets/Resources/UI/PanelFill.png", "Assets/Resources/UI/PanelBorder.png" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100;
                ti.spriteBorder = new Vector4(6, 6, 6, 6);
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        static void ConfigureTexture(string path, FilterMode filter, TextureWrapMode wrap, bool srgb, bool mips)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.filterMode = filter;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = mips;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ sprites & font

        [MenuItem("Poketato/Steps/Import Sprites")]
        public static void ImportSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Pokemon" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Point;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        [MenuItem("Poketato/Steps/Import Font")]
        public static void ImportFont()
        {
            var fi = AssetImporter.GetAtPath("Assets/Resources/Fonts/FusionPixel12.ttf") as TrueTypeFontImporter;
            if (fi == null) { Debug.LogWarning("[Poketato] font missing; run tools/subset_font.py"); return; }
            fi.fontSize = 24;
            fi.fontRenderingMode = FontRenderingMode.HintedRaster;
            fi.fontTextureCase = FontTextureCase.Dynamic;
            fi.includeFontData = true;
            fi.SaveAndReimport();
        }

        // ------------------------------------------------------------------ materials

        static Material Mat(string path, Shader shader, System.Action<Material> cfg)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            cfg?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Unlit => Shader.Find("Universal Render Pipeline/Unlit");

        static System.Action<Material> LitCfg(Color c, float smooth, Texture tex = null, Color? emission = null, float metallic = 0f)
        {
            return m =>
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metallic);
                if (tex) m.SetTexture("_BaseMap", tex);
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                m.enableInstancing = true;
            };
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{GenDir}/{name}.png");

        [MenuItem("Poketato/Steps/Create Materials")]
        public static void CreateMaterials()
        {
            var sprite = Shader.Find("Poketato/BillboardSprite");
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Pokemon" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Mat($"Assets/Resources/PokemonMaterials/{tex.name}.mat", sprite, m =>
                {
                    m.SetTexture("_MainTex", tex);
                    m.enableInstancing = true;
                });
            }

            var add = Shader.Find("Poketato/FxAdditive");
            Mat("Assets/Resources/Fx/FxOrb.mat", Unlit, m => { m.SetColor("_BaseColor", Color.white); m.enableInstancing = true; });
            Mat("Assets/Resources/Fx/FxAdditive.mat", add, m => m.SetColor("_BaseColor", Color.white));
            Mat("Assets/Resources/Fx/FxSpark.mat", add, m => { m.SetTexture("_MainTex", Tex("spark")); m.SetColor("_BaseColor", new Color(3f, 3f, 3f, 1)); });
            Mat("Assets/Resources/Fx/Candy.mat", Lit, LitCfg(new Color(1f, 0.55f, 0.72f), 0.85f, null, new Color(0.7f, 0.25f, 0.45f)));
            Mat("Assets/Resources/Fx/Berry.mat", Lit, LitCfg(new Color(0.35f, 0.55f, 1f), 0.8f, null, new Color(0.15f, 0.25f, 0.6f)));
            var sphere = MeshGen.UVSphere(14, 10);
            sphere.hideFlags = HideFlags.None;
            SaveMesh(sphere, "Assets/Resources/Fx/Sphere.asset");

            Mat($"{MatDir}/Ground.mat", Lit, LitCfg(new Color(0.82f, 0.84f, 0.8f), 0.12f, Tex("grass")));
            Mat($"{MatDir}/Dirt.mat", Lit, LitCfg(Color.white, 0.1f, Tex("dirt")));
            Mat($"{MatDir}/Stone.mat", Lit, LitCfg(new Color(0.95f, 0.95f, 1f), 0.86f, Tex("stone")));
            Mat($"{MatDir}/Bark.mat", Lit, LitCfg(Hex("#6b4a2e"), 0.1f));
            Mat($"{MatDir}/Leaves.mat", Lit, LitCfg(Hex("#3f8f3a"), 0.15f));
            Mat($"{MatDir}/LeavesDark.mat", Lit, LitCfg(Hex("#2e6e33"), 0.15f));
            Mat($"{MatDir}/LeavesLight.mat", Lit, LitCfg(Hex("#6aae3f"), 0.15f));
            Mat($"{MatDir}/Rock.mat", Lit, LitCfg(Hex("#8a8f96"), 0.25f));
            Mat($"{MatDir}/RockDark.mat", Lit, LitCfg(Hex("#3a3330"), 0.3f));
            Mat($"{MatDir}/Hill.mat", Lit, LitCfg(Hex("#3d7a3a"), 0.05f));
            Mat($"{MatDir}/Wood.mat", Lit, LitCfg(Hex("#8a6038"), 0.2f));
            Mat($"{MatDir}/Metal.mat", Lit, LitCfg(Hex("#2c2f36"), 0.6f, null, null, 0.8f));
            Mat($"{MatDir}/GrassTuft.mat", Lit, LitCfg(Hex("#5fae45"), 0.1f));
            Mat($"{MatDir}/FlowerRed.mat", Lit, LitCfg(Hex("#e8504a"), 0.3f));
            Mat($"{MatDir}/FlowerYellow.mat", Lit, LitCfg(Hex("#f6d24a"), 0.3f));
            Mat($"{MatDir}/FlowerWhite.mat", Lit, LitCfg(Hex("#f4f1ea"), 0.3f));
            Mat($"{MatDir}/LanternGlow.mat", Unlit, m => m.SetColor("_BaseColor", new Color(6f, 3.6f, 1.6f, 1)));
            Mat($"{MatDir}/Crystal.mat", Lit, LitCfg(new Color(1f, 0.55f, 0.85f), 0.9f, null, new Color(2.6f, 0.7f, 2.0f)));
            Mat($"{MatDir}/Water.mat", Shader.Find("Poketato/Water"), m =>
            {
                m.SetTexture("_NormalMap", Tex("water_normal"));
                m.SetFloat("_NormalScale", 0.35f);
                m.SetFloat("_RippleStrength", 0.32f);
                m.SetFloat("_Specular", 1.4f);
                m.SetFloat("_ReflectionStrength", 0.92f);
                m.SetFloat("_Distortion", 0.015f);
            });
            Mat($"{MatDir}/Lava.mat", Shader.Find("Poketato/Lava"), m =>
            {
                m.SetTexture("_NoiseTex", Tex("noise"));
                m.SetColor("_CrustColor", new Color(0.07f, 0.03f, 0.025f));
                m.SetColor("_HotColor", new Color(0.95f, 0.22f, 0.04f));
                m.SetColor("_GlowColor", new Color(1.7f, 0.62f, 0.12f));
                m.SetFloat("_Tiling", 0.34f);
            });
            Mat($"{MatDir}/Rain.mat", add, m => { m.SetTexture("_MainTex", Tex("spark")); m.SetColor("_BaseColor", new Color(0.35f, 0.45f, 0.6f, 1)); });
            Mat($"{MatDir}/Ember.mat", add, m => { m.SetTexture("_MainTex", Tex("spark")); m.SetColor("_BaseColor", new Color(4f, 1.4f, 0.3f, 1)); });
            Mat($"{MatDir}/Sky.mat", Shader.Find("Skybox/Procedural"), m =>
            {
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_AtmosphereThickness", 1.0f);
                m.SetColor("_GroundColor", new Color(0.32f, 0.36f, 0.3f));
            });
            AssetDatabase.SaveAssets();
        }

        static Mesh SaveMesh(Mesh m, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(m, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ post-processing

        [MenuItem("Poketato/Steps/Create Volume")]
        public static void CreateVolume()
        {
            string path = SettingsDir + "/Poketato_PostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var bloom = Add<Bloom>();
            bloom.threshold.value = 1.0f;
            bloom.intensity.value = 0.8f;
            bloom.scatter.value = 0.68f;
            bloom.highQualityFiltering.value = false;
            var tm = Add<Tonemapping>();
            tm.mode.value = TonemappingMode.ACES;
            var ca = Add<ColorAdjustments>();
            ca.postExposure.value = 0.15f;
            ca.contrast.value = 8f;
            ca.saturation.value = 14f;
            var wb = Add<WhiteBalance>();
            wb.temperature.value = 6f;
            var vg = Add<Vignette>();
            vg.intensity.value = 0.24f;
            vg.smoothness.value = 0.45f;
            var dof = Add<DepthOfField>();
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.value = 30f;
            dof.gaussianEnd.value = 62f;
            dof.gaussianMaxRadius.value = 1.0f;
            dof.active = false;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ scene

        static GameObject EnvObj(string name, Transform parent, EnvKind kind, Material[] mats, bool shadows = true, int layer = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            var em = go.AddComponent<EnvMesh>();
            em.kind = kind;
            em.Rebuild();
            return go;
        }

        static Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        [MenuItem("Poketato/Steps/Build Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Sky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            // --- lights
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.88f;
            sun.intensity = 1.5f;
            sunGo.transform.rotation = Quaternion.Euler(50, -40, 0);
            RenderSettings.sun = sun;

            // --- camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 170;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camData.dithering = true;
            camGo.transform.SetPositionAndRotation(new Vector3(0, 17.7f, -15.4f), Quaternion.Euler(48, 0, 0));
            var rig = camGo.AddComponent<CameraRig>();
            var reflection = camGo.AddComponent<PlanarReflection>();
            reflection.source = cam;

            // --- post
            var volGo = new GameObject("PostFX");
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SettingsDir + "/Poketato_PostFX.asset");

            // --- environment (meshes are generated by EnvMesh at load time)
            var env = new GameObject("Environment").transform;
            EnvObj("Ground", env, EnvKind.Ground, new[] { M("Ground") }, false);
            EnvObj("DirtPath", env, EnvKind.Path, new[] { M("Dirt") }, false);
            EnvObj("Plaza", env, EnvKind.Plaza, new[] { M("Stone") }, false);
            EnvObj("PlazaCurb", env, EnvKind.Curb, new[] { M("Rock") });
            EnvObj("RainWater", env, EnvKind.RainWater, new[] { M("Water") }, false, 4);
            EnvObj("LavaPool", env, EnvKind.Lava, new[] { M("Lava") }, false);
            EnvObj("LavaRim", env, EnvKind.LavaRim, new[] { M("RockDark") });
            EnvObj("Crystals", env, EnvKind.Crystals, new[] { M("Crystal") });
            EnvObj("Lanterns", env, EnvKind.Lanterns, new[] { M("Wood"), M("Metal"), M("LanternGlow") });
            EnvObj("Trees", env, EnvKind.Trees, new[] { M("Bark"), M("Leaves"), M("LeavesDark"), M("LeavesLight") });
            EnvObj("Hills", env, EnvKind.Hills, new[] { M("Hill") }, false);
            EnvObj("Rocks", env, EnvKind.Rocks, new[] { M("Rock") });
            EnvObj("GrassTufts", env, EnvKind.Tufts, new[] { M("GrassTuft") }, false);
            EnvObj("Flowers", env, EnvKind.Flowers, new[] { M("FlowerRed"), M("FlowerYellow"), M("FlowerWhite") }, false);
            EnvObj("Fence", env, EnvKind.Fence, new[] { M("Wood") });

            var rainCircles = EnvMeshes.FieldCirclesWorld(Fields.All[0]);
            var rainCenter = EnvMeshes.Centroid(rainCircles);
            var rainExtent = EnvMeshes.Extent(rainCircles);
            var rainPs = Particles("Rain", env, new Vector3(rainCenter.x, 7f, rainCenter.y), M("Rain"));
            {
                var main = rainPs.main;
                main.startLifetime = 0.55f;
                main.startSpeed = 13f;
                main.startSize = 0.06f;
                main.maxParticles = 700;
                var em = rainPs.emission;
                em.rateOverTime = 420;
                var sh = rainPs.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(rainExtent.x * 2f, rainExtent.y * 2f, 0.1f);
                rainPs.transform.rotation = Quaternion.Euler(90, 0, 0);
                var r = rainPs.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.05f;
                r.lengthScale = 1.5f;
            }

            var lavaCircles = EnvMeshes.FieldCirclesWorld(Fields.All[1]);
            var lavaCenter = EnvMeshes.Centroid(lavaCircles);
            var lavaExtent = EnvMeshes.Extent(lavaCircles);
            var glowLights = new List<Light>();
            foreach (var c in new[] { lavaCircles[0], lavaCircles[1] })
            {
                var l = PointLight("LavaGlow", env, new Vector3(c.x, 1.3f, c.y), new Color(1f, 0.45f, 0.12f), 3f, 8f);
                l.gameObject.AddComponent<Flicker>();
                glowLights.Add(l);
            }
            var ember = Particles("Embers", env, new Vector3(lavaCenter.x, 0.2f, lavaCenter.y), M("Ember"));
            {
                var main = ember.main;
                main.startLifetime = 2.2f;
                main.startSpeed = 1.1f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                main.gravityModifier = -0.05f;
                main.maxParticles = 120;
                var em = ember.emission;
                em.rateOverTime = 26;
                var sh = ember.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(lavaExtent.x * 1.6f, lavaExtent.y * 1.6f, 0.1f);
                ember.transform.rotation = Quaternion.Euler(-90, 0, 0);
                var noise = ember.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                var col = ember.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, 0.4f, 0.1f), 1) },
                    alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) },
                });
            }
            glowLights.Add(PointLight("CrystalGlow", env, EnvMeshes.CrystalPos + Vector3.up * 2f, new Color(1f, 0.45f, 0.85f), 2.5f, 9f));

            var lanterns = new List<Light>();
            for (int i = 0; i < 4; i++)
                lanterns.Add(PointLight("Lantern" + i, env, EnvMeshes.LanternPos(i) + Vector3.up * 1.85f, new Color(1f, 0.72f, 0.42f), 1f, 8.5f));

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(env, false);
            probeGo.transform.position = new Vector3(0, 1.5f, 0);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.size = new Vector3(70, 14, 60);
            probe.boxProjection = true;
            probe.cullingMask = ~((1 << 4) | (1 << 5));
            probe.intensity = 1f;

            // --- game objects
            var gmGo = new GameObject("Game");
            var lightPool = gmGo.AddComponent<LightPool>();
            var view = gmGo.AddComponent<GameView>();
            view.lights = lightPool;
            view.rig = rig;
            var ui = gmGo.AddComponent<UIManager>();
            var perf = gmGo.AddComponent<PerfStats>();
            var numbers = gmGo.AddComponent<DamageNumbers>();
            var quality = gmGo.AddComponent<QualityController>();
            quality.reflection = reflection;
            quality.lights = lightPool;
            quality.volume = volume;
            quality.shadowLanterns = new[] { lanterns[0] };
            var envCtl = gmGo.AddComponent<EnvironmentController>();
            envCtl.sun = sun;
            envCtl.lanterns = lanterns.ToArray();
            envCtl.glowLights = glowLights.ToArray();
            envCtl.probe = probe;
            envCtl.volume = volume;
            var gm = gmGo.AddComponent<GameManager>();
            gm.view = view;
            gm.ui = ui;
            gm.perf = perf;
            gm.quality = quality;
            gm.environment = envCtl;
            gm.numbers = numbers;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        static ParticleSystem Particles(string name, Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            main.loop = true;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}

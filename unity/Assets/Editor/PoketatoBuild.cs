using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Poketato.EditorTools
{
    /// <summary>
    /// Batch-mode build entry point:
    ///   Unity -batchmode -projectPath unity -buildTarget WebGL -executeMethod Poketato.EditorTools.PoketatoBuild.WebGL -quit
    /// Output: &lt;repo&gt;/webgl (the committed folder Render publishes). Environment overrides:
    ///   POKETATO_WEBGL_OUT          output folder (e.g. build/webgl-br for experiments)
    ///   POKETATO_WEBGL_COMPRESSION  gzip (default, with JS decompression fallback), brotli (needs Content-Encoding
    ///                               headers, no fallback), none
    /// A JSON build report is written to &lt;output&gt;/build-report.json.
    /// </summary>
    public static class PoketatoBuild
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Poketato/Build WebGL")]
        public static void WebGL()
        {
            string outDir = System.Environment.GetEnvironmentVariable("POKETATO_WEBGL_OUT");
            outDir = string.IsNullOrEmpty(outDir) ? Path.Combine(Root, "webgl") : Path.GetFullPath(Path.Combine(Root, outDir));
            string compression = (System.Environment.GetEnvironmentVariable("POKETATO_WEBGL_COMPRESSION") ?? "gzip").ToLowerInvariant();
            switch (compression)
            {
                case "brotli":
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                    PlayerSettings.WebGL.decompressionFallback = false;
                    break;
                case "none":
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                    PlayerSettings.WebGL.decompressionFallback = false;
                    break;
                default:
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                    PlayerSettings.WebGL.decompressionFallback = true;
                    break;
            }

            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            var json = JsonUtility.ToJson(new BuildInfo
            {
                result = s.result.ToString(),
                compression = compression,
                totalSizeBytes = (long)s.totalSize,
                totalTimeSeconds = (float)s.totalTime.TotalSeconds,
                errors = s.totalErrors,
                warnings = s.totalWarnings,
                unityVersion = Application.unityVersion,
            }, true);
            File.WriteAllText(Path.Combine(outDir, "build-report.json"), json);
            Debug.Log("[Poketato] WebGL build " + json);
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [System.Serializable]
        class BuildInfo
        {
            public string result, compression, unityVersion;
            public long totalSizeBytes;
            public float totalTimeSeconds;
            public int errors, warnings;
        }
    }
}

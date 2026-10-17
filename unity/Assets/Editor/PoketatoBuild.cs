using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Poketato.EditorTools
{
    /// <summary>
    /// Batch-mode build entry points:
    ///   Unity -batchmode -projectPath unity -buildTarget WebGL -executeMethod Poketato.EditorTools.PoketatoBuild.WebGL -quit
    /// Output goes to ../build/webgl (git-ignored) plus a small JSON build report next to it.
    /// </summary>
    public static class PoketatoBuild
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Poketato/Build WebGL")]
        public static void WebGL()
        {
            string outDir = Path.Combine(Root, "build", "webgl");
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
                totalSizeBytes = (long)s.totalSize,
                totalTimeSeconds = (float)s.totalTime.TotalSeconds,
                errors = s.totalErrors,
                warnings = s.totalWarnings,
                unityVersion = Application.unityVersion,
            }, true);
            File.WriteAllText(Path.Combine(Root, "build", "webgl-build-report.json"), json);
            Debug.Log("[Poketato] WebGL build " + json);
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        [System.Serializable]
        class BuildInfo
        {
            public string result, unityVersion;
            public long totalSizeBytes;
            public float totalTimeSeconds;
            public int errors, warnings;
        }
    }
}

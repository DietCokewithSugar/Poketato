using UnityEditor;
using UnityEngine;

namespace Poketato.EditorTools
{
    /// <summary>Batch-mode diagnostics: prints bounds and triangle counts of every generated environment mesh.</summary>
    public static class PoketatoDiagnostics
    {
        [MenuItem("Poketato/Diagnostics/Env Mesh Stats")]
        public static void EnvMeshStats()
        {
            int total = 0;
            foreach (EnvKind k in System.Enum.GetValues(typeof(EnvKind)))
            {
                var m = EnvMeshes.Build(k, 20261017);
                int tris = 0;
                for (int s = 0; s < m.subMeshCount; s++) tris += (int)m.GetIndexCount(s) / 3;
                total += tris;
                Debug.Log($"[EnvMesh] {k,-10} verts {m.vertexCount,6} tris {tris,6} bounds center {m.bounds.center} size {m.bounds.size}");
                Object.DestroyImmediate(m);
            }
            Debug.Log($"[EnvMesh] total tris {total}");
        }
    }
}

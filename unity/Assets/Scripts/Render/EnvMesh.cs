using UnityEngine;

namespace Poketato
{
    /// <summary>
    /// Generates an environment mesh at load time from a fixed seed (also in edit mode), so the repository stores
    /// no bulky mesh assets and the field geometry always matches the gameplay data in Fields.cs.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public class EnvMesh : MonoBehaviour
    {
        public EnvKind kind;
        public int seed = 20261017;
        Mesh mesh;

        void OnEnable() => Rebuild();

        void OnDisable()
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        public void Rebuild()
        {
            OnDisable();
            mesh = EnvMeshes.Build(kind, seed);
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }
}

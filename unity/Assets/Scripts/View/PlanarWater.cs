using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Poketato
{
    /// <summary>
    /// Mirrors the main camera across a flat water plane and draws that image on the water material.
    /// URP has no screen-space reflections, so wet missions use this plus a reflection probe.
    /// </summary>
    public class PlanarWater : MonoBehaviour
    {
        public Camera source;
        public Material material;
        public float waterY = 0.12f;

        Camera reflection;
        RenderTexture texture;

        public void SetMaterial(Material value)
        {
            material = value;
            Ensure();
        }

        void OnEnable()
        {
            Ensure();
        }

        void Ensure()
        {
            if (texture == null)
            {
                texture = new RenderTexture(512, 512, 16, RenderTextureFormat.DefaultHDR) { name = "WaterReflection" };
                var go = new GameObject("Water Reflection Camera");
                reflection = go.AddComponent<Camera>();
                reflection.enabled = false;
                reflection.targetTexture = texture;
                reflection.clearFlags = CameraClearFlags.Skybox;
                reflection.cullingMask = ~(1 << 4);
                reflection.GetUniversalAdditionalCameraData().renderShadows = false;
            }
            if (material != null) material.SetTexture("_ReflectionTex", texture);
        }

        void OnDisable()
        {
            if (reflection != null) Destroy(reflection.gameObject);
            if (texture != null) texture.Release();
        }

        void LateUpdate()
        {
            if (source == null) source = Camera.main;
            if (source == null || reflection == null) return;
            var position = source.transform.position;
            position.y = waterY - (position.y - waterY);
            reflection.transform.position = position;
            var euler = source.transform.eulerAngles;
            reflection.transform.rotation = Quaternion.Euler(-euler.x, euler.y, 0f);
            reflection.fieldOfView = source.fieldOfView;
            reflection.nearClipPlane = source.nearClipPlane;
            reflection.farClipPlane = source.farClipPlane;
            reflection.Render();
        }
    }
}

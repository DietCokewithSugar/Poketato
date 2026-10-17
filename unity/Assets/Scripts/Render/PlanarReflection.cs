using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Poketato
{
    /// <summary>
    /// Planar reflection for the puddles (URP has no SSR). A second camera renders the scene mirrored across the
    /// water plane into a render texture that Water.shader samples in screen space.
    /// The view matrix is F * V * R (R = mirror, F = flip camera X) so it stays a proper rotation and culling
    /// does not need to be inverted; the shader compensates by sampling at (1 - u, v).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class PlanarReflection : MonoBehaviour
    {
        public Camera source;
        public float planeY = 0.04f;
        public float clipOffset = 0.03f;
        public float resolutionScale = 0.5f;
        public LayerMask cullingMask = ~((1 << 4) | (1 << 5));

        static readonly int TexId = Shader.PropertyToID("_PlanarReflectionTex");
        const string Keyword = "_PLANAR_REFLECTION";

        Camera refl;
        RenderTexture rt;

        void OnEnable()
        {
            Shader.EnableKeyword(Keyword);
            if (refl) refl.enabled = true;
        }

        void OnDisable()
        {
            Shader.DisableKeyword(Keyword);
            if (refl) refl.enabled = false;
        }

        void OnDestroy()
        {
            if (rt) rt.Release();
        }

        void EnsureResources()
        {
            if (!refl)
            {
                var go = new GameObject("ReflectionCamera");
                go.transform.SetParent(transform, false);
                refl = go.AddComponent<Camera>();
                refl.depth = source.depth - 10;
                refl.clearFlags = CameraClearFlags.Skybox;
                var data = refl.GetUniversalAdditionalCameraData();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.antialiasing = AntialiasingMode.None;
            }
            int w = Mathf.Max(64, Mathf.RoundToInt(source.pixelWidth * resolutionScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(source.pixelHeight * resolutionScale));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt) rt.Release();
                rt = new RenderTexture(w, h, 16, RenderTextureFormat.DefaultHDR) { name = "PlanarReflection", useMipMap = false };
                rt.Create();
                refl.targetTexture = rt;
                Shader.SetGlobalTexture(TexId, rt);
            }
        }

        void LateUpdate()
        {
            if (!source) return;
            EnsureResources();
            refl.cullingMask = cullingMask;
            refl.fieldOfView = source.fieldOfView;
            refl.nearClipPlane = source.nearClipPlane;
            refl.farClipPlane = source.farClipPlane;
            refl.aspect = source.aspect;

            Vector3 n = Vector3.up;
            float d = -planeY;
            var mirror = Mirror(new Vector4(n.x, n.y, n.z, d));
            var flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
            Matrix4x4 view = flipX * source.worldToCameraMatrix * mirror;
            refl.worldToCameraMatrix = view;

            Vector3 cpos = view.MultiplyPoint(new Vector3(0, planeY, 0) + n * clipOffset);
            Vector3 cnormal = view.MultiplyVector(n).normalized;
            var clipPlane = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
            refl.ResetProjectionMatrix();
            refl.projectionMatrix = source.projectionMatrix;
            refl.projectionMatrix = refl.CalculateObliqueMatrix(clipPlane);

            var c2w = view.inverse;
            Vector3 fwd = -(Vector3)c2w.GetColumn(2);
            Vector3 up = c2w.GetColumn(1);
            refl.transform.SetPositionAndRotation(c2w.GetColumn(3), Quaternion.LookRotation(fwd, up));
            refl.worldToCameraMatrix = view;
        }

        static Matrix4x4 Mirror(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1 - 2 * p.x * p.x; m.m01 = -2 * p.x * p.y; m.m02 = -2 * p.x * p.z; m.m03 = -2 * p.w * p.x;
            m.m10 = -2 * p.y * p.x; m.m11 = 1 - 2 * p.y * p.y; m.m12 = -2 * p.y * p.z; m.m13 = -2 * p.w * p.y;
            m.m20 = -2 * p.z * p.x; m.m21 = -2 * p.z * p.y; m.m22 = 1 - 2 * p.z * p.z; m.m23 = -2 * p.w * p.z;
            return m;
        }
    }
}

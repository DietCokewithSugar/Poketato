using UnityEngine;
using UnityEngine.UI;

namespace Poketato
{
    /// <summary>
    /// Renders the world at a fixed 480x320 (exactly 2x the GBA's 240x160, same 3:2 frame) and the overlay
    /// canvas shows that texture with point filtering, scaled by an integer so pixels stay square and sharp.
    /// </summary>
    public class PixelScreen : MonoBehaviour
    {
        public Camera worldCamera;
        public RawImage view;
        public CanvasScaler scaler;

        public const int Width = 480, Height = 320;
        RenderTexture target;

        void Awake()
        {
            target = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32)
            {
                name = "PixelTarget",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            target.Create();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = Height / 2f / PixelArt.WorldPPU;
            worldCamera.aspect = (float)Width / Height;
            worldCamera.targetTexture = target;
            worldCamera.allowHDR = false;
            worldCamera.allowMSAA = false;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.47f, 0.72f, 0.36f);
            if (view) view.texture = target;
        }

        void LateUpdate()
        {
            if (view && target && view.texture != target) view.texture = target;
            int s = Mathf.Max(1, Mathf.Min(Screen.width / Width, Screen.height / Height));
            if (scaler && !Mathf.Approximately(scaler.scaleFactor, s)) scaler.scaleFactor = s;
        }

        void OnDestroy()
        {
            if (worldCamera) worldCamera.targetTexture = null;
            if (target) target.Release();
        }
    }
}

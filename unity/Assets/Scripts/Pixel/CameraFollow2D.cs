using UnityEngine;

namespace Poketato
{
    /// <summary>
    /// Orthographic follow camera for the 480x320 pixel view. Like poke-rouge it eases towards the player and
    /// clamps to the arena (showing a couple of border tiles); the position snaps to whole pixels.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public Vector2 viewHalf = new(15f, 10f);
        public Vector2 arenaHalf = new(24f, 18f);
        public float margin = 2.5f;
        public float follow = 12f;

        Vector2 pos;
        float shakeT, shakeAmp;

        public void Snap()
        {
            pos = Clamp(target ? (Vector2)target.position : Vector2.zero);
            Apply();
        }

        public void Shake(float pixels, float seconds)
        {
            shakeAmp = Mathf.Max(shakeAmp, pixels);
            shakeT = Mathf.Max(shakeT, seconds);
        }

        Vector2 Clamp(Vector2 p)
        {
            float mx = Mathf.Max(0, arenaHalf.x + margin - viewHalf.x), my = Mathf.Max(0, arenaHalf.y + margin - viewHalf.y);
            return new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, -my, my));
        }

        void LateUpdate()
        {
            Vector2 want = Clamp(target ? (Vector2)target.position : Vector2.zero);
            pos = Vector2.Lerp(pos, want, Mathf.Min(1f, Time.unscaledDeltaTime * follow));
            Apply();
        }

        void Apply()
        {
            Vector2 p = pos;
            if (shakeT > 0)
            {
                shakeT -= Time.unscaledDeltaTime;
                p += new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) * shakeAmp / PixelArt.WorldPPU;
                if (shakeT <= 0) shakeAmp = 0;
            }
            float k = PixelArt.WorldPPU;
            transform.position = new Vector3(Mathf.Round(p.x * k) / k, Mathf.Round(p.y * k) / k, -10f);
        }
    }
}

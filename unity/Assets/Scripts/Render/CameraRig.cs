using UnityEngine;

namespace Poketato
{
    /// <summary>Fixed-angle follow camera (HD-2D style): narrow FOV, high pitch, sprites lean slightly towards it.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public float pitch = 48f;
        public float yaw;
        public float distance = 23f;
        public float spriteLean = 14f;
        public float smoothTime = 0.12f;
        public Vector2 clampHalfExtents = new(15f, 11f);

        Vector3 focus, vel;
        float shakeT, shakeAmp;

        void Start()
        {
            focus = target ? target.position : Vector3.zero;
            Place();
        }

        public void Snap()
        {
            focus = target ? target.position : Vector3.zero;
            vel = Vector3.zero;
            Place();
        }

        public void Shake(float amp, float dur)
        {
            shakeAmp = Mathf.Max(shakeAmp, amp);
            shakeT = Mathf.Max(shakeT, dur);
        }

        void LateUpdate()
        {
            Vector3 want = target ? target.position : Vector3.zero;
            want.x = Mathf.Clamp(want.x, -clampHalfExtents.x, clampHalfExtents.x);
            want.z = Mathf.Clamp(want.z, -clampHalfExtents.y, clampHalfExtents.y);
            focus = Vector3.SmoothDamp(focus, want, ref vel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            Place();
        }

        void Place()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 f = focus + Vector3.up * 0.6f;
            Vector3 pos = f - rot * Vector3.forward * distance;
            if (shakeT > 0)
            {
                shakeT -= Time.unscaledDeltaTime;
                pos += (Vector3)Random.insideUnitCircle * shakeAmp;
                shakeAmp *= 0.9f;
            }
            transform.SetPositionAndRotation(pos, rot);
            SpriteActor.Billboard = Quaternion.Euler(spriteLean, yaw, 0);
        }
    }
}

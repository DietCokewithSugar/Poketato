using UnityEngine;

namespace Poketato
{
    /// <summary>Lava / fire light flicker. Modulates range so EnvironmentController keeps ownership of intensity.</summary>
    [RequireComponent(typeof(Light))]
    public class Flicker : MonoBehaviour
    {
        public float amount = 0.18f;
        public float speed = 7f;
        Light l;
        float baseRange, seed;

        void Awake()
        {
            l = GetComponent<Light>();
            baseRange = l.range;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            l.range = baseRange * (1f + n * amount * 2f);
        }
    }
}

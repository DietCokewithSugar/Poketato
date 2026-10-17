using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Poketato
{
    /// <summary>Day / dusk / night lighting presets: everything is real-time, nothing is baked.</summary>
    public class EnvironmentController : MonoBehaviour
    {
        public static readonly string[] Names = { "白天", "黄昏", "夜晚" };
        public static readonly string[] Ids = { "day", "dusk", "night" };

        public Light sun;
        public Light[] lanterns;
        public Light[] glowLights;
        public ReflectionProbe probe;
        public Volume volume;

        public int Preset { get; private set; } = -1;
        Material sky;

        struct P
        {
            public Vector3 sunEuler;
            public Color sun, skyAmb, eqAmb, groundAmb, fog, skyTint;
            public float sunI, lanternI, glowI, exposure, fogStart, fogEnd, bloom, postExposure;
        }

        static readonly P[] Presets =
        {
            new P
            {
                sunEuler = new Vector3(52, -38, 0), sun = new Color(1f, 0.95f, 0.86f), sunI = 1.55f,
                skyAmb = new Color(0.62f, 0.72f, 0.86f), eqAmb = new Color(0.5f, 0.55f, 0.5f), groundAmb = new Color(0.28f, 0.25f, 0.2f),
                fog = new Color(0.68f, 0.78f, 0.88f), fogStart = 30, fogEnd = 80, skyTint = new Color(0.5f, 0.6f, 0.75f), exposure = 1.25f,
                lanternI = 0.6f, glowI = 1.4f, bloom = 0.55f, postExposure = -0.05f,
            },
            new P
            {
                sunEuler = new Vector3(14, -62, 0), sun = new Color(1f, 0.58f, 0.32f), sunI = 1.45f,
                skyAmb = new Color(0.55f, 0.42f, 0.55f), eqAmb = new Color(0.5f, 0.36f, 0.32f), groundAmb = new Color(0.2f, 0.15f, 0.14f),
                fog = new Color(0.78f, 0.52f, 0.45f), fogStart = 24, fogEnd = 70, skyTint = new Color(0.8f, 0.45f, 0.35f), exposure = 0.85f,
                lanternI = 3.2f, glowI = 2.4f, bloom = 0.8f, postExposure = 0.05f,
            },
            new P
            {
                sunEuler = new Vector3(42, 150, 0), sun = new Color(0.5f, 0.6f, 0.95f), sunI = 0.42f,
                skyAmb = new Color(0.1f, 0.13f, 0.24f), eqAmb = new Color(0.08f, 0.09f, 0.14f), groundAmb = new Color(0.04f, 0.04f, 0.06f),
                fog = new Color(0.06f, 0.08f, 0.14f), fogStart = 20, fogEnd = 60, skyTint = new Color(0.12f, 0.15f, 0.3f), exposure = 0.18f,
                lanternI = 6f, glowI = 3.5f, bloom = 1.0f, postExposure = 0.3f,
            },
        };

        public void Apply(int i)
        {
            i = Mathf.Clamp(i, 0, Presets.Length - 1);
            Preset = i;
            var p = Presets[i];
            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                sun.color = p.sun;
                sun.intensity = p.sunI;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.skyAmb;
            RenderSettings.ambientEquatorColor = p.eqAmb;
            RenderSettings.ambientGroundColor = p.groundAmb;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fog;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            if (!sky && RenderSettings.skybox) { sky = new Material(RenderSettings.skybox); RenderSettings.skybox = sky; }
            if (sky)
            {
                sky.SetColor("_SkyTint", p.skyTint);
                sky.SetFloat("_Exposure", p.exposure);
            }
            if (lanterns != null) foreach (var l in lanterns) if (l) l.intensity = p.lanternI;
            if (glowLights != null) foreach (var l in glowLights) if (l) l.intensity = p.glowI;
            if (volume && volume.profile)
            {
                if (volume.profile.TryGet(out Bloom bloom)) bloom.intensity.value = p.bloom;
                if (volume.profile.TryGet(out ColorAdjustments ca)) ca.postExposure.value = p.postExposure;
            }
            if (probe) probe.RenderProbe();
        }
    }
}

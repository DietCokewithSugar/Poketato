using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Poketato
{
    /// <summary>
    /// Three quality tiers. Each maps to a URP asset (Quality level 0/1/2, created by PoketatoSetup) plus the
    /// scene-side knobs URP assets do not cover: planar reflection, dynamic light budget, DOF and lantern shadows.
    /// </summary>
    public class QualityController : MonoBehaviour
    {
        public static readonly string[] Names = { "低", "中", "高" };
        public static readonly string[] Ids = { "low", "medium", "high" };

        public PlanarReflection reflection;
        public LightPool lights;
        public Volume volume;
        public Light[] shadowLanterns;

        public int Level { get; private set; } = -1;

        public void Apply(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            Level = level;
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            if (reflection)
            {
                reflection.enabled = level >= 1;
                reflection.resolutionScale = level == 2 ? 0.75f : 0.5f;
            }
            if (lights) lights.SetCapacity(level == 0 ? 2 : level == 1 ? 8 : 16);
            if (volume && volume.profile)
            {
                if (volume.profile.TryGet(out DepthOfField dof)) dof.active = level == 2;
                if (volume.profile.TryGet(out Bloom bloom)) bloom.highQualityFiltering.value = level == 2;
            }
            if (shadowLanterns != null)
                foreach (var l in shadowLanterns) if (l) l.shadows = level == 2 ? LightShadows.Soft : LightShadows.None;
            var cam = Camera.main;
            if (cam)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.antialiasing = level == 0 ? AntialiasingMode.None : level == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
        }
    }
}

using UnityEngine;

namespace MuLike.VFX
{
    [CreateAssetMenu(menuName = "MuLike/VFX/LOD Profile", fileName = "VfxLodProfile_")]
    public sealed class VfxLodProfile : ScriptableObject
    {
        [Header("Distance LOD")]
        [Min(1f)] public float highDistance = 8f;
        [Min(1f)] public float midDistance = 18f;

        [Header("Mobile")]
        public bool disableHeavyParticlesOnMidRange = true;

        [Header("Pooling")]
        [Min(1)] public int warmupPerPrefab = 2;
        [Min(1)] public int maxPoolPerPrefab = 24;
    }

    [CreateAssetMenu(menuName = "MuLike/VFX/Skill VFX Profile", fileName = "SkillVfx_")]
    public sealed class SkillVfxProfile : ScriptableObject
    {
        public GameObject mainVfxPrefab;
        public GameObject impactVfxPrefab;
        public GameObject lowSpecVfxPrefab;
        [Min(0.1f)] public float duration = 1.2f;
        public bool heavyParticle = false;
        public VfxLodProfile lodProfile;
    }
}

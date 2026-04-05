using UnityEngine;

namespace MuLike.Skills
{
    /// <summary>
    /// Shared cooldown bucket used by skills of similar category to avoid spam.
    /// </summary>
    [CreateAssetMenu(menuName = "MuLike/Skills/Cooldown Group", fileName = "CDG_")]
    public sealed class SkillCooldownGroup : ScriptableObject
    {
        public string groupId = "default";
        [Min(0f)] public float sharedCooldownSeconds = 0f;
    }
}

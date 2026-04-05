using System;

namespace MuLike.Combat
{
    [Serializable]
    public struct ComboStep
    {
        public int order;
        public int requiredSkillId;
        public float minInputWindow;
        public float maxInputWindow;
        public float effectMultiplier;
        public bool requiresHitFrame;
        public string animationState;
    }
}

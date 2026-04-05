using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Skills
{
    /// <summary>
    /// Context payload used by local UX validation and server-authoritative execution requests.
    /// </summary>
    public struct SkillExecutionContext
    {
        public int requestId;
        public int skillId;
        public int actorEntityId;
        public EntityView actorView;
        public int targetEntityId;
        public EntityView targetView;
        public Vector3 origin;
        public Vector3 direction;
        public Vector3 targetPoint;
        public float clientTimestamp;
        public int upgradeLevel;
    }
}

using System;
using System.Collections.Generic;

namespace MuLike.Server.Game.Skills
{
    public sealed class SkillCooldown
    {
        public int SkillId { get; set; }
        public float RemainingSeconds { get; set; }
    }

    public sealed class SkillCooldownManager
    {
        private readonly Dictionary<int, Dictionary<int, float>> _playerCooldowns = new();

        public void StartCooldown(int playerId, int skillId, float durationSeconds)
        {
            if (!_playerCooldowns.ContainsKey(playerId))
                _playerCooldowns[playerId] = new Dictionary<int, float>();

            _playerCooldowns[playerId][skillId] = durationSeconds;
        }

        public bool IsOnCooldown(int playerId, int skillId)
        {
            if (!_playerCooldowns.TryGetValue(playerId, out var cooldowns))
                return false;

            if (!cooldowns.TryGetValue(skillId, out var remaining))
                return false;

            return remaining > 0f;
        }

        public float GetRemainingCooldown(int playerId, int skillId)
        {
            if (!_playerCooldowns.TryGetValue(playerId, out var cooldowns))
                return 0f;

            cooldowns.TryGetValue(skillId, out var remaining);
            return Math.Max(0f, remaining);
        }

        public void Update(float deltaTime)
        {
            var playerIds = new List<int>(_playerCooldowns.Keys);
            
            foreach (int playerId in playerIds)
            {
                var cooldowns = _playerCooldowns[playerId];
                var skillsToRemove = new List<int>();

                foreach (var kvp in cooldowns)
                {
                    int skillId = kvp.Key;
                    float remaining = kvp.Value - deltaTime;

                    if (remaining <= 0f)
                        skillsToRemove.Add(skillId);
                    else
                        cooldowns[skillId] = remaining;
                }

                foreach (int skillId in skillsToRemove)
                    cooldowns.Remove(skillId);

                if (cooldowns.Count == 0)
                    _playerCooldowns.Remove(playerId);
            }
        }

        public void ClearPlayerCooldowns(int playerId)
        {
            _playerCooldowns.Remove(playerId);
        }
    }
}

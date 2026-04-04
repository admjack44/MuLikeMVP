namespace MuLike.Systems
{
    /// <summary>
    /// Holds the local player's current stat values as reported by the server.
    /// </summary>
    public class StatsClientSystem
    {
        public struct PlayerStats
        {
            public int Level;
            public long Experience;
            public long ExperienceNextLevel;
            public int HpCurrent;
            public int HpMax;
            public int ManaCurrent;
            public int ManaMax;
            public int Strength;
            public int Agility;
            public int Vitality;
            public int Energy;
            public int Command;
            public int MinDamage;
            public int MaxDamage;
            public int Defense;
        }

        public PlayerStats Stats { get; private set; }

        public event System.Action OnStatsChanged;

        public void ApplySnapshot(PlayerStats stats)
        {
            Stats = stats;
            OnStatsChanged?.Invoke();
        }

        public void UpdateHp(int current, int max)
        {
            var s = Stats;
            s.HpCurrent = current;
            s.HpMax = max;
            Stats = s;
            OnStatsChanged?.Invoke();
        }

        public void UpdateMana(int current, int max)
        {
            var s = Stats;
            s.ManaCurrent = current;
            s.ManaMax = max;
            Stats = s;
            OnStatsChanged?.Invoke();
        }

        public void UpdateExperience(long exp, long nextLevel)
        {
            var s = Stats;
            s.Experience = exp;
            s.ExperienceNextLevel = nextLevel;
            Stats = s;
            OnStatsChanged?.Invoke();
        }
    }
}

using System;

namespace MuLike.Systems
{
    /// <summary>
    /// Holds client-side player stats and applies server snapshots/deltas.
    /// </summary>
    public class StatsClientSystem
    {
        public enum CharacterClass
        {
            Unknown,
            DarkWizard,
            DarkKnight,
            FairyElf,
            MagicGladiator,
            DarkLord,
            Summoner,
            RageFighter
        }

        public struct ResourceStat
        {
            public int Current;
            public int Max;
        }

        public struct PrimaryStats
        {
            public CharacterClass Class;
            public int Level;
            public long Experience;
            public long ExperienceNextLevel;
            public int Strength;
            public int Agility;
            public int Vitality;
            public int Energy;
            public int Command;
        }

        public struct CombatStats
        {
            public int DamageMin;
            public int DamageMax;
            public int Defense;
            public int AttackSpeed;
        }

        public struct ResourceStats
        {
            public ResourceStat Hp;
            public ResourceStat Mana;
            public ResourceStat Shield;
            public ResourceStat Stamina;
        }

        public struct PlayerStatsSnapshot
        {
            public PrimaryStats Primary;
            public ResourceStats Resources;
            public CombatStats Combat;
        }

        public struct PlayerStatsDelta
        {
            public bool HasClass;
            public CharacterClass Class;

            public bool HasLevel;
            public int Level;
            public bool HasExperience;
            public long Experience;
            public bool HasExperienceNextLevel;
            public long ExperienceNextLevel;

            public bool HasStrength;
            public int Strength;
            public bool HasAgility;
            public int Agility;
            public bool HasVitality;
            public int Vitality;
            public bool HasEnergy;
            public int Energy;
            public bool HasCommand;
            public int Command;

            public bool HasHp;
            public int HpCurrent;
            public int HpMax;
            public bool HasMana;
            public int ManaCurrent;
            public int ManaMax;
            public bool HasShield;
            public int ShieldCurrent;
            public int ShieldMax;
            public bool HasStamina;
            public int StaminaCurrent;
            public int StaminaMax;

            public bool HasDamage;
            public int DamageMin;
            public int DamageMax;
            public bool HasDefense;
            public int Defense;
            public bool HasAttackSpeed;
            public int AttackSpeed;
        }

        /// <summary>
        /// Legacy model kept for compatibility with existing callers.
        /// </summary>
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
            public int ShieldCurrent;
            public int ShieldMax;
            public int StaminaCurrent;
            public int StaminaMax;
            public int AttackSpeed;
            public CharacterClass Class;
        }

        private readonly IStatsDerivedCalculator _derivedCalculator;

        public PlayerStatsSnapshot Snapshot { get; private set; }
        public PlayerStats Stats => ToLegacy(Snapshot);

        public event Action OnStatsChanged;
        public event Action<PlayerStatsSnapshot> OnStatsSnapshotApplied;
        public event Action<PlayerStatsDelta> OnStatsDeltaApplied;
        public event Action<PrimaryStats> OnPrimaryStatsChanged;
        public event Action<ResourceStats> OnResourcesChanged;
        public event Action<CombatStats> OnCombatStatsChanged;
        public event Action<int, int> OnLevelChanged;

        public StatsClientSystem(IStatsDerivedCalculator derivedCalculator = null)
        {
            _derivedCalculator = derivedCalculator;
            Snapshot = Normalize(default);
        }

        public void ApplySnapshot(PlayerStatsSnapshot snapshot)
        {
            PlayerStatsSnapshot previous = Snapshot;

            Snapshot = Normalize(snapshot);
            RecalculateDerived();

            EmitChanges(previous, Snapshot);
            OnStatsSnapshotApplied?.Invoke(Snapshot);
            OnStatsChanged?.Invoke();
        }

        public void ApplySnapshot(PlayerStats stats)
        {
            ApplySnapshot(FromLegacy(stats));
        }

        public void ApplyDelta(PlayerStatsDelta delta)
        {
            PlayerStatsSnapshot previous = Snapshot;
            PlayerStatsSnapshot updated = Snapshot;

            if (delta.HasClass) updated.Primary.Class = delta.Class;
            if (delta.HasLevel) updated.Primary.Level = delta.Level;
            if (delta.HasExperience) updated.Primary.Experience = delta.Experience;
            if (delta.HasExperienceNextLevel) updated.Primary.ExperienceNextLevel = delta.ExperienceNextLevel;

            if (delta.HasStrength) updated.Primary.Strength = delta.Strength;
            if (delta.HasAgility) updated.Primary.Agility = delta.Agility;
            if (delta.HasVitality) updated.Primary.Vitality = delta.Vitality;
            if (delta.HasEnergy) updated.Primary.Energy = delta.Energy;
            if (delta.HasCommand) updated.Primary.Command = delta.Command;

            if (delta.HasHp)
            {
                updated.Resources.Hp.Current = delta.HpCurrent;
                updated.Resources.Hp.Max = delta.HpMax;
            }

            if (delta.HasMana)
            {
                updated.Resources.Mana.Current = delta.ManaCurrent;
                updated.Resources.Mana.Max = delta.ManaMax;
            }

            if (delta.HasShield)
            {
                updated.Resources.Shield.Current = delta.ShieldCurrent;
                updated.Resources.Shield.Max = delta.ShieldMax;
            }

            if (delta.HasStamina)
            {
                updated.Resources.Stamina.Current = delta.StaminaCurrent;
                updated.Resources.Stamina.Max = delta.StaminaMax;
            }

            if (delta.HasDamage)
            {
                updated.Combat.DamageMin = delta.DamageMin;
                updated.Combat.DamageMax = delta.DamageMax;
            }

            if (delta.HasDefense) updated.Combat.Defense = delta.Defense;
            if (delta.HasAttackSpeed) updated.Combat.AttackSpeed = delta.AttackSpeed;

            Snapshot = Normalize(updated);
            RecalculateDerived();

            EmitChanges(previous, Snapshot);
            OnStatsDeltaApplied?.Invoke(delta);
            OnStatsChanged?.Invoke();
        }

        public void UpdateHp(int current, int max)
        {
            ApplyDelta(new PlayerStatsDelta
            {
                HasHp = true,
                HpCurrent = current,
                HpMax = max
            });
        }

        public void UpdateMana(int current, int max)
        {
            ApplyDelta(new PlayerStatsDelta
            {
                HasMana = true,
                ManaCurrent = current,
                ManaMax = max
            });
        }

        public void UpdateExperience(long exp, long nextLevel)
        {
            ApplyDelta(new PlayerStatsDelta
            {
                HasExperience = true,
                Experience = exp,
                HasExperienceNextLevel = true,
                ExperienceNextLevel = nextLevel
            });
        }

        private void RecalculateDerived()
        {
            if (_derivedCalculator == null) return;
            PlayerStatsSnapshot snapshot = Snapshot;
            _derivedCalculator.Recalculate(ref snapshot);
            Snapshot = Normalize(snapshot);
        }

        private static PlayerStatsSnapshot Normalize(PlayerStatsSnapshot snapshot)
        {
            if (snapshot.Primary.Level < 1) snapshot.Primary.Level = 1;
            if (snapshot.Primary.Experience < 0) snapshot.Primary.Experience = 0;
            if (snapshot.Primary.ExperienceNextLevel < 0) snapshot.Primary.ExperienceNextLevel = 0;

            if (snapshot.Primary.Strength < 0) snapshot.Primary.Strength = 0;
            if (snapshot.Primary.Agility < 0) snapshot.Primary.Agility = 0;
            if (snapshot.Primary.Vitality < 0) snapshot.Primary.Vitality = 0;
            if (snapshot.Primary.Energy < 0) snapshot.Primary.Energy = 0;

            if (snapshot.Primary.Class != CharacterClass.DarkLord)
                snapshot.Primary.Command = 0;
            else if (snapshot.Primary.Command < 0)
                snapshot.Primary.Command = 0;

            snapshot.Resources.Hp = NormalizeResource(snapshot.Resources.Hp);
            snapshot.Resources.Mana = NormalizeResource(snapshot.Resources.Mana);
            snapshot.Resources.Shield = NormalizeResource(snapshot.Resources.Shield);
            snapshot.Resources.Stamina = NormalizeResource(snapshot.Resources.Stamina);

            if (snapshot.Combat.DamageMin < 0) snapshot.Combat.DamageMin = 0;
            if (snapshot.Combat.DamageMax < snapshot.Combat.DamageMin)
                snapshot.Combat.DamageMax = snapshot.Combat.DamageMin;
            if (snapshot.Combat.Defense < 0) snapshot.Combat.Defense = 0;
            if (snapshot.Combat.AttackSpeed < 0) snapshot.Combat.AttackSpeed = 0;

            return snapshot;
        }

        private static ResourceStat NormalizeResource(ResourceStat value)
        {
            if (value.Max < 0) value.Max = 0;
            if (value.Current < 0) value.Current = 0;
            if (value.Current > value.Max) value.Current = value.Max;
            return value;
        }

        private void EmitChanges(PlayerStatsSnapshot previous, PlayerStatsSnapshot current)
        {
            if (previous.Primary.Level != current.Primary.Level)
                OnLevelChanged?.Invoke(previous.Primary.Level, current.Primary.Level);

            if (!ArePrimaryEqual(previous.Primary, current.Primary))
                OnPrimaryStatsChanged?.Invoke(current.Primary);

            if (!AreResourcesEqual(previous.Resources, current.Resources))
                OnResourcesChanged?.Invoke(current.Resources);

            if (!AreCombatEqual(previous.Combat, current.Combat))
                OnCombatStatsChanged?.Invoke(current.Combat);
        }

        private static bool ArePrimaryEqual(PrimaryStats left, PrimaryStats right)
        {
            return left.Class == right.Class
                && left.Level == right.Level
                && left.Experience == right.Experience
                && left.ExperienceNextLevel == right.ExperienceNextLevel
                && left.Strength == right.Strength
                && left.Agility == right.Agility
                && left.Vitality == right.Vitality
                && left.Energy == right.Energy
                && left.Command == right.Command;
        }

        private static bool AreResourcesEqual(ResourceStats left, ResourceStats right)
        {
            return AreResourceEqual(left.Hp, right.Hp)
                && AreResourceEqual(left.Mana, right.Mana)
                && AreResourceEqual(left.Shield, right.Shield)
                && AreResourceEqual(left.Stamina, right.Stamina);
        }

        private static bool AreResourceEqual(ResourceStat left, ResourceStat right)
        {
            return left.Current == right.Current && left.Max == right.Max;
        }

        private static bool AreCombatEqual(CombatStats left, CombatStats right)
        {
            return left.DamageMin == right.DamageMin
                && left.DamageMax == right.DamageMax
                && left.Defense == right.Defense
                && left.AttackSpeed == right.AttackSpeed;
        }

        private static PlayerStats ToLegacy(PlayerStatsSnapshot snapshot)
        {
            return new PlayerStats
            {
                Class = snapshot.Primary.Class,
                Level = snapshot.Primary.Level,
                Experience = snapshot.Primary.Experience,
                ExperienceNextLevel = snapshot.Primary.ExperienceNextLevel,
                Strength = snapshot.Primary.Strength,
                Agility = snapshot.Primary.Agility,
                Vitality = snapshot.Primary.Vitality,
                Energy = snapshot.Primary.Energy,
                Command = snapshot.Primary.Command,
                HpCurrent = snapshot.Resources.Hp.Current,
                HpMax = snapshot.Resources.Hp.Max,
                ManaCurrent = snapshot.Resources.Mana.Current,
                ManaMax = snapshot.Resources.Mana.Max,
                ShieldCurrent = snapshot.Resources.Shield.Current,
                ShieldMax = snapshot.Resources.Shield.Max,
                StaminaCurrent = snapshot.Resources.Stamina.Current,
                StaminaMax = snapshot.Resources.Stamina.Max,
                MinDamage = snapshot.Combat.DamageMin,
                MaxDamage = snapshot.Combat.DamageMax,
                Defense = snapshot.Combat.Defense,
                AttackSpeed = snapshot.Combat.AttackSpeed
            };
        }

        private static PlayerStatsSnapshot FromLegacy(PlayerStats stats)
        {
            return new PlayerStatsSnapshot
            {
                Primary = new PrimaryStats
                {
                    Class = stats.Class,
                    Level = stats.Level,
                    Experience = stats.Experience,
                    ExperienceNextLevel = stats.ExperienceNextLevel,
                    Strength = stats.Strength,
                    Agility = stats.Agility,
                    Vitality = stats.Vitality,
                    Energy = stats.Energy,
                    Command = stats.Command
                },
                Resources = new ResourceStats
                {
                    Hp = new ResourceStat { Current = stats.HpCurrent, Max = stats.HpMax },
                    Mana = new ResourceStat { Current = stats.ManaCurrent, Max = stats.ManaMax },
                    Shield = new ResourceStat { Current = stats.ShieldCurrent, Max = stats.ShieldMax },
                    Stamina = new ResourceStat { Current = stats.StaminaCurrent, Max = stats.StaminaMax }
                },
                Combat = new CombatStats
                {
                    DamageMin = stats.MinDamage,
                    DamageMax = stats.MaxDamage,
                    Defense = stats.Defense,
                    AttackSpeed = stats.AttackSpeed
                }
            };
        }
    }
}

namespace MuLike.Systems
{
    /// <summary>
    /// Extension point for derived stat calculations after snapshots/deltas are applied.
    /// Keep formulas server-authoritative and mirror here only when needed for UI feedback.
    /// </summary>
    public interface IStatsDerivedCalculator
    {
        void Recalculate(ref StatsClientSystem.PlayerStatsSnapshot snapshot);
    }
}

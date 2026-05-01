using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

/// <summary>
/// Calculates effective ownership metrics from a set of manager live snapshots.
/// </summary>
public interface IEffectiveOwnershipCalculator
{
    /// <summary>
    /// Builds league-level EO rows and optional user-specific rank impact values.
    /// </summary>
    /// <param name="managerScores">Per-manager live snapshots for a single league and event.</param>
    /// <param name="selectedManagerId">Optional manager used to compute user-specific multiplier impact.</param>
    IReadOnlyList<LeagueEffectiveOwnershipEntryDto> Calculate(
        IReadOnlyList<ManagerLiveDto> managerScores,
        int? selectedManagerId);
}

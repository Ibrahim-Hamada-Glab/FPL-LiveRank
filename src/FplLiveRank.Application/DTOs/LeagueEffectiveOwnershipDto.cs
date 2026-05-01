namespace FplLiveRank.Application.DTOs;

public sealed record LeagueEffectiveOwnershipDto(
    int LeagueId,
    string LeagueName,
    int EventId,
    int ManagerCount,
    int? SelectedManagerId,
    IReadOnlyList<LeagueEffectiveOwnershipEntryDto> Players,
    DateTimeOffset CalculatedAtUtc);

public sealed record LeagueEffectiveOwnershipEntryDto(
    int ElementId,
    string WebName,
    int TeamId,
    int ElementType,
    decimal OwnershipPercent,
    decimal CaptaincyPercent,
    decimal EffectiveOwnershipPercent,
    int UserMultiplier,
    decimal RankImpactPerPoint,
    string ImpactExplanation);

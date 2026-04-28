using FplLiveRank.Domain.Enums;

namespace FplLiveRank.Application.DTOs;

public sealed record ManagerLiveDto(
    int ManagerId,
    int EventId,
    string PlayerName,
    string TeamName,
    int RawLivePoints,
    int TransferCost,
    int LivePointsAfterHits,
    int PreviousTotal,
    int LiveSeasonTotal,
    ChipType ActiveChip,
    int? CaptainElementId,
    int? ViceCaptainElementId,
    CaptaincyStatus CaptaincyStatus,
    int? EffectiveCaptainElementId,
    IReadOnlyList<SubstitutionDto> AutoSubs,
    IReadOnlyList<int> BlockedStarterElementIds,
    bool AutoSubProjectionFinal,
    IReadOnlyList<ManagerLivePickDto> Picks,
    DateTimeOffset CalculatedAtUtc);

public sealed record SubstitutionDto(int OutElementId, int InElementId);

public sealed record ManagerLivePickDto(
    int ElementId,
    string WebName,
    int TeamId,
    int ElementType,
    int Position,
    int Multiplier,
    bool IsCaptain,
    bool IsViceCaptain,
    int LiveTotalPoints,
    int Minutes,
    int Bonus,
    int ContributedPoints);

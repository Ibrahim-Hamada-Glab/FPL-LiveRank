using FplLiveRank.Domain.Enums;

namespace FplLiveRank.Application.DTOs;

public sealed record LeagueLiveRankDto(
    int LeagueId,
    string LeagueName,
    int EventId,
    int ManagerCount,
    IReadOnlyList<LeagueLiveRankEntryDto> Standings,
    DateTimeOffset CalculatedAtUtc);

public sealed record LeagueLiveRankEntryDto(
    int ManagerId,
    string EntryName,
    string PlayerName,
    int OfficialRank,
    int LiveRank,
    int RankChange,
    int OfficialTotal,
    int LiveTotal,
    int LiveGwPoints,
    int TransferCost,
    ChipType ActiveChip,
    int? CaptainElementId,
    string? CaptainName,
    IReadOnlyList<SubstitutionDto> AutoSubs,
    bool AutoSubProjectionFinal,
    bool IsTiedOnLiveTotal,
    int? PreviousLiveRank = null,
    int RankDeltaSincePreviousSnapshot = 0,
    string? RankChangeExplanation = null);

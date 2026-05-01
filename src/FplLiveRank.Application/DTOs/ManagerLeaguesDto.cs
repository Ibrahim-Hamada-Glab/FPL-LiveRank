namespace FplLiveRank.Application.DTOs;

public sealed record ManagerLeaguesDto(
    int ManagerId,
    string PlayerName,
    string TeamName,
    IReadOnlyList<ManagerLeagueDto> ClassicLeagues,
    DateTimeOffset SyncedAtUtc);

public sealed record ManagerLeagueDto(
    int Id,
    string Name,
    string? ShortName,
    string LeagueType,
    string Scoring,
    int? Rank,
    int? MaxEntries,
    bool EntryCanLeave,
    bool EntryCanAdmin,
    bool EntryCanInvite,
    bool IsSystemLeague);

namespace FplLiveRank.Application.DTOs;

public sealed record CurrentEventDto(
    int Id,
    string Name,
    DateTimeOffset? DeadlineTime,
    bool IsCurrent,
    bool IsNext,
    bool IsFinished,
    bool IsDataChecked);

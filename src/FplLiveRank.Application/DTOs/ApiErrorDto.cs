namespace FplLiveRank.Application.DTOs;

public sealed record ApiErrorDto(
    string Code,
    string Message,
    string? Details,
    string? TraceId,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

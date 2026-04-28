using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Infrastructure.External.Fpl;

namespace FplLiveRank.Api.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteAsync(ctx, ex).ConfigureAwait(false);
        }
    }

    private async Task WriteAsync(HttpContext ctx, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? ctx.TraceIdentifier;
        var (status, dto) = MapException(ex, traceId);

        if (status >= HttpStatusCode.InternalServerError)
        {
            _logger.LogError(ex, "Unhandled exception (TraceId={TraceId})", traceId);
        }
        else
        {
            _logger.LogWarning(ex, "Request failed: {Code} (TraceId={TraceId})", dto.Code, traceId);
        }

        ctx.Response.Clear();
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(dto, JsonOptions)).ConfigureAwait(false);
    }

    private static (HttpStatusCode Status, ApiErrorDto Dto) MapException(Exception ex, string traceId) => ex switch
    {
        NotFoundException nf => (HttpStatusCode.NotFound,
            new ApiErrorDto(nf.Code, nf.Message, nf.Details, traceId)),

        Application.Errors.ValidationException ve => (HttpStatusCode.BadRequest,
            new ApiErrorDto(ve.Code, ve.Message, ve.Details, traceId, ve.Errors)),

        FluentValidation.ValidationException fve => (HttpStatusCode.BadRequest,
            new ApiErrorDto("VALIDATION_FAILED", "Request validation failed.", null, traceId,
                fve.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))),

        FplApiException fpl => (
            fpl.StatusCode == HttpStatusCode.NotFound ? HttpStatusCode.NotFound : HttpStatusCode.BadGateway,
            new ApiErrorDto(
                fpl.StatusCode == HttpStatusCode.NotFound ? "FPL_RESOURCE_NOT_FOUND" : "FPL_API_UNAVAILABLE",
                fpl.StatusCode == HttpStatusCode.NotFound
                    ? "The requested FPL resource was not found."
                    : "Unable to fetch data from Fantasy Premier League right now.",
                fpl.RequestPath, traceId)),

        ExternalServiceException ext => (HttpStatusCode.BadGateway,
            new ApiErrorDto(ext.Code, ext.Message, ext.Details, traceId)),

        AppException app => (HttpStatusCode.BadRequest,
            new ApiErrorDto(app.Code, app.Message, app.Details, traceId)),

        OperationCanceledException => (HttpStatusCode.RequestTimeout,
            new ApiErrorDto("REQUEST_CANCELLED", "Request was cancelled.", null, traceId)),

        _ => (HttpStatusCode.InternalServerError,
            new ApiErrorDto("INTERNAL_ERROR", "An unexpected error occurred.", null, traceId))
    };
}

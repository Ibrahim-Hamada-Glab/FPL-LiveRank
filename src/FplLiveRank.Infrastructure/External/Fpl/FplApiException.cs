using System.Net;

namespace FplLiveRank.Infrastructure.External.Fpl;

public sealed class FplApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? RequestPath { get; }

    public FplApiException(string message, string? requestPath = null, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        RequestPath = requestPath;
        StatusCode = statusCode;
    }
}

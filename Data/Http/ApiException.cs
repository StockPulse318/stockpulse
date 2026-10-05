using System.Net;

namespace WarehouseInventory.Data.Http;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ErrorCode { get; }
    public string DisplayMessage { get; }

    public ApiException(HttpStatusCode statusCode, string? errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        DisplayMessage = message;
    }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
}

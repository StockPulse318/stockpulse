using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarehouseInventory.Data.Dtos;

public sealed class ApiErrorDetail
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public sealed class ApiErrorDto
{
    [JsonPropertyName("error")]
    public JsonElement? ErrorElement { get; set; }

    [JsonPropertyName("message")]
    public string? DirectMessage { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    public (string? Code, string Message) ExtractMessage(string fallbackReason)
    {
        if (ErrorElement.HasValue)
        {
            var el = ErrorElement.Value;
            if (el.ValueKind == JsonValueKind.Object)
            {
                string? code = null;
                string? message = null;

                if (el.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    code = c.GetString();
                }
                if (el.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    message = m.GetString();
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return (code, message);
                }
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    return (null, str);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(DirectMessage))
        {
            return (null, DirectMessage);
        }

        if (!string.IsNullOrWhiteSpace(Title))
        {
            return (null, Title);
        }

        return (null, fallbackReason);
    }
}

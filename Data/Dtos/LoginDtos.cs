using System.Text.Json.Serialization;

namespace WarehouseInventory.Data.Dtos;

public sealed class LoginRequestDto
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponseDto
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("assignedBranch")]
    public string? AssignedBranch { get; set; }
}

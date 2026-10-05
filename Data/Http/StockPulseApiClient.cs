using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WarehouseInventory.Data.Dtos;
using WarehouseInventory.Data.State;

namespace WarehouseInventory.Data.Http;

public sealed class StockPulseApiClient : IStockPulseApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StockPulseApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public async Task<LoginResponseDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var requestDto = new LoginRequestDto
        {
            Username = username,
            Password = password
        };

        using var response = await SendJsonAsync(HttpMethod.Post, "/api/Auth/login", requestDto, includeAuth: false, cancellationToken);
        var loginResponse = await DeserializeAsync<LoginResponseDto>(response, cancellationToken);

        if (loginResponse == null || string.IsNullOrWhiteSpace(loginResponse.Token))
        {
            throw new ApiException(HttpStatusCode.OK, null, "Authentication response was missing a valid token.");
        }

        _tokenStorage.SetToken(loginResponse.Token);
        return loginResponse;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/Products?limit=100", includeAuth: true, cancellationToken);
        return await DeserializeProductListAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string name, CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(name.Trim());
        if (string.IsNullOrWhiteSpace(encoded))
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/Products?limit=100", includeAuth: true, cancellationToken);
            return await DeserializeProductListAsync(response, cancellationToken);
        }

        // New backend uses ?q= on the collection; legacy used /search?name=.
        try
        {
            using var response = await SendAsync(HttpMethod.Get, $"/api/Products?q={encoded}&limit=100", includeAuth: true, cancellationToken);
            return await DeserializeProductListAsync(response, cancellationToken);
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            using var response = await SendAsync(HttpMethod.Get, $"/api/Products/search?name={encoded}", includeAuth: true, cancellationToken);
            return await DeserializeProductListAsync(response, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        // Legacy path first (keeps existing unit tests asserting this path green),
        // then the new-backend path.
        try
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/Products/low-stock", includeAuth: true, cancellationToken);
            return await DeserializeProductListAsync(response, cancellationToken);
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/alerts/low-stock", includeAuth: true, cancellationToken);
            return await DeserializeProductListAsync(response, cancellationToken);
        }
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Get, $"/api/Products/{id}", includeAuth: true, cancellationToken);
            return await DeserializeAsync<ProductDto>(response, cancellationToken);
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            return null;
        }
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "/api/Products", request, includeAuth: true, cancellationToken);
        var created = await DeserializeAsync<ProductDto>(response, cancellationToken);
        return created ?? new ProductDto
        {
            ProductCode = request.ProductCode,
            ProductName = request.Name,
            CategoryId = request.CategoryId,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            ReorderLevel = request.ReorderLevel
        };
    }

    public async Task UpdateProductAsync(int id, UpdateProductRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"/api/Products/{id}", request, includeAuth: true, cancellationToken);
    }

    public async Task DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"/api/Products/{id}", includeAuth: true, cancellationToken);
    }

    public async Task StockInAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var request = new StockMovementRequestDto { Amount = quantity };
        using var response = await SendJsonAsync(HttpMethod.Post, $"/api/products/{productId}/stock-in", request, includeAuth: true, cancellationToken);
    }

    public async Task StockOutAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var request = new StockMovementRequestDto { Amount = quantity };
        using var response = await SendJsonAsync(HttpMethod.Post, $"/api/products/{productId}/stock-out", request, includeAuth: true, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // New backend serves /api/categories (array of {id,name} or strings, possibly enveloped).
        // Legacy path /api/products/categories is kept as fallback.
        try
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/categories", includeAuth: true, cancellationToken);
            var categories = await DeserializeCategoriesAsync(response, cancellationToken);
            if (categories.Count > 0)
            {
                return categories;
            }
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            // Fall through to legacy path below.
        }

        try
        {
            using var legacy = await SendAsync(HttpMethod.Get, "/api/products/categories", includeAuth: true, cancellationToken);
            var categories = await DeserializeCategoriesAsync(legacy, cancellationToken);
            return categories;
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            // Backend mismatch: neither categories endpoint exists.
            // Return empty list so caller can fall back to distinct categories from product catalog.
            return Array.Empty<CategoryDto>();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, bool includeAuth, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        request.Headers.Pragma.ParseAdd("no-cache");

        if (includeAuth && !string.IsNullOrWhiteSpace(_tokenStorage.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStorage.Token);
        }

        return await ExecuteRequestAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendJsonAsync<T>(HttpMethod method, string path, T body, bool includeAuth, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        request.Headers.Pragma.ParseAdd("no-cache");

        if (includeAuth && !string.IsNullOrWhiteSpace(_tokenStorage.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStorage.Token);
        }

        var json = JsonSerializer.Serialize(body, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return await ExecuteRequestAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> ExecuteRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, "NETWORK_ERROR",
                "Cannot connect to the warehouse server. Please check your network connection and server settings.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException(HttpStatusCode.RequestTimeout, "TIMEOUT",
                "The server took too long to respond. Please try again.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var (errorCode, errorMessage) = await ExtractErrorAsync(response, cancellationToken);
        response.Dispose();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiException(response.StatusCode, errorCode ?? "UNAUTHORIZED", errorMessage);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new ApiException(response.StatusCode, errorCode ?? "FORBIDDEN",
                string.IsNullOrWhiteSpace(errorMessage) || errorMessage.StartsWith("Server returned")
                    ? "Access denied. Your role is not authorized to perform this operation."
                    : errorMessage);
        }

        throw new ApiException(response.StatusCode, errorCode, errorMessage);
    }

    private static async Task<(string? Code, string Message)> ExtractErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(content))
            {
                var errorDto = JsonSerializer.Deserialize<ApiErrorDto>(content, JsonOptions);
                if (errorDto != null)
                {
                    return errorDto.ExtractMessage(response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}");
                }
            }
        }
        catch
        {
            // Ignore error extraction parse failure
        }

        return (null, $"Server returned error {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    private static async Task<IReadOnlyList<ProductDto>> DeserializeProductListAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<ProductDto>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Case 1: bare array [...]
            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<ProductDto>>(json, JsonOptions) ?? new List<ProductDto>();
            }

            // Case 2: enveloped object — look for the first array property
            // (data / items / products / results / rows / ...), one level deep.
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var arrayElement in FindNestedArrays(root))
                {
                    var list = JsonSerializer.Deserialize<List<ProductDto>>(arrayElement.GetRawText(), JsonOptions);
                    if (list != null)
                    {
                        return list;
                    }
                }

                // Case 3: object is a single product (has product identifiers) — wrap it.
                if (LooksLikeProduct(root))
                {
                    var single = JsonSerializer.Deserialize<ProductDto>(json, JsonOptions);
                    if (single != null)
                    {
                        return new List<ProductDto> { single };
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new ApiException(response.StatusCode, "INVALID_RESPONSE",
                $"Inventory response was not in expected format. Server returned: {Truncate(json, 500)}", ex);
        }

        throw new ApiException(response.StatusCode, "INVALID_RESPONSE",
            $"Inventory response was not in expected format. Server returned: {Truncate(json, 500)}");
    }

    private static async Task<IReadOnlyList<string>> DeserializeStringListAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Bare string array ["a","b"]
        if (root.ValueKind == JsonValueKind.Array)
        {
            return ParseCategoryArray(root);
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var arrayElement in FindNestedArrays(root))
            {
                var parsed = ParseCategoryArray(arrayElement);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }

        return Array.Empty<string>();
    }

    private static async Task<IReadOnlyList<CategoryDto>> DeserializeCategoriesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<CategoryDto>();

        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : FindNestedArrays(doc.RootElement).FirstOrDefault();
        if (array.ValueKind != JsonValueKind.Array) return Array.Empty<CategoryDto>();

        var result = new List<CategoryDto>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var id = GetInt(item, "id");
                var name = GetString(item, "name");
                if (id > 0 && !string.IsNullOrWhiteSpace(name)) result.Add(new CategoryDto { Id = id, Name = name });
            }
        }
        return result;
    }

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<JsonElement> FindNestedArrays(JsonElement obj)
    {
        // Direct array properties first (data, items, products, results, rows, ...)
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                yield return prop.Value;
            }
        }

        // One level deeper: { "data": { "items": [...] } }
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var inner in prop.Value.EnumerateObject())
                {
                    if (inner.Value.ValueKind == JsonValueKind.Array)
                    {
                        yield return inner.Value;
                    }
                }
            }
        }
    }

    private static List<string> ParseCategoryArray(JsonElement array)
    {
        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    result.Add(s!);
                }
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                // New backend shape: { "id": 3, "name": "Beverages" }
                if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    var s = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        result.Add(s!);
                        continue;
                    }
                }
                foreach (var p in item.EnumerateObject())
                {
                    if (string.Equals(p.Name, "name", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                    {
                        var s = p.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            result.Add(s!);
                        }
                        break;
                    }
                }
            }
        }
        return result;
    }

    private static bool LooksLikeProduct(JsonElement obj)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (p.Name.Equals("productName", StringComparison.OrdinalIgnoreCase)
                || p.Name.Equals("productID", StringComparison.OrdinalIgnoreCase)
                || p.Name.Equals("product_id", StringComparison.OrdinalIgnoreCase)
                || (p.Name.Equals("name", StringComparison.OrdinalIgnoreCase) && obj.TryGetProperty("quantity", out _)))
            {
                return true;
            }
        }
        return false;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (stream.Length == 0 && response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }
}

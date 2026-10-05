using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WarehouseInventory.Models;

namespace WarehouseInventory.Services;

public class StockPulseApiClient
{
    private readonly HttpClient _http;
    private string _baseUrl;
    private string? _token;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            _baseUrl = value.Trim().TrimEnd('/');
            _http.BaseAddress = new Uri(_baseUrl);
        }
    }

    public string? Token => _token;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    public StockPulseApiClient(string? baseUrl = null)
    {
        _baseUrl = (baseUrl ?? StockPulseConfig.DefaultBaseUrl).Trim().TrimEnd('/');
        _http = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetToken(string token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearToken()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        var payload = JsonSerializer.Serialize(new { username, password });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/api/auth/login", content).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InventoryException($"Could not connect to backend server at {_baseUrl}: {ex.Message}");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await ExtractErrorMessageAsync(response).ConfigureAwait(false);
            throw new InventoryException(err);
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var loginResponse = JsonSerializer.Deserialize<ApiLoginResponse>(json, JsonOptions);

        if (loginResponse == null || string.IsNullOrEmpty(loginResponse.Token))
        {
            throw new InventoryException("Invalid response format received from login server.");
        }

        SetToken(loginResponse.Token);

        var role = loginResponse.Role ?? "";
        UserRole userRole;
        if (string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase))
            userRole = UserRole.Administrator;
        else if (string.Equals(role, "Warehouse Manager", StringComparison.OrdinalIgnoreCase))
            userRole = UserRole.Manager;
        else
            userRole = UserRole.Clerk;

        var fullName = !string.IsNullOrWhiteSpace(loginResponse.FullName) ? loginResponse.FullName : loginResponse.Username;
        var assignedBranch = !string.IsNullOrWhiteSpace(loginResponse.AssignedBranch) ? loginResponse.AssignedBranch : "All Branches";

        return new User
        {
            Username = loginResponse.Username,
            FullName = fullName,
            AssignedBranch = assignedBranch,
            Role = userRole
        };
    }

    public async Task<IReadOnlyList<UserInfo>> GetUsersAsync()
    {
        EnsureAuthenticated();

        using var response = await _http.GetAsync("/api/auth/users").ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var users = JsonSerializer.Deserialize<List<UserInfo>>(json, JsonOptions) ?? new();
        return users.OrderBy(u => u.Username).ToList();
    }

    public async Task RegisterUserAsync(
        string username,
        string password,
        string role,
        string? fullName = null,
        string? assignedBranch = null)
    {
        EnsureAuthenticated();

        var payload = JsonSerializer.Serialize(new
        {
            username = username.Trim(),
            password = password,
            role = role.Trim(),
            fullName = fullName?.Trim() ?? string.Empty,
            assignedBranch = string.IsNullOrWhiteSpace(assignedBranch) ? "All Branches" : assignedBranch.Trim()
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("/api/auth/users", content).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task UpdateUserAsync(
        string username,
        string? fullName,
        string? role,
        string? assignedBranch,
        bool? isActive)
    {
        EnsureAuthenticated();

        var payload = JsonSerializer.Serialize(new
        {
            fullName,
            role,
            assignedBranch,
            isActive
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync($"/api/auth/users/{Uri.EscapeDataString(username.Trim())}", content).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(string username, string newPassword)
    {
        EnsureAuthenticated();

        var payload = JsonSerializer.Serialize(new
        {
            newPassword = newPassword
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"/api/auth/users/{Uri.EscapeDataString(username.Trim())}/reset-password", content).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task DeleteUserAsync(string username)
    {
        EnsureAuthenticated();

        using var response = await _http.DeleteAsync($"/api/auth/users/{Uri.EscapeDataString(username.Trim())}").ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Product>> GetAllProductsAsync()
    {
        EnsureAuthenticated();

        using var response = await _http.GetAsync("/api/products").ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var apiProducts = JsonSerializer.Deserialize<List<ApiProductResponse>>(json, JsonOptions) ?? new();

        return apiProducts.Select(MapToDomainProduct).OrderBy(p => ParseId(p.Id)).ToList();
    }

    public async Task<IReadOnlyList<Product>> SearchProductsAsync(string searchText)
    {
        var all = await GetAllProductsAsync().ConfigureAwait(false);
        var term = (searchText ?? "").Trim();
        if (term.Length == 0)
            return all;

        return all
            .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        p.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<Product?> GetProductAsync(string productId)
    {
        EnsureAuthenticated();
        if (!int.TryParse(productId, out int id))
            return null;

        using var response = await _http.GetAsync($"/api/products/{id}").ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var apiProduct = JsonSerializer.Deserialize<ApiProductResponse>(json, JsonOptions);
        return apiProduct != null ? MapToDomainProduct(apiProduct) : null;
    }

    public async Task<string> GetNextProductIdAsync()
    {
        try
        {
            var all = await GetAllProductsAsync().ConfigureAwait(false);
            int highest = 0;
            foreach (var p in all)
            {
                if (int.TryParse(p.Id, out int n) && n > highest)
                    highest = n;
            }
            return (highest + 1).ToString();
        }
        catch
        {
            return "1";
        }
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync()
    {
        EnsureAuthenticated();
        try
        {
            using var response = await _http.GetAsync("/api/products/branches").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                if (list != null && list.Count > 0)
                    return list;
            }
        }
        catch
        {
            // fallback to product scan
        }

        var all = await GetAllProductsAsync().ConfigureAwait(false);
        return all.Select(p => p.Branch).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().OrderBy(b => b).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        EnsureAuthenticated();
        try
        {
            using var response = await _http.GetAsync("/api/products/categories").ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                if (list != null && list.Count > 0)
                    return list;
            }
        }
        catch
        {
            // fallback to product scan
        }

        var all = await GetAllProductsAsync().ConfigureAwait(false);
        return all.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
    }

    public async Task AddProductAsync(Product product)
    {
        EnsureAuthenticated();

        var branch = product.Branch?.Trim() ?? string.Empty;
        var cat = product.Category?.Trim() ?? string.Empty;
        var compositeCategory = !string.IsNullOrWhiteSpace(branch) && !cat.StartsWith(branch, StringComparison.OrdinalIgnoreCase)
            ? $"{branch} - {cat}"
            : cat;

        var payload = JsonSerializer.Serialize(new
        {
            productName = product.Name,
            branch = branch,
            category = compositeCategory,
            quantity = product.Quantity,
            unitPrice = product.UnitPrice,
            reorderLevel = product.ReorderLevel
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("/api/products", content).ConfigureAwait(false);

        await EnsureSuccessAsync(response).ConfigureAwait(false);

        // Update product ID with backend assigned ID
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("productId", out var idProp))
                {
                    product.Id = idProp.GetInt32().ToString();
                }
            }
            catch
            {
                // ignore if format differs
            }
        }
    }

    public async Task UpdateProductAsync(Product product)
    {
        EnsureAuthenticated();
        if (!int.TryParse(product.Id, out int id))
            throw new InventoryException($"Invalid product ID '{product.Id}'.");

        var branch = product.Branch?.Trim() ?? string.Empty;
        var cat = product.Category?.Trim() ?? string.Empty;
        var compositeCategory = !string.IsNullOrWhiteSpace(branch) && !cat.StartsWith(branch, StringComparison.OrdinalIgnoreCase)
            ? $"{branch} - {cat}"
            : cat;

        var payload = JsonSerializer.Serialize(new
        {
            productName = product.Name,
            branch = branch,
            category = compositeCategory,
            unitPrice = product.UnitPrice,
            reorderLevel = product.ReorderLevel
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync($"/api/products/{id}", content).ConfigureAwait(false);

        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task DeleteProductAsync(string productId)
    {
        EnsureAuthenticated();
        if (!int.TryParse(productId, out int id))
            throw new InventoryException($"Invalid product ID '{productId}'.");

        using var response = await _http.DeleteAsync($"/api/products/{id}").ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task StockInAsync(string productId, int quantity)
    {
        EnsureAuthenticated();
        if (!int.TryParse(productId, out int id))
            throw new InventoryException($"Invalid product ID '{productId}'.");

        var payload = JsonSerializer.Serialize(new { quantity });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"/api/products/{id}/stock/in", content).ConfigureAwait(false);

        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task StockOutAsync(string productId, int quantity)
    {
        EnsureAuthenticated();
        if (!int.TryParse(productId, out int id))
            throw new InventoryException($"Invalid product ID '{productId}'.");

        var payload = JsonSerializer.Serialize(new { quantity });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"/api/products/{id}/stock/out", content).ConfigureAwait(false);

        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
            throw new InventoryException("You must be logged in to perform this action.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InventoryException("Your session has expired or you are unauthorized. Please log in again.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InventoryException("Access denied. Your user role is not authorized for this operation.");

        var err = await ExtractErrorMessageAsync(response).ConfigureAwait(false);
        throw new InventoryException(err);
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("error", out var errorProp) && errorProp.GetString() is { } err)
                    return err;
                if (doc.RootElement.TryGetProperty("message", out var msgProp) && msgProp.GetString() is { } msg)
                    return msg;
                if (doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.GetString() is { } title)
                    return title;
            }
        }
        catch
        {
            // fallback
        }

        return $"Server returned status {(int)response.StatusCode} ({response.ReasonPhrase})";
    }

    private static Product MapToDomainProduct(ApiProductResponse p)
    {
        string branch = p.Branch?.Trim() ?? string.Empty;
        string category = p.Category ?? string.Empty;

        if (string.IsNullOrWhiteSpace(branch) && category.Contains(" - "))
        {
            var parts = category.Split(" - ", 2);
            branch = parts[0].Trim();
            category = parts[1].Trim();
        }

        return new()
        {
            Id = p.ProductID.ToString(),
            Name = p.ProductName,
            Branch = branch,
            Category = category,
            Quantity = p.Quantity,
            UnitPrice = p.UnitPrice,
            ReorderLevel = p.ReorderLevel
        };
    }

    private static int ParseId(string id) => int.TryParse(id, out int n) ? n : 0;

    private sealed record ApiLoginResponse(
        string Token,
        string Username,
        string Role,
        string? FullName = null,
        string? AssignedBranch = null);

    private sealed record ApiProductResponse(
        int ProductID,
        string ProductName,
        string Category,
        int Quantity,
        decimal UnitPrice,
        int ReorderLevel,
        bool IsLowStock,
        string? Branch = null);
}

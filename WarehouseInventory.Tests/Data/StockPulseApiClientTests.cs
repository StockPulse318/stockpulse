using System.Net;
using System.Net.Http;
using System.Text;
using WarehouseInventory.Data.Dtos;
using WarehouseInventory.Data.Http;
using WarehouseInventory.Data.State;
using Xunit;

namespace WarehouseInventory.Tests.Data;

public class StockPulseApiClientTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? Handler { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Handler == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(Handler(request));
        }
    }

    [Fact]
    public async Task LoginAsync_Success_ReturnsLoginResponse_AndSetsTokenInStorage()
    {
        var tokenStorage = new InMemoryTokenStorage();
        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Equal("/api/Auth/login", req.RequestUri?.AbsolutePath);

                var json = "{\"token\":\"jwt-test-token-123\",\"username\":\"manager\",\"role\":\"Warehouse Manager\"}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.stockpulse.local") };
        var client = new StockPulseApiClient(httpClient, tokenStorage);

        var response = await client.LoginAsync("manager", "Manager@1234");

        Assert.Equal("jwt-test-token-123", response.Token);
        Assert.Equal("manager", response.Username);
        Assert.Equal("Warehouse Manager", response.Role);
        Assert.Equal("jwt-test-token-123", tokenStorage.Token);
        Assert.True(tokenStorage.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_401_Unauthorized_ThrowsApiException_WithReadableMessage()
    {
        var tokenStorage = new InMemoryTokenStorage();
        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                var json = "{\"error\":\"Invalid username or password.\"}";
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.stockpulse.local") };
        var client = new StockPulseApiClient(httpClient, tokenStorage);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("baduser", "badpass"));

        Assert.True(ex.IsUnauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("Invalid username or password.", ex.DisplayMessage);
    }

    [Fact]
    public async Task Request_403_Forbidden_ThrowsApiException_WithPermissionMessage()
    {
        var tokenStorage = new InMemoryTokenStorage();
        tokenStorage.SetToken("clerk-token");

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                var json = "{\"error\":\"Access denied. User does not have manager privileges.\"}";
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.stockpulse.local") };
        var client = new StockPulseApiClient(httpClient, tokenStorage);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.DeleteProductAsync(99));

        Assert.True(ex.IsForbidden);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Contains("Access denied", ex.DisplayMessage);
    }

    [Fact]
    public async Task StockOutAsync_400_ValidationError_ExtractsNestedErrorMessage()
    {
        var tokenStorage = new InMemoryTokenStorage();
        tokenStorage.SetToken("valid-token");

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                var json = "{\"error\":{\"code\":\"INSUFFICIENT_STOCK\",\"message\":\"Cannot reduce stock below zero. Requested 50 exceeds available 10.\"}}";
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.stockpulse.local") };
        var client = new StockPulseApiClient(httpClient, tokenStorage);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.StockOutAsync(1, 50));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("INSUFFICIENT_STOCK", ex.ErrorCode);
        Assert.Contains("Cannot reduce stock below zero", ex.DisplayMessage);
    }

    [Fact]
    public async Task Request_ServerUnreachable_ThrowsApiException_WithNetworkError()
    {
        var tokenStorage = new InMemoryTokenStorage();
        var handler = new MockHttpMessageHandler
        {
            Handler = req => throw new HttpRequestException("Connection refused")
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://unreachable.stockpulse.local") };
        var client = new StockPulseApiClient(httpClient, tokenStorage);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetProductsAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal("NETWORK_ERROR", ex.ErrorCode);
        Assert.Contains("Cannot connect to the warehouse server", ex.DisplayMessage);
    }

    [Fact]
    public async Task GetLowStockProductsAsync_Success_ReturnsProducts()
    {
        var tokenStorage = new InMemoryTokenStorage();
        tokenStorage.SetToken("valid-token");

        var handler = new MockHttpMessageHandler
        {
            Handler = req =>
            {
                Assert.Equal("/api/Products/low-stock", req.RequestUri?.AbsolutePath);
                var json = "[{\"productID\":4,\"productName\":\"Copper Cable\",\"category\":\"Electrical\",\"quantity\":5,\"unitPrice\":280.0,\"reorderLevel\":15,\"isLowStock\":true}]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.stockpulse.local") };
        var client = new StockPulseApiClient(httpClient, tokenStorage);

        var lowStock = await client.GetLowStockProductsAsync();

        Assert.Single(lowStock);
        Assert.Equal("Copper Cable", lowStock[0].ProductName);
        Assert.True(lowStock[0].IsLowStock);
        Assert.Equal(5, lowStock[0].Quantity);
    }
}

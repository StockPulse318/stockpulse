using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Services;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Data.Dtos;
using WarehouseInventory.Data.Http;
using WarehouseInventory.Data.State;
using Xunit;

namespace WarehouseInventory.Tests.Business;

public class InventoryServiceTests
{
    private class FakeApiClient : IStockPulseApiClient
    {
        public List<ProductDto> Products { get; set; } = new();
        public List<ProductDto> LowStockProducts { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public bool ThrowUnauthorized { get; set; }

        public Task<LoginResponseDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LoginResponseDto { Token = "token", Username = username, Role = "Warehouse Manager" });

        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowUnauthorized) throw new ApiException(System.Net.HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Token expired");
            return Task.FromResult<IReadOnlyList<ProductDto>>(Products);
        }

        public Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string name, CancellationToken cancellationToken = default)
        {
            if (ThrowUnauthorized) throw new ApiException(System.Net.HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Token expired");
            return Task.FromResult<IReadOnlyList<ProductDto>>(Products.Where(p => p.ProductName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList());
        }

        public Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowUnauthorized) throw new ApiException(System.Net.HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Token expired");
            return Task.FromResult<IReadOnlyList<ProductDto>>(LowStockProducts);
        }

        public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Products.FirstOrDefault(p => p.ProductId == id));

        public Task<ProductDto> CreateProductAsync(CreateProductRequestDto request, CancellationToken cancellationToken = default)
        {
            var p = new ProductDto
            {
                ProductId = Products.Count + 1,
                ProductName = request.ProductName,
                Category = request.Category,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                ReorderLevel = request.ReorderLevel
            };
            Products.Add(p);
            return Task.FromResult(p);
        }

        public Task UpdateProductAsync(int id, UpdateProductRequestDto request, CancellationToken cancellationToken = default)
        {
            var p = Products.FirstOrDefault(x => x.ProductId == id);
            if (p != null)
            {
                p.ProductName = request.ProductName;
                p.Category = request.Category;
                p.UnitPrice = request.UnitPrice;
                p.ReorderLevel = request.ReorderLevel;
            }
            return Task.CompletedTask;
        }

        public Task DeleteProductAsync(int id, CancellationToken cancellationToken = default)
        {
            Products.RemoveAll(x => x.ProductId == id);
            return Task.CompletedTask;
        }

        public Task StockInAsync(int productId, int quantity, CancellationToken cancellationToken = default)
        {
            var p = Products.FirstOrDefault(x => x.ProductId == productId);
            if (p != null) p.Quantity += quantity;
            return Task.CompletedTask;
        }

        public Task StockOutAsync(int productId, int quantity, CancellationToken cancellationToken = default)
        {
            var p = Products.FirstOrDefault(x => x.ProductId == productId);
            if (p != null) p.Quantity -= quantity;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Categories);
    }

    [Fact]
    public async Task GetAllProductsAsync_MapsDtoToDomainModel()
    {
        var fakeApi = new FakeApiClient
        {
            Products = new List<ProductDto>
            {
                new() { ProductId = 1, ProductName = "Cement", Category = "Building Supplies", Quantity = 100, UnitPrice = 75.0m, ReorderLevel = 20, IsLowStock = false }
            }
        };

        var tokenStorage = new InMemoryTokenStorage();
        var validationService = new ValidationService();
        var authService = new AuthService(fakeApi, tokenStorage, validationService);
        var inventoryService = new InventoryService(fakeApi, authService, validationService);

        var list = await inventoryService.GetAllProductsAsync();

        Assert.Single(list);
        Assert.Equal(1, list[0].Id);
        Assert.Equal("Cement", list[0].Name);
        Assert.Equal("Building Supplies", list[0].Category);
        Assert.Equal(100, list[0].Quantity);
        Assert.False(list[0].IsLowStock);
    }

    [Fact]
    public async Task AddProductAsync_ThrowsWhenUserIsClerk()
    {
        var fakeApi = new FakeApiClient();
        var tokenStorage = new InMemoryTokenStorage();
        var validationService = new ValidationService();
        var authService = new AuthService(fakeApi, tokenStorage, validationService);
        var inventoryService = new InventoryService(fakeApi, authService, validationService);

        // Simulate clerk login
        var clerkUser = new User("clerk1", "Clerk", UserRole.Clerk);
        typeof(AuthService).GetField("_currentUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(authService, clerkUser);

        var newProduct = new Product
        {
            Name = "New Paint",
            Category = "Paints",
            UnitPrice = 50.0m,
            ReorderLevel = 10,
            Quantity = 20
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => inventoryService.AddProductAsync(newProduct));
        Assert.Contains("Only a Warehouse Manager", ex.Message);
    }

    [Fact]
    public async Task StockOutAsync_ThrowsValidationException_WhenAmountExceedsAvailable()
    {
        var fakeApi = new FakeApiClient();
        var tokenStorage = new InMemoryTokenStorage();
        var validationService = new ValidationService();
        var authService = new AuthService(fakeApi, tokenStorage, validationService);
        var inventoryService = new InventoryService(fakeApi, authService, validationService);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => inventoryService.StockOutAsync(1, currentQuantity: 10, quantity: 15));
        Assert.Contains("exceeds available inventory", ex.Message);
    }

    [Fact]
    public async Task UnauthorizedException_TriggersSessionExpiredEvent()
    {
        var fakeApi = new FakeApiClient { ThrowUnauthorized = true };
        var tokenStorage = new InMemoryTokenStorage();
        tokenStorage.SetToken("expired-token");

        var validationService = new ValidationService();
        var authService = new AuthService(fakeApi, tokenStorage, validationService);
        var inventoryService = new InventoryService(fakeApi, authService, validationService);

        bool sessionExpiredFired = false;
        authService.SessionExpired += msg => sessionExpiredFired = true;

        await Assert.ThrowsAsync<ApiException>(() => inventoryService.GetAllProductsAsync());

        Assert.True(sessionExpiredFired);
        Assert.Null(tokenStorage.Token);
    }
}

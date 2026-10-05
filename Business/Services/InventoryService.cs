using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Data.Dtos;
using WarehouseInventory.Data.Http;

namespace WarehouseInventory.Business.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly IStockPulseApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly IValidationService _validationService;

    public InventoryService(
        IStockPulseApiClient apiClient,
        IAuthService authService,
        IValidationService validationService)
    {
        _apiClient = apiClient;
        _authService = authService;
        _validationService = validationService;
    }

    public async Task<IReadOnlyList<Product>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithAuthHandlingAsync(async () =>
        {
            var dtos = await _apiClient.GetProductsAsync(cancellationToken);
            return dtos.Select(MapToModel).ToList();
        });
    }

    public async Task<IReadOnlyList<Product>> SearchProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithAuthHandlingAsync(async () =>
        {
            var dtos = await _apiClient.SearchProductsAsync(query, cancellationToken);
            return dtos.Select(MapToModel).ToList();
        });
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithAuthHandlingAsync(async () =>
        {
            var dtos = await _apiClient.GetLowStockProductsAsync(cancellationToken);
            return dtos.Select(MapToModel).ToList();
        });
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithAuthHandlingAsync(async () =>
        {
            var categories = await _apiClient.GetCategoriesAsync(cancellationToken);
            if (categories.Count > 0)
            {
                return categories.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
            }

            // Fallback: derive distinct categories from the full product catalog
            var allProducts = await _apiClient.GetProductsAsync(cancellationToken);
            return allProducts
                .Select(p => CleanCategory(p.Category))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        });
    }

    public async Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        _validationService.ValidateRoleAction(_authService.CurrentUser, "AddProduct");
        _validationService.ValidateProduct(product.Name, product.Category, product.UnitPrice, product.ReorderLevel, product.Quantity);

        return await ExecuteWithAuthHandlingAsync(async () =>
        {
            var request = new CreateProductRequestDto
            {
                ProductName = product.Name.Trim(),
                Category = product.Category.Trim(),
                Quantity = product.Quantity,
                UnitPrice = product.UnitPrice,
                ReorderLevel = product.ReorderLevel
            };

            var createdDto = await _apiClient.CreateProductAsync(request, cancellationToken);
            return MapToModel(createdDto);
        });
    }

    public async Task UpdateProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        _validationService.ValidateRoleAction(_authService.CurrentUser, "EditProduct");
        _validationService.ValidateProduct(product.Name, product.Category, product.UnitPrice, product.ReorderLevel);

        await ExecuteWithAuthHandlingAsync(async () =>
        {
            var request = new UpdateProductRequestDto
            {
                ProductName = product.Name.Trim(),
                Category = product.Category.Trim(),
                UnitPrice = product.UnitPrice,
                ReorderLevel = product.ReorderLevel
            };

            await _apiClient.UpdateProductAsync(product.Id, request, cancellationToken);
            return true;
        });
    }

    public async Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        _validationService.ValidateRoleAction(_authService.CurrentUser, "DeleteProduct");

        await ExecuteWithAuthHandlingAsync(async () =>
        {
            await _apiClient.DeleteProductAsync(productId, cancellationToken);
            return true;
        });
    }

    public async Task StockInAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        _validationService.ValidateStockAmount(quantity);

        await ExecuteWithAuthHandlingAsync(async () =>
        {
            await _apiClient.StockInAsync(productId, quantity, cancellationToken);
            return true;
        });
    }

    public async Task StockOutAsync(int productId, int currentQuantity, int quantity, CancellationToken cancellationToken = default)
    {
        _validationService.ValidateStockOut(currentQuantity, quantity);

        await ExecuteWithAuthHandlingAsync(async () =>
        {
            await _apiClient.StockOutAsync(productId, quantity, cancellationToken);
            return true;
        });
    }

    private async Task<T> ExecuteWithAuthHandlingAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiException ex) when (ex.IsUnauthorized)
        {
            _authService.NotifySessionExpired();
            throw;
        }
    }

    private static Product MapToModel(ProductDto dto)
    {
        return new Product
        {
            Id = dto.ProductId,
            Name = dto.ProductName,
            Category = CleanCategory(dto.Category),
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            ReorderLevel = dto.ReorderLevel,
            ServerIsLowStock = dto.IsLowStock
        };
    }

    private static string CleanCategory(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            return string.Empty;
        }

        // If category contains branch prefix (e.g., "Accra Central - Building Supplies"), strip prefix
        if (rawCategory.Contains(" - "))
        {
            var parts = rawCategory.Split(" - ", 2);
            return parts[1].Trim();
        }

        return rawCategory.Trim();
    }
}

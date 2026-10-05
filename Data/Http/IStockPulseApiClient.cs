using WarehouseInventory.Data.Dtos;

namespace WarehouseInventory.Data.Http;

public interface IStockPulseApiClient
{
    Task<LoginResponseDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequestDto request, CancellationToken cancellationToken = default);
    Task UpdateProductAsync(int id, UpdateProductRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int id, CancellationToken cancellationToken = default);
    Task StockInAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    Task StockOutAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}

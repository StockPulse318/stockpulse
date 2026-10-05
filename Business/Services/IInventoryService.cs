using WarehouseInventory.Business.Models;

namespace WarehouseInventory.Business.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<Product>> GetAllProductsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> SearchProductsAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateProductAsync(Product product, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default);
    Task StockInAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    Task StockOutAsync(int productId, int currentQuantity, int quantity, CancellationToken cancellationToken = default);
}

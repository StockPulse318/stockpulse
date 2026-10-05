using WarehouseInventory.Models;

namespace WarehouseInventory.Services;

public class InventoryService : IInventoryService
{
    private readonly StockPulseApiClient _client;

    public InventoryService(StockPulseApiClient client)
    {
        _client = client;
    }

    public StockPulseApiClient Client => _client;

    public IReadOnlyList<Product> GetAllProducts() =>
        RunSync(() => _client.GetAllProductsAsync());

    public IReadOnlyList<Product> SearchProducts(string searchText) =>
        RunSync(() => _client.SearchProductsAsync(searchText));

    public Product? GetProduct(string productId) =>
        RunSync(() => _client.GetProductAsync(productId));

    public string GetNextProductId() =>
        RunSync(() => _client.GetNextProductIdAsync());

    public void AddProduct(Product product) =>
        RunSync(() => _client.AddProductAsync(product));

    public void UpdateProduct(Product product) =>
        RunSync(() => _client.UpdateProductAsync(product));

    public void DeleteProduct(string productId) =>
        RunSync(() => _client.DeleteProductAsync(productId));

    public void StockIn(string productId, int quantity) =>
        RunSync(() => _client.StockInAsync(productId, quantity));

    public void StockOut(string productId, int quantity) =>
        RunSync(() => _client.StockOutAsync(productId, quantity));

    private static T RunSync<T>(Func<Task<T>> func)
    {
        try
        {
            return Task.Run(func).GetAwaiter().GetResult();
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static void RunSync(Func<Task> func)
    {
        try
        {
            Task.Run(func).GetAwaiter().GetResult();
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}

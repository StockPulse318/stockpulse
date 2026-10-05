using WarehouseInventory.Models;

namespace WarehouseInventory.Services;

// Everything the screens need from the business/data layers.
// Interfaced through REST API client services communicating with the backend.
public interface IInventoryService
{
    IReadOnlyList<Product> GetAllProducts();

    // Empty or blank search text returns everything. Matches product name or ID, ignoring case.
    IReadOnlyList<Product> SearchProducts(string searchText);

    Product? GetProduct(string productId);

    // Suggested ID for the Add Product form, e.g. "P-1011".
    string GetNextProductId();

    // These throw InventoryException when the rules are broken.
    void AddProduct(Product product);
    void UpdateProduct(Product product);   // changes name, category, price, reorder level (not quantity)
    void DeleteProduct(string productId);
    void StockIn(string productId, int quantity);
    void StockOut(string productId, int quantity);   // must refuse if it would go below zero

    IReadOnlyList<string> GetBranches();
    IReadOnlyList<string> GetCategories();
}

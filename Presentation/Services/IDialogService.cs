using WarehouseInventory.Business.Models;

namespace WarehouseInventory.Presentation.Services;

public interface IDialogService
{
    void ShowInformation(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    bool ShowConfirmation(string title, string message);
    bool ShowStockDialog(Product product, bool isStockIn, out int quantity);
    bool ShowProductDialog(Product? existingProduct, IReadOnlyList<Category> categories, out Product result);
    void ShowMainWindow();
    void ShowLoginWindow(string? message = null);
}

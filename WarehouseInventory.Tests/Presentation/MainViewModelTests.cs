using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Services;
using WarehouseInventory.Presentation.Services;
using WarehouseInventory.Presentation.ViewModels;
using Xunit;

namespace WarehouseInventory.Tests.Presentation;

public class MainViewModelTests
{
    private class FakeDialogService : IDialogService
    {
        public void ShowInformation(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
        public bool ShowConfirmation(string title, string message) => true;
        public bool ShowStockDialog(Product product, bool isStockIn, out int quantity) { quantity = 5; return true; }
        public bool ShowProductDialog(Product? existingProduct, IReadOnlyList<Category> categories, out Product result) { result = new Product(); return true; }
        public void ShowMainWindow() { }
        public void ShowLoginWindow(string? message = null) { }
    }

    private class FakeAuthService : IAuthService
    {
        public User? CurrentUser { get; set; }
        public bool IsAuthenticated => CurrentUser != null;
        public event Action<string>? SessionExpired;

        public Task<User> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentUser!);
        public void Logout() => CurrentUser = null;
        public void NotifySessionExpired() => SessionExpired?.Invoke("Expired");
    }

    private class FakeInventoryService : IInventoryService
    {
        public List<Product> Products { get; set; } = new();
        public List<Product> LowStockProducts { get; set; } = new();

        public Task<IReadOnlyList<Product>> GetAllProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>(Products);
        public Task<IReadOnlyList<Product>> SearchProductsAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>(Products.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());
        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>(LowStockProducts);
        public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Category>>(Products.Select(p => new Category(p.CategoryId == 0 ? 1 : p.CategoryId, p.Category)).DistinctBy(c => c.Name).ToList());
        public Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default) => Task.FromResult(product);
        public Task UpdateProductAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteProductAsync(int productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StockInAsync(int productId, int quantity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StockOutAsync(int productId, int currentQuantity, int quantity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void RoleVisibility_ManagerSeesControls_ClerkDoesNot()
    {
        var authService = new FakeAuthService { CurrentUser = new User("mgr", "Manager", UserRole.WarehouseManager) };
        var inventoryService = new FakeInventoryService();
        var dialogService = new FakeDialogService();

        var vm = new MainViewModel(inventoryService, authService, dialogService);

        Assert.True(vm.CanManageProducts);

        authService.CurrentUser = new User("clk", "Clerk", UserRole.Clerk);
        var clerkVm = new MainViewModel(inventoryService, authService, dialogService);

        Assert.False(clerkVm.CanManageProducts);
    }

    [Fact]
    public async Task Paging_CalculatesTotalPagesAndNavigates()
    {
        var authService = new FakeAuthService { CurrentUser = new User("mgr", "Manager", UserRole.WarehouseManager) };
        var inventoryService = new FakeInventoryService();
        for (int i = 1; i <= 35; i++)
        {
            inventoryService.Products.Add(new Product { Id = i, Name = $"Product {i}", Category = "General", Quantity = 50, UnitPrice = 10, ReorderLevel = 5 });
        }

        var dialogService = new FakeDialogService();
        var vm = new MainViewModel(inventoryService, authService, dialogService) { PageSize = 10 };

        await vm.InitializeAsync();

        Assert.Equal(35, vm.TotalItemCount);
        Assert.Equal(4, vm.TotalPages);
        Assert.Equal(1, vm.CurrentPage);
        Assert.False(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
        Assert.Equal(10, vm.DisplayedProducts.Count);

        vm.NextPageCommand.Execute(null);

        Assert.Equal(2, vm.CurrentPage);
        Assert.True(vm.HasPreviousPage);
        Assert.True(vm.HasNextPage);
    }

    [Fact]
    public async Task AlertsTab_FiltersToOnlyLowStockProducts()
    {
        var authService = new FakeAuthService { CurrentUser = new User("mgr", "Manager", UserRole.WarehouseManager) };
        var inventoryService = new FakeInventoryService();

        inventoryService.Products.Add(new Product { Id = 1, Name = "Good Stock", Category = "General", Quantity = 100, ReorderLevel = 10, ServerIsLowStock = false });
        inventoryService.Products.Add(new Product { Id = 2, Name = "Low Stock Item", Category = "General", Quantity = 2, ReorderLevel = 10, ServerIsLowStock = true });

        inventoryService.LowStockProducts.Add(inventoryService.Products[1]);

        var dialogService = new FakeDialogService();
        var vm = new MainViewModel(inventoryService, authService, dialogService);

        await vm.InitializeAsync();

        Assert.Equal(1, vm.LowStockCount);
        Assert.Equal(2, vm.DisplayedProducts.Count); // On all tab

        vm.SwitchTabCommand.Execute("Alerts");

        Assert.True(vm.IsAlertsTabActive);
        Assert.Single(vm.DisplayedProducts);
        Assert.Equal("Low Stock Item", vm.DisplayedProducts[0].Name);
        Assert.True(vm.DisplayedProducts[0].IsLowStock);
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Services;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Data.Http;
using WarehouseInventory.Presentation.Services;

namespace WarehouseInventory.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;

    private CancellationTokenSource? _searchCts;
    private List<Product> _allLoadedProducts = new();
    private List<Product> _lowStockProducts = new();

    // User & Role
    public User? CurrentUser => _authService.CurrentUser;
    public string UserDisplay => CurrentUser != null ? $"{CurrentUser.FullName} ({CurrentUser.RoleDisplayName})" : string.Empty;
    public bool CanManageProducts => CurrentUser?.CanManageProducts == true;

    // Search & Filter
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All Categories";

    public ObservableCollection<string> Categories { get; } = new() { "All Categories" };

    // View Mode (Inventory vs Alerts)
    [ObservableProperty]
    private bool _isAlertsTabActive;

    [ObservableProperty]
    private int _lowStockCount;

    // DataGrid Display Collection
    public ObservableCollection<ProductRowViewModel> DisplayedProducts { get; } = new();

    [ObservableProperty]
    private ProductRowViewModel? _selectedProduct;

    // Pagination
    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 15;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalItemCount;

    [ObservableProperty]
    private string _pageInfo = "Page 1 of 1";

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    // Sorting
    [ObservableProperty]
    private string _sortColumn = "Id";

    [ObservableProperty]
    private bool _sortAscending = true;

    // UI Status States
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public MainViewModel(
        IInventoryService inventoryService,
        IAuthService authService,
        IDialogService dialogService)
    {
        _inventoryService = inventoryService;
        _authService = authService;
        _dialogService = dialogService;

        _authService.SessionExpired += OnSessionExpired;
    }

    private void OnSessionExpired(string message)
    {
        _dialogService.ShowLoginWindow(message);
    }

    public async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
        await LoadDataAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                App.Current?.Dispatcher?.Invoke(() =>
                {
                    CurrentPage = 1;
                    ApplyFiltersAndPaging();
                });
            }
            catch (TaskCanceledException)
            {
                // In-flight request was cancelled due to new keystroke
            }
        }, token);
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPaging();
    }

    partial void OnIsAlertsTabActiveChanged(bool value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPaging();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        IsAlertsTabActive = tabName == "Alerts";
    }

    [RelayCommand]
    private void Sort(string column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        ApplyFiltersAndPaging();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (HasNextPage)
        {
            CurrentPage++;
            ApplyFiltersAndPaging();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
            ApplyFiltersAndPaging();
        }
    }

    [RelayCommand]
    private async Task StockInAsync()
    {
        if (SelectedProduct == null) return;
        var product = SelectedProduct.Product;

        if (_dialogService.ShowStockDialog(product, isStockIn: true, out int quantity))
        {
            IsLoading = true;
            try
            {
                await _inventoryService.StockInAsync(product.Id, quantity);
                product.Quantity += quantity;
                SelectedProduct.RefreshProperties();
                await RefreshLowStockCountAsync();
                _dialogService.ShowInformation("Stock Updated",
                    $"Successfully stocked in {quantity} units of {product.Name}. New quantity: {product.Quantity}.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Stock-In Failed", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task StockOutAsync()
    {
        if (SelectedProduct == null) return;
        var product = SelectedProduct.Product;

        if (_dialogService.ShowStockDialog(product, isStockIn: false, out int quantity))
        {
            IsLoading = true;
            try
            {
                await _inventoryService.StockOutAsync(product.Id, product.Quantity, quantity);
                product.Quantity -= quantity;
                SelectedProduct.RefreshProperties();
                await RefreshLowStockCountAsync();

                if (product.IsLowStock)
                {
                    _dialogService.ShowWarning("Low Stock Warning",
                        $"{product.Name} quantity is now {product.Quantity} (at or below reorder level {product.ReorderLevel}). Restock needed!");
                }
                else
                {
                    _dialogService.ShowInformation("Stock Updated",
                        $"Successfully stocked out {quantity} units of {product.Name}. New quantity: {product.Quantity}.");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Stock-Out Failed", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task AddProductAsync()
    {
        if (!CanManageProducts) return;

        var categoriesList = Categories.Where(c => c != "All Categories").ToList();
        if (_dialogService.ShowProductDialog(null, categoriesList, out var newProduct))
        {
            IsLoading = true;
            try
            {
                var created = await _inventoryService.AddProductAsync(newProduct);
                _allLoadedProducts.Add(created);
                ApplyFiltersAndPaging();
                await RefreshLowStockCountAsync();
                _dialogService.ShowInformation("Product Added",
                    $"Product '{created.Name}' (ID: {created.Id}) was successfully added.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Add Product Failed", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task EditProductAsync()
    {
        if (!CanManageProducts || SelectedProduct == null) return;
        var existingProduct = SelectedProduct.Product;

        var categoriesList = Categories.Where(c => c != "All Categories").ToList();
        if (_dialogService.ShowProductDialog(existingProduct, categoriesList, out var updatedProduct))
        {
            IsLoading = true;
            try
            {
                await _inventoryService.UpdateProductAsync(updatedProduct);
                existingProduct.Name = updatedProduct.Name;
                existingProduct.Category = updatedProduct.Category;
                existingProduct.UnitPrice = updatedProduct.UnitPrice;
                existingProduct.ReorderLevel = updatedProduct.ReorderLevel;
                SelectedProduct.RefreshProperties();
                ApplyFiltersAndPaging();
                await RefreshLowStockCountAsync();
                _dialogService.ShowInformation("Product Updated",
                    $"Product '{existingProduct.Name}' was successfully updated.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Update Failed", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (!CanManageProducts || SelectedProduct == null) return;
        var product = SelectedProduct.Product;

        bool confirm = _dialogService.ShowConfirmation("Confirm Delete",
            $"Are you sure you want to delete product #{product.Id} '{product.Name}'?\nThis action cannot be undone.");

        if (confirm)
        {
            IsLoading = true;
            try
            {
                await _inventoryService.DeleteProductAsync(product.Id);
                _allLoadedProducts.RemoveAll(p => p.Id == product.Id);
                ApplyFiltersAndPaging();
                await RefreshLowStockCountAsync();
                _dialogService.ShowInformation("Product Deleted",
                    $"Product '{product.Name}' (ID: {product.Id}) has been deleted.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Delete Failed", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        _dialogService.ShowLoginWindow();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _inventoryService.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add("All Categories");
            foreach (var cat in categories)
            {
                if (!Categories.Contains(cat))
                {
                    Categories.Add(cat);
                }
            }
        }
        catch
        {
            // Fallback gracefully if categories cannot be loaded immediately
        }
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var productsTask = _inventoryService.GetAllProductsAsync();
            var lowStockTask = _inventoryService.GetLowStockProductsAsync();

            await Task.WhenAll(productsTask, lowStockTask);

            _allLoadedProducts = productsTask.Result.ToList();
            _lowStockProducts = lowStockTask.Result.ToList();
            LowStockCount = _lowStockProducts.Count;

            // Populate any new categories discovered from products
            foreach (var p in _allLoadedProducts)
            {
                if (!string.IsNullOrWhiteSpace(p.Category) && !Categories.Contains(p.Category))
                {
                    Categories.Add(p.Category);
                }
            }

            ApplyFiltersAndPaging();
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.DisplayMessage;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load inventory: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshLowStockCountAsync()
    {
        try
        {
            var lowStock = await _inventoryService.GetLowStockProductsAsync();
            _lowStockProducts = lowStock.ToList();
            LowStockCount = _lowStockProducts.Count;
        }
        catch
        {
            // Ignore background count refresh failure
        }
    }

    private void ApplyFiltersAndPaging()
    {
        var sourceList = IsAlertsTabActive ? _lowStockProducts : _allLoadedProducts;

        IEnumerable<Product> query = sourceList;

        // 1. Search text filter (matches Product Name or Product ID)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Category filter
        if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All Categories")
        {
            query = query.Where(p => string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Sorting
        query = SortColumn switch
        {
            "Id" => SortAscending ? query.OrderBy(p => p.Id) : query.OrderByDescending(p => p.Id),
            "Name" => SortAscending ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            "Category" => SortAscending ? query.OrderBy(p => p.Category) : query.OrderByDescending(p => p.Category),
            "Quantity" => SortAscending ? query.OrderBy(p => p.Quantity) : query.OrderByDescending(p => p.Quantity),
            "UnitPrice" => SortAscending ? query.OrderBy(p => p.UnitPrice) : query.OrderByDescending(p => p.UnitPrice),
            "ReorderLevel" => SortAscending ? query.OrderBy(p => p.ReorderLevel) : query.OrderByDescending(p => p.ReorderLevel),
            _ => query.OrderBy(p => p.Id)
        };

        var filteredList = query.ToList();
        TotalItemCount = filteredList.Count;

        // 4. Pagination
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalItemCount / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;

        var pagedItems = filteredList
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new ProductRowViewModel(p))
            .ToList();

        // Keep previously selected row if still present
        var previousSelectedId = SelectedProduct?.Id;

        DisplayedProducts.Clear();
        foreach (var item in pagedItems)
        {
            DisplayedProducts.Add(item);
        }

        if (previousSelectedId.HasValue)
        {
            SelectedProduct = DisplayedProducts.FirstOrDefault(p => p.Id == previousSelectedId.Value);
        }

        IsEmpty = TotalItemCount == 0;
        PageInfo = $"Page {CurrentPage} of {TotalPages} ({TotalItemCount} items)";

        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
    }
}

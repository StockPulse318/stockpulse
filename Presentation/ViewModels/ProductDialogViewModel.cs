using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Validation;

namespace WarehouseInventory.Presentation.ViewModels;

public sealed partial class ProductDialogViewModel : ObservableObject
{
    private readonly IValidationService _validationService;
    private readonly Product? _originalProduct;

    public bool IsAddMode => _originalProduct == null;
    public string Title => IsAddMode ? "Add New Product" : $"Edit Product #{_originalProduct!.Id}";

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private string _unitPriceText = "0.00";

    [ObservableProperty]
    private string _reorderLevelText = "10";

    [ObservableProperty]
    private string _quantityText = "0";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public Product? ResultProduct { get; private set; }
    public bool? DialogResult { get; private set; }

    public event Action? CloseRequested;

    public ProductDialogViewModel(
        Product? originalProduct,
        IReadOnlyList<string> availableCategories,
        IValidationService validationService)
    {
        _originalProduct = originalProduct;
        _validationService = validationService;

        foreach (var cat in availableCategories.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            Categories.Add(cat);
        }

        if (originalProduct != null)
        {
            Name = originalProduct.Name;
            SelectedCategory = Categories.FirstOrDefault(c => string.Equals(c, originalProduct.Category, StringComparison.OrdinalIgnoreCase))
                               ?? originalProduct.Category;
            UnitPriceText = originalProduct.UnitPrice.ToString("F2");
            ReorderLevelText = originalProduct.ReorderLevel.ToString();
            QuantityText = originalProduct.Quantity.ToString();
        }
        else
        {
            SelectedCategory = Categories.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Product Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCategory))
        {
            ErrorMessage = "Please select a Category from the list.";
            return;
        }

        if (!decimal.TryParse(UnitPriceText.Trim(), out decimal unitPrice) || unitPrice < 0)
        {
            ErrorMessage = "Unit Price must be a valid non-negative number.";
            return;
        }

        if (!int.TryParse(ReorderLevelText.Trim(), out int reorderLevel) || reorderLevel < 0)
        {
            ErrorMessage = "Reorder Level must be a valid non-negative whole number.";
            return;
        }

        int quantity = 0;
        if (IsAddMode)
        {
            if (!int.TryParse(QuantityText.Trim(), out quantity) || quantity < 0)
            {
                ErrorMessage = "Initial Quantity must be a valid non-negative whole number.";
                return;
            }
        }
        else
        {
            quantity = _originalProduct!.Quantity;
        }

        try
        {
            _validationService.ValidateProduct(Name, SelectedCategory, unitPrice, reorderLevel, quantity);

            ResultProduct = new Product
            {
                Id = _originalProduct?.Id ?? 0,
                Name = Name.Trim(),
                Category = SelectedCategory.Trim(),
                UnitPrice = unitPrice,
                ReorderLevel = reorderLevel,
                Quantity = quantity,
                ServerIsLowStock = _originalProduct?.ServerIsLowStock ?? false
            };

            DialogResult = true;
            CloseRequested?.Invoke();
        }
        catch (ValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        CloseRequested?.Invoke();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Validation;

namespace WarehouseInventory.Presentation.ViewModels;

public sealed partial class StockDialogViewModel : ObservableObject
{
    private readonly IValidationService _validationService;
    private readonly Product _product;
    private readonly bool _isStockIn;

    public string Title => _isStockIn ? $"Stock In — {_product.Name}" : $"Stock Out — {_product.Name}";
    public string OperationLabel => _isStockIn ? "Stock In Quantity" : "Stock Out Quantity";
    public string ActionButtonText => _isStockIn ? "Confirm Stock In" : "Confirm Stock Out";
    public string ProductInfo => $"Product #{_product.Id}: {_product.Name} | Current Stock: {_product.Quantity}";

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public int ValidatedAmount { get; private set; }
    public bool? DialogResult { get; private set; }

    public event Action? CloseRequested;

    public StockDialogViewModel(Product product, bool isStockIn, IValidationService validationService)
    {
        _product = product;
        _isStockIn = isStockIn;
        _validationService = validationService;
    }

    [RelayCommand]
    private void Confirm()
    {
        ErrorMessage = string.Empty;

        try
        {
            int amount = _validationService.ValidateStockAmount(AmountText);

            if (!_isStockIn)
            {
                _validationService.ValidateStockOut(_product.Quantity, amount);
            }

            ValidatedAmount = amount;
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

using CommunityToolkit.Mvvm.ComponentModel;
using WarehouseInventory.Business.Models;

namespace WarehouseInventory.Presentation.ViewModels;

public sealed partial class ProductRowViewModel : ObservableObject
{
    public Product Product { get; }

    public int Id => Product.Id;
    public string Name => Product.Name;
    public string Category => Product.Category;
    public int Quantity => Product.Quantity;
    public decimal UnitPrice => Product.UnitPrice;
    public string UnitPriceFormatted => $"GH₵ {Product.UnitPrice:N2}";
    public int ReorderLevel => Product.ReorderLevel;
    public bool IsLowStock => Product.IsLowStock;
    public string StatusText => IsLowStock ? "⚠ Restock needed" : "Normal";

    public ProductRowViewModel(Product product)
    {
        Product = product;
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(UnitPriceFormatted));
        OnPropertyChanged(nameof(ReorderLevel));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(StatusText));
    }
}

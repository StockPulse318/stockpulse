using System.Windows;
using System.Windows.Controls;
using WarehouseInventory.Models;

namespace WarehouseInventory.Views;

public partial class StockDialog : Window
{
    private readonly Product _product;
    private readonly bool _isStockIn;

    public StockDialog(Product product, bool isStockIn)
    {
        _product = product;
        _isStockIn = isStockIn;
        InitializeComponent();

        Title = isStockIn ? "Stock In" : "Stock Out";
        ProductText.Text = $"{product.Name} ({product.Id})";
        CurrentText.Text = $"In stock now: {product.Quantity}   |   Reorder level: {product.ReorderLevel}";
        QuantityLabel.Text = isStockIn ? "Quantity received" : "Quantity leaving the warehouse";
        OkButton.Content = isStockIn ? "Add to stock" : "Remove from stock";

        QuantityBox.Text = "1";
        Loaded += (s, e) => { QuantityBox.Focus(); QuantityBox.SelectAll(); };
    }

    public int Quantity { get; private set; }

    // Live check, so the user sees a problem before pressing the button.
    private void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ErrorText.Text = "";
        PreviewText.Text = "";
        OkButton.IsEnabled = false;

        if (!int.TryParse(QuantityBox.Text, out int qty) || qty < 1)
        {
            if (QuantityBox.Text.Length > 0) ErrorText.Text = "Enter a whole number of 1 or more.";
            return;
        }

        int after = _isStockIn ? _product.Quantity + qty : _product.Quantity - qty;
        if (after < 0)
        {
            ErrorText.Text = $"Only {_product.Quantity} in stock. Quantity cannot go below zero.";
            return;
        }

        PreviewText.Text = $"New quantity will be {after}" + (after <= _product.ReorderLevel ? " (at or below reorder level)." : ".");
        OkButton.IsEnabled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(QuantityBox.Text, out int qty) || qty < 1) return;
        Quantity = qty;
        DialogResult = true;
    }
}

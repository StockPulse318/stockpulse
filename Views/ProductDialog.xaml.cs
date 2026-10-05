using System.Globalization;
using System.Windows;
using WarehouseInventory.Models;

namespace WarehouseInventory.Views;

public partial class ProductDialog : Window
{
    private readonly bool _isEdit;
    private readonly int _existingQuantity;

    // Pass existing = null to add a new product.
    public ProductDialog(string productId, Product? existing, IEnumerable<string> categories)
    {
        InitializeComponent();
        _isEdit = existing != null;
        Title = _isEdit ? "Edit Product" : "Add Product";
        CategoryBox.ItemsSource = categories.ToList();

        IdBox.Text = productId;
        if (existing != null)
        {
            NameBox.Text = existing.Name;
            CategoryBox.Text = existing.Category;
            QuantityBox.Text = existing.Quantity.ToString();
            ReorderBox.Text = existing.ReorderLevel.ToString();
            PriceBox.Text = existing.UnitPrice.ToString("0.00");
            _existingQuantity = existing.Quantity;

            IdBox.IsEnabled = false;
            QuantityBox.IsEnabled = false;   // quantity only changes through Stock In / Stock Out
            HintText.Text = "To change the quantity, use Stock In or Stock Out.";
        }
        else
        {
            IdBox.IsEnabled = false;
            IdBox.Text = string.IsNullOrWhiteSpace(productId) ? "(Auto)" : productId;
            HintText.Text = "Product ID is assigned automatically by the server.";
            QuantityBox.Text = "0";
            ReorderBox.Text = "10";
            PriceBox.Text = "1.00";
        }

        Loaded += (s, e) => NameBox.Focus();
    }

    // Filled in when the user presses Save and everything checks out.
    public Product? Result { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string id = IdBox.Text.Trim();
        string name = NameBox.Text.Trim();
        string category = CategoryBox.Text.Trim();

        if (name.Length == 0 || category.Length == 0)
        {
            ErrorText.Text = "Product name and category are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(id) || id == "(Auto)")
        {
            id = "0";
        }

        int quantity = _isEdit ? _existingQuantity : 0;
        if (!_isEdit && (!int.TryParse(QuantityBox.Text, out quantity) || quantity < 0))
        {
            ErrorText.Text = "Quantity must be a whole number, 0 or more.";
            return;
        }
        if (!int.TryParse(ReorderBox.Text, out int reorder) || reorder < 0)
        {
            ErrorText.Text = "Reorder level must be a whole number, 0 or more.";
            return;
        }

        var priceParsed = decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price) ||
                          decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out price);

        if (!priceParsed || price <= 0)
        {
            ErrorText.Text = "Unit price must be a valid number greater than zero.";
            return;
        }

        Result = new Product
        {
            Id = id,
            Name = name,
            Category = category,
            Quantity = quantity,
            ReorderLevel = reorder,
            UnitPrice = price
        };
        DialogResult = true;
    }
}

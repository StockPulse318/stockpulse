using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WarehouseInventory.Models;
using WarehouseInventory.Services;
using WarehouseInventory.ViewModels;

namespace WarehouseInventory.Views;

public partial class MainWindow : Window
{
    private readonly User _user;
    private readonly IAuthService _auth;
    private readonly IInventoryService _inventory;
    private bool _ready;   // stops the filter events from running while the window is still being built

    public MainWindow(User user, IAuthService auth, IInventoryService inventory)
    {
        _user = user;
        _auth = auth;
        _inventory = inventory;
        InitializeComponent();

        UserText.Text = $"{user.FullName} ({user.RoleName})";

        // Clerks only search and move stock, so the product record buttons are hidden for them.
        var manageVisibility = user.CanManageProducts ? Visibility.Visible : Visibility.Collapsed;
        AddButton.Visibility = manageVisibility;
        EditButton.Visibility = manageVisibility;
        DeleteButton.Visibility = manageVisibility;

        _ready = true;
        RefreshList();
    }

    private ProductRow? SelectedRow => ProductGrid.SelectedItem as ProductRow;

    // Reloads the grid from the service, keeping the same product selected if it is still there.
    private void RefreshList(string? selectId = null)
    {
        if (!_ready) return;
        string? keep = selectId ?? SelectedRow?.Id;

        var all = _inventory.GetAllProducts();
        var found = _inventory.SearchProducts(SearchBox.Text);
        if (LowOnlyBox.IsChecked == true)
            found = found.Where(p => p.IsLowStock).ToList();

        // the longest bar is the biggest quantity or reorder level in the whole warehouse
        int scale = Math.Max(1, all.Select(p => Math.Max(p.Quantity, p.ReorderLevel)).DefaultIfEmpty(1).Max());
        var rows = found.Select(p => new ProductRow(p, scale)).ToList();

        ProductGrid.ItemsSource = rows;
        ProductGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == keep);
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var low = all.Where(p => p.IsLowStock).ToList();
        AlertBar.Visibility = low.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AlertText.Text = $"{low.Count} product{(low.Count == 1 ? "" : "s")} need restocking: "
                         + string.Join(", ", low.Take(4).Select(p => p.Name))
                         + (low.Count > 4 ? " and more." : ".");
        CountText.Text = $"{rows.Count} of {all.Count} products";

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool picked = SelectedRow != null;
        StockInButton.IsEnabled = picked;
        StockOutButton.IsEnabled = picked;
        EditButton.IsEnabled = picked;
        DeleteButton.IsEnabled = picked;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshList();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => RefreshList();
    private void ShowLow_Click(object sender, RoutedEventArgs e) => LowOnlyBox.IsChecked = true;
    private void ProductGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void ProductGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_user.CanManageProducts && SelectedRow != null) Edit_Click(sender, e);
    }

    private void ProductGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _user.CanManageProducts && SelectedRow != null) Delete_Click(sender, e);
    }

    // ----- product records (manager) -----

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var categories = _inventory.GetAllProducts().Select(p => p.Category).Distinct().OrderBy(c => c);
        var dialog = new ProductDialog(_inventory.GetNextProductId(), null, categories) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        Run(() => _inventory.AddProduct(dialog.Result), dialog.Result.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow == null) return;
        var categories = _inventory.GetAllProducts().Select(p => p.Category).Distinct().OrderBy(c => c);
        var dialog = new ProductDialog(SelectedRow.Id, SelectedRow.Product, categories) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        Run(() => _inventory.UpdateProduct(dialog.Result), dialog.Result.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row == null) return;

        var answer = MessageBox.Show(this,
            $"Delete {row.Name} ({row.Id})?\nThis cannot be undone.",
            "Delete product", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        Run(() => _inventory.DeleteProduct(row.Id), null);
    }

    // ----- stock movements (manager and clerk) -----

    private void StockIn_Click(object sender, RoutedEventArgs e) => MoveStock(true);
    private void StockOut_Click(object sender, RoutedEventArgs e) => MoveStock(false);

    private void MoveStock(bool isStockIn)
    {
        var row = SelectedRow;
        if (row == null) return;

        var dialog = new StockDialog(row.Product, isStockIn) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        string id = row.Id;
        bool ok = Run(() =>
        {
            if (isStockIn) _inventory.StockIn(id, dialog.Quantity);
            else _inventory.StockOut(id, dialog.Quantity);
        }, id);

        // PRD: show a warning when Quantity <= Reorder Level
        var after = _inventory.GetProduct(id);
        if (ok && after != null && after.IsLowStock)
        {
            MessageBox.Show(this,
                $"{after.Name} now has {after.Quantity} in stock (reorder level {after.ReorderLevel}).\nThis product requires restocking.",
                "Low stock warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Runs a service call, shows any business rule error, then refreshes the grid.
    private bool Run(Action action, string? selectId)
    {
        try
        {
            action();
            RefreshList(selectId);
            return true;
        }
        catch (InventoryException ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot complete that", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshList();
            return false;
        }
    }

    // ----- menu -----

    private void LogOut_Click(object sender, RoutedEventArgs e)
    {
        new LoginWindow(_auth, _inventory).Show();
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Warehouse Inventory Management System\nVersion 1.0 (front end)", "About",
            MessageBoxButton.OK, MessageBoxImage.Information);
}

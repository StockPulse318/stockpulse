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

        UserText.Text = $"{user.FullName} ({user.RoleName}) • Branch: {user.AssignedBranch}";
        Title = $"StockPulse - {user.AssignedBranch} [{user.RoleName}: {user.FullName}]";

        var manageProductsVis = user.CanManageProducts ? Visibility.Visible : Visibility.Collapsed;
        AddButton.Visibility = manageProductsVis;
        EditButton.Visibility = manageProductsVis;
        DeleteButton.Visibility = manageProductsVis;

        var manageUsersVis = user.CanManageUsers ? Visibility.Visible : Visibility.Collapsed;
        ManageUsersButton.Visibility = manageUsersVis;
        UsersMenuItem.Visibility = manageUsersVis;

        _ready = true;
        RefreshList();
    }

    private bool _updatingFilters;
    private ProductRow? SelectedRow => ProductGrid.SelectedItem as ProductRow;

    private void PopulateFilters(IReadOnlyList<Product> all)
    {
        _updatingFilters = true;
        try
        {
            var currentBranch = BranchFilterBox.SelectedItem as string ?? "All Branches";
            var branchSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in _inventory.GetBranches())
            {
                if (!string.IsNullOrWhiteSpace(b)) branchSet.Add(b.Trim());
            }
            foreach (var p in all)
            {
                if (!string.IsNullOrWhiteSpace(p.Branch)) branchSet.Add(p.Branch.Trim());
            }

            var branches = new List<string> { "All Branches" };
            branches.AddRange(branchSet.OrderBy(b => b));
            BranchFilterBox.ItemsSource = branches;
            BranchFilterBox.SelectedItem = branches.Contains(currentBranch) ? currentBranch : "All Branches";

            var currentCategory = CategoryFilterBox.SelectedItem as string ?? "All Categories";
            var categorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _inventory.GetCategories())
            {
                if (!string.IsNullOrWhiteSpace(c)) categorySet.Add(c.Trim());
            }
            foreach (var p in all)
            {
                if (!string.IsNullOrWhiteSpace(p.Category)) categorySet.Add(p.Category.Trim());
            }

            var categories = new List<string> { "All Categories" };
            categories.AddRange(categorySet.OrderBy(c => c));
            CategoryFilterBox.ItemsSource = categories;
            CategoryFilterBox.SelectedItem = categories.Contains(currentCategory) ? currentCategory : "All Categories";
        }
        finally
        {
            _updatingFilters = false;
        }
    }

    // Reloads the grid from the service, keeping the same product selected if it is still there.
    private void RefreshList(string? selectId = null)
    {
        if (!_ready || _updatingFilters) return;
        string? keep = selectId ?? SelectedRow?.Id;

        var all = _inventory.GetAllProducts();
        PopulateFilters(all);

        var selectedBranch = BranchFilterBox.SelectedItem as string ?? "All Branches";
        var selectedCategory = CategoryFilterBox.SelectedItem as string ?? "All Categories";

        var found = _inventory.SearchProducts(SearchBox.Text);

        if (selectedBranch != "All Branches")
        {
            found = found.Where(p => string.Equals(p.Branch, selectedBranch, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (selectedCategory != "All Categories")
        {
            found = found.Where(p => string.Equals(p.Category, selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
        }

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

    // ----- user accounts (manager) -----

    private void ManageUsers_Click(object sender, RoutedEventArgs e)
    {
        var usersWindow = new UsersWindow(_user, _auth, _inventory) { Owner = this };
        usersWindow.ShowDialog();
    }

    // ----- product records (manager) -----

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var branches = _inventory.GetBranches();
        var categories = _inventory.GetCategories();
        var dialog = new ProductDialog(_inventory.GetNextProductId(), null, categories, branches) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        Run(() => _inventory.AddProduct(dialog.Result), dialog.Result.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow == null) return;
        var branches = _inventory.GetBranches();
        var categories = _inventory.GetCategories();
        var dialog = new ProductDialog(SelectedRow.Id, SelectedRow.Product, categories, branches) { Owner = this };
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

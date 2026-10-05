using System.Windows;
using System.Windows.Controls;
using WarehouseInventory.Models;
using WarehouseInventory.Services;

namespace WarehouseInventory.Views;

public partial class UsersWindow : Window
{
    private readonly User _currentUser;
    private readonly IAuthService _auth;
    private readonly IInventoryService _inventory;
    private bool _updatingFilters;
    private bool _ready;

    public UsersWindow(User currentUser, IAuthService auth, IInventoryService inventory)
    {
        _currentUser = currentUser;
        _auth = auth;
        _inventory = inventory;
        InitializeComponent();

        LoggedInUserBadge.Text = $"{currentUser.FullName} ({currentUser.RoleName}) • {currentUser.AssignedBranch}";

        Loaded += (s, e) =>
        {
            if (RoleFilterBox.SelectedIndex < 0) RoleFilterBox.SelectedIndex = 0;
            if (StatusFilterBox.SelectedIndex < 0) StatusFilterBox.SelectedIndex = 0;
            _ready = true;
            InitBranchInputs();
            RefreshUsers();
        };
    }

    private UserInfo? SelectedUser => (_ready && UsersGrid != null) ? UsersGrid.SelectedItem as UserInfo : null;

    private void InitBranchInputs()
    {
        try
        {
            var branches = _inventory.GetBranches().Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().OrderBy(b => b).ToList();
            if (!branches.Contains("All Branches"))
                branches.Insert(0, "All Branches");

            NewBranchBox.ItemsSource = branches;
            if (branches.Count > 0)
                NewBranchBox.SelectedIndex = 0;
        }
        catch
        {
            NewBranchBox.ItemsSource = new List<string> { "All Branches" };
            NewBranchBox.SelectedIndex = 0;
        }
    }

    private void PopulateBranchFilter(IReadOnlyList<UserInfo> allUsers)
    {
        _updatingFilters = true;
        try
        {
            var current = BranchFilterBox.SelectedItem as string ?? "All Branches";
            var branchSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "All Branches" };

            try
            {
                foreach (var b in _inventory.GetBranches())
                    if (!string.IsNullOrWhiteSpace(b)) branchSet.Add(b.Trim());
            }
            catch { }

            foreach (var u in allUsers)
            {
                if (!string.IsNullOrWhiteSpace(u.AssignedBranch))
                    branchSet.Add(u.AssignedBranch.Trim());
            }

            var branches = branchSet.OrderBy(b => b == "All Branches" ? "" : b).ToList();
            BranchFilterBox.ItemsSource = branches;
            BranchFilterBox.SelectedItem = branches.Contains(current) ? current : "All Branches";
        }
        finally
        {
            _updatingFilters = false;
        }
    }

    private void RefreshUsers(string? selectUsername = null)
    {
        if (!_ready || _updatingFilters) return;
        string? keep = selectUsername ?? SelectedUser?.Username;

        try
        {
            var all = _auth.GetAllUsers();
            PopulateBranchFilter(all);

            var query = all.AsEnumerable();

            // Search filter
            var term = (SearchBox?.Text ?? "").Trim();
            if (term.Length > 0)
            {
                query = query.Where(u =>
                    u.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Branch filter
            var selectedBranch = BranchFilterBox?.SelectedItem as string ?? "All Branches";
            if (selectedBranch != "All Branches")
            {
                query = query.Where(u => string.Equals(u.AssignedBranch, selectedBranch, StringComparison.OrdinalIgnoreCase));
            }

            // Role filter
            if (RoleFilterBox?.SelectedItem is ComboBoxItem roleItem && roleItem.Content?.ToString() is { } role && role != "All Roles")
            {
                query = query.Where(u => string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase));
            }

            // Status filter
            if (StatusFilterBox?.SelectedItem is ComboBoxItem statusItem && statusItem.Content?.ToString() is { } statusText)
            {
                if (statusText == "Active Only") query = query.Where(u => u.IsActive);
                else if (statusText == "Suspended Only") query = query.Where(u => !u.IsActive);
            }

            var filtered = query.OrderBy(u => u.Username).ToList();
            if (UsersGrid != null)
            {
                UsersGrid.ItemsSource = filtered;
                UsersGrid.SelectedItem = filtered.FirstOrDefault(u => string.Equals(u.Username, keep, StringComparison.OrdinalIgnoreCase));
            }

            int activeCount = all.Count(u => u.IsActive);
            int suspendedCount = all.Count - activeCount;
            if (UserCountText != null)
            {
                UserCountText.Text = $"Showing {filtered.Count} of {all.Count} accounts ({activeCount} active, {suspendedCount} suspended)";
            }

            UpdateActionButtons();
        }
        catch (Exception ex)
        {
            if (UserCountText != null)
                UserCountText.Text = "Failed to load accounts.";
            MessageBox.Show(this, $"Failed to load user accounts:\n\n{ex.Message}", "User Governance", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateActionButtons()
    {
        var selected = SelectedUser;
        bool picked = selected != null;
        bool isSelf = picked && string.Equals(selected!.Username, _currentUser.Username, StringComparison.OrdinalIgnoreCase);

        if (EditUserButton != null) EditUserButton.IsEnabled = picked;
        if (ResetPasswordButton != null) ResetPasswordButton.IsEnabled = picked;
        if (ToggleStatusButton != null) ToggleStatusButton.IsEnabled = picked && !isSelf;
        if (DeleteUserButton != null) DeleteUserButton.IsEnabled = picked && !isSelf;

        if (ToggleStatusButton != null)
        {
            if (picked)
            {
                ToggleStatusButton.Content = selected!.IsActive ? "Suspend Account" : "Activate Account";
            }
            else
            {
                ToggleStatusButton.Content = "Toggle Status";
            }
        }
    }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionButtons();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        if (SearchHint != null)
            SearchHint.Visibility = (SearchBox?.Text.Length ?? 0) == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshUsers();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        RefreshUsers();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        RefreshUsers();
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        FormErrorText.Visibility = Visibility.Collapsed;
        string fullName = NewFullNameBox.Text.Trim();
        string username = NewUsernameBox.Text.Trim();
        string password = NewPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowFormError("Username cannot be empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            ShowFormError("Password must be at least 8 characters long.");
            return;
        }

        var selectedRoleItem = NewRoleBox.SelectedItem as ComboBoxItem;
        string role = selectedRoleItem?.Content?.ToString() ?? "Stock Clerk";
        string branch = NewBranchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(branch))
            branch = "All Branches";

        AddUserButton.IsEnabled = false;
        AddUserButton.Content = "Adding...";

        try
        {
            _auth.RegisterUser(username, password, role, fullName, branch);
            NewFullNameBox.Clear();
            NewUsernameBox.Clear();
            NewPasswordBox.Clear();
            NewRoleBox.SelectedIndex = 0;
            NewBranchBox.SelectedIndex = 0;
            RefreshUsers(username);
            MessageBox.Show(this, $"User account '{username}' successfully registered as {role} (Branch: {branch}).", "Account Provisioned", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowFormError(ex.Message);
        }
        finally
        {
            AddUserButton.IsEnabled = true;
            AddUserButton.Content = "+ Add User";
        }
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUser;
        if (selected == null) return;

        var branches = _inventory.GetBranches();
        var dialog = new EditUserDialog(selected, branches) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _auth.UpdateUser(
                selected.Username,
                dialog.ResultFullName,
                dialog.ResultRole,
                dialog.ResultBranch,
                dialog.ResultIsActive);

            RefreshUsers(selected.Username);
            MessageBox.Show(this, $"Updated user profile for '{selected.Username}'.", "Profile Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to update user profile: {ex.Message}", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUser;
        if (selected == null) return;

        var dialog = new ResetPasswordDialog(selected.Username) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _auth.ResetPassword(selected.Username, dialog.ResultPassword);
            MessageBox.Show(this, $"Password for '{selected.Username}' has been reset successfully.", "Password Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to reset password: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUser;
        if (selected == null) return;

        if (string.Equals(selected.Username, _currentUser.Username, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "You cannot deactivate your own account.", "Action Prohibited", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool newStatus = !selected.IsActive;
        string actionName = newStatus ? "activate" : "suspend";

        var confirm = MessageBox.Show(this,
            $"Are you sure you want to {actionName} user account '{selected.Username}'?",
            "Confirm Status Change",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _auth.UpdateUser(selected.Username, null, null, null, newStatus);
            RefreshUsers(selected.Username);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to change account status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedUser;
        if (selected == null) return;

        if (string.Equals(selected.Username, _currentUser.Username, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "You cannot delete your own account while logged in.", "Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Are you sure you want to permanently delete user account '{selected.Username}' ({selected.Role})?\nThis cannot be undone.",
            "Confirm Delete User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _auth.DeleteUser(selected.Username);
            RefreshUsers();
            MessageBox.Show(this, $"User account '{selected.Username}' deleted.", "User Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to delete user: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UsersGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SelectedUser != null)
            EditUser_Click(sender, e);
    }

    private void ShowFormError(string message)
    {
        FormErrorText.Text = message;
        FormErrorText.Visibility = Visibility.Visible;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

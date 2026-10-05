using System.Windows;
using System.Windows.Controls;
using WarehouseInventory.Models;

namespace WarehouseInventory.Views;

public partial class EditUserDialog : Window
{
    private readonly UserInfo _user;

    public EditUserDialog(UserInfo user, IEnumerable<string> branches)
    {
        InitializeComponent();
        _user = user;

        HeaderTitle.Text = $"Edit Profile: {user.Username}";
        UsernameBox.Text = user.Username;
        FullNameBox.Text = user.FullName;

        // Set role selection
        var targetRole = string.IsNullOrWhiteSpace(user.Role) ? "Stock Clerk" : user.Role;
        foreach (ComboBoxItem item in RoleBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), targetRole, StringComparison.OrdinalIgnoreCase))
            {
                RoleBox.SelectedItem = item;
                break;
            }
        }
        if (RoleBox.SelectedItem == null) RoleBox.SelectedIndex = 0;

        // Set status
        StatusBox.SelectedIndex = user.IsActive ? 0 : 1;

        // Populate branches
        var branchList = new List<string> { "All Branches" };
        branchList.AddRange(branches.Where(b => !string.IsNullOrWhiteSpace(b) && b != "All Branches").Distinct().OrderBy(b => b));
        BranchBox.ItemsSource = branchList;
        BranchBox.Text = string.IsNullOrWhiteSpace(user.AssignedBranch) ? "All Branches" : user.AssignedBranch;

        Loaded += (s, e) => FullNameBox.Focus();
    }

    public string ResultFullName { get; private set; } = string.Empty;
    public string ResultRole { get; private set; } = "Stock Clerk";
    public string ResultBranch { get; private set; } = "All Branches";
    public bool ResultIsActive { get; private set; } = true;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var fullName = FullNameBox.Text.Trim();
        var selectedRoleItem = RoleBox.SelectedItem as ComboBoxItem;
        var role = selectedRoleItem?.Content?.ToString() ?? "Stock Clerk";
        var branch = BranchBox.Text.Trim();
        var isActive = StatusBox.SelectedIndex == 0;

        if (string.IsNullOrWhiteSpace(branch))
        {
            branch = "All Branches";
        }

        ResultFullName = fullName;
        ResultRole = role;
        ResultBranch = branch;
        ResultIsActive = isActive;

        DialogResult = true;
    }
}

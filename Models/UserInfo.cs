using System.Windows.Media;

namespace WarehouseInventory.Models;

public class UserInfo
{
    private static readonly Brush ActiveGreen = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x41));
    private static readonly Brush SuspendedRed = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38));

    static UserInfo()
    {
        ActiveGreen.Freeze();
        SuspendedRed.Freeze();
    }

    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AssignedBranch { get; set; } = "All Branches";
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;

    public bool IsAdmin => string.Equals(Role, "Administrator", StringComparison.OrdinalIgnoreCase);
    public bool IsManager => IsAdmin || string.Equals(Role, "Warehouse Manager", StringComparison.OrdinalIgnoreCase);

    public string StatusText => IsActive ? "Active" : "Suspended";
    public Brush StatusBadgeBrush => IsActive ? ActiveGreen : SuspendedRed;

    public string RoleDescription => Role switch
    {
        "Administrator" => "System Admin (Full Access, User & Role Governance)",
        "Warehouse Manager" => "Warehouse Manager (Product Records, Stock & Logs)",
        _ => "Stock Clerk (Movements & Inventory Lookup)"
    };
}

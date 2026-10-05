namespace WarehouseInventory.Models;

public class UserInfo
{
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
    public string StatusBadgeBrush => IsActive ? "#107C41" : "#D13438";

    public string RoleDescription => Role switch
    {
        "Administrator" => "System Admin (Full Access, User & Role Governance)",
        "Warehouse Manager" => "Warehouse Manager (Product Records, Stock & Logs)",
        _ => "Stock Clerk (Movements & Inventory Lookup)"
    };
}

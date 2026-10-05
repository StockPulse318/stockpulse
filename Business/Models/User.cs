namespace WarehouseInventory.Business.Models;

public sealed class User
{
    public string Username { get; }
    public string FullName { get; }
    public UserRole Role { get; }

    public User(string username, string? fullName, UserRole role)
    {
        Username = username;
        FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName.Trim();
        Role = role;
    }

    public string RoleDisplayName => Role switch
    {
        UserRole.WarehouseManager => "Warehouse Manager",
        UserRole.Clerk => "Stock Clerk",
        _ => "User"
    };

    public bool CanManageProducts => Role == UserRole.WarehouseManager;
}

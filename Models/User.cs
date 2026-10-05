namespace WarehouseInventory.Models;

public enum UserRole { Administrator, Manager, Clerk }

public class User
{
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string AssignedBranch { get; set; } = "All Branches";
    public UserRole Role { get; set; }

    public string RoleName => Role switch
    {
        UserRole.Administrator => "Administrator",
        UserRole.Manager => "Warehouse Manager",
        _ => "Stock Clerk"
    };

    public bool IsAdmin => Role == UserRole.Administrator;
    public bool IsManager => Role is UserRole.Manager or UserRole.Administrator;
    public bool CanManageProducts => IsManager;
    public bool CanManageUsers => IsManager;
}

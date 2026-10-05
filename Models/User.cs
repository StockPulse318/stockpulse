namespace WarehouseInventory.Models;

public enum UserRole { Manager, Clerk }

public class User
{
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }

    public string RoleName => Role == UserRole.Manager ? "Warehouse Manager" : "Stock Clerk";

    // Managers look after product records. Clerks only search and move stock.
    // If the team decides clerks may edit products too, change this one line.
    public bool CanManageProducts => Role == UserRole.Manager;
}

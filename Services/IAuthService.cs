using WarehouseInventory.Models;

namespace WarehouseInventory.Services;

public interface IAuthService
{
    // Returns the user if the details are right, or null if they are not.
    // The real version should compare password hashes, never plain text.
    User? Login(string username, string password);

    IReadOnlyList<UserInfo> GetAllUsers();
    void RegisterUser(string username, string password, string role, string? fullName = null, string? assignedBranch = null);
    void UpdateUser(string username, string? fullName, string? role, string? assignedBranch, bool? isActive);
    void ResetPassword(string username, string newPassword);
    void DeleteUser(string username);
}

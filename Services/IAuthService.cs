using WarehouseInventory.Models;

namespace WarehouseInventory.Services;

public interface IAuthService
{
    // Returns the user if the details are right, or null if they are not.
    // The real version should compare password hashes, never plain text.
    User? Login(string username, string password);
}

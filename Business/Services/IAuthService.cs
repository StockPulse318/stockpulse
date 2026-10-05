using WarehouseInventory.Business.Models;

namespace WarehouseInventory.Business.Services;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    Task<User> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    void Logout();
    void NotifySessionExpired();
    event Action<string>? SessionExpired;
}

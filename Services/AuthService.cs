using WarehouseInventory.Models;

namespace WarehouseInventory.Services;

public class AuthService : IAuthService
{
    private readonly StockPulseApiClient _client;

    public AuthService(StockPulseApiClient client)
    {
        _client = client;
    }

    public StockPulseApiClient Client => _client;

    public User? Login(string username, string password)
    {
        return RunSync(() => _client.LoginAsync(username, password));
    }

    public void Logout()
    {
        _client.ClearToken();
    }

    public IReadOnlyList<UserInfo> GetAllUsers() =>
        RunSync(() => _client.GetUsersAsync());

    public void RegisterUser(string username, string password, string role, string? fullName = null, string? assignedBranch = null) =>
        RunSync(() => _client.RegisterUserAsync(username, password, role, fullName, assignedBranch));

    public void UpdateUser(string username, string? fullName, string? role, string? assignedBranch, bool? isActive) =>
        RunSync(() => _client.UpdateUserAsync(username, fullName, role, assignedBranch, isActive));

    public void ResetPassword(string username, string newPassword) =>
        RunSync(() => _client.ResetPasswordAsync(username, newPassword));

    public void DeleteUser(string username) =>
        RunSync(() => _client.DeleteUserAsync(username));

    private static T RunSync<T>(Func<Task<T>> func)
    {
        try
        {
            return Task.Run(func).GetAwaiter().GetResult();
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static void RunSync(Func<Task> func)
    {
        try
        {
            Task.Run(func).GetAwaiter().GetResult();
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}

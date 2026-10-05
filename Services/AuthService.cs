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
}

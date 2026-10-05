namespace WarehouseInventory.Data.State;

public interface ITokenStorage
{
    string? Token { get; }
    bool IsAuthenticated { get; }
    void SetToken(string token);
    void ClearToken();
}

public sealed class InMemoryTokenStorage : ITokenStorage
{
    private readonly object _lock = new();
    private string? _token;

    public string? Token
    {
        get
        {
            lock (_lock)
            {
                return _token;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_token);
            }
        }
    }

    public void SetToken(string token)
    {
        lock (_lock)
        {
            _token = token;
        }
    }

    public void ClearToken()
    {
        lock (_lock)
        {
            _token = null;
        }
    }
}

using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Data.Http;
using WarehouseInventory.Data.State;

namespace WarehouseInventory.Business.Services;

public sealed class AuthService : IAuthService
{
    private readonly IStockPulseApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly IValidationService _validationService;
    private User? _currentUser;

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null && _tokenStorage.IsAuthenticated;

    public event Action<string>? SessionExpired;

    public AuthService(
        IStockPulseApiClient apiClient,
        ITokenStorage tokenStorage,
        IValidationService validationService)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _validationService = validationService;
    }

    public async Task<User> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        _validationService.ValidateCredentials(username, password);

        var loginDto = await _apiClient.LoginAsync(username.Trim(), password, cancellationToken);

        var role = MapRole(loginDto.Role);
        _currentUser = new User(loginDto.Username, loginDto.FullName, role);
        return _currentUser;
    }

    public void Logout()
    {
        _currentUser = null;
        _tokenStorage.ClearToken();
    }

    public void NotifySessionExpired()
    {
        if (_currentUser == null && !_tokenStorage.IsAuthenticated)
        {
            return;
        }

        Logout();
        SessionExpired?.Invoke("Your session has expired. Please log in again to continue.");
    }

    private static UserRole MapRole(string? roleString)
    {
        if (string.IsNullOrWhiteSpace(roleString))
        {
            return UserRole.Clerk;
        }

        if (roleString.Equals("WAREHOUSE_MANAGER", StringComparison.OrdinalIgnoreCase) ||
            roleString.Equals("Warehouse Manager", StringComparison.OrdinalIgnoreCase) ||
            roleString.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
            roleString.Equals("Manager", StringComparison.OrdinalIgnoreCase))
        {
            return UserRole.WarehouseManager;
        }

        return UserRole.Clerk;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarehouseInventory.Business.Services;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Data.Http;
using WarehouseInventory.Presentation.Services;

namespace WarehouseInventory.Presentation.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IValidationService _validationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(
        IAuthService authService,
        IDialogService dialogService,
        IValidationService validationService)
    {
        _authService = authService;
        _dialogService = dialogService;
        _validationService = validationService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            _validationService.ValidateCredentials(Username, Password);
        }
        catch (ValidationException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        IsBusy = true;

        try
        {
            await _authService.LoginAsync(Username, Password);
            Password = string.Empty; // Zero out password
            _dialogService.ShowMainWindow();
        }
        catch (ApiException ex)
        {
            if (ex.IsUnauthorized)
            {
                ErrorMessage = "Invalid username or password. Please try again.";
            }
            else
            {
                ErrorMessage = ex.DisplayMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to sign in: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

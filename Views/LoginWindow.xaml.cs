using System.Windows;
using WarehouseInventory.Services;

namespace WarehouseInventory.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _auth;
    private readonly IInventoryService _inventory;
    private readonly StockPulseApiClient? _client;

    public LoginWindow(IAuthService auth, IInventoryService inventory, StockPulseApiClient? client = null)
    {
        _auth = auth;
        _inventory = inventory;
        _client = client ?? (_auth as AuthService)?.Client;

        InitializeComponent();

        if (_client != null)
        {
            ServerUrlBox.Text = _client.BaseUrl;
            StatusText.Text = $"Backend: {_client.BaseUrl}";
        }
        else
        {
            ServerUrlBox.Text = StockPulseConfig.DefaultBaseUrl;
            StatusText.Text = $"Backend: {StockPulseConfig.DefaultBaseUrl}";
        }

        Loaded += (s, e) => UsernameBox.Focus();
    }

    private void LogIn_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        string username = UsernameBox.Text.Trim();
        string password = PasswordBox.Password;

        if (username.Length == 0 || password.Length == 0)
        {
            ErrorText.Text = "Enter your username and password.";
            return;
        }

        string newUrl = ServerUrlBox.Text.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(newUrl))
        {
            if (_client != null && !string.Equals(_client.BaseUrl, newUrl, StringComparison.OrdinalIgnoreCase))
            {
                _client.BaseUrl = newUrl;
                StockPulseConfig.SaveBaseUrl(newUrl);
                StatusText.Text = $"Backend: {newUrl}";
            }
        }

        LoginButton.IsEnabled = false;
        LoginButton.Content = "Signing in...";

        try
        {
            var user = _auth.Login(username, password);
            if (user == null)
            {
                ErrorText.Text = "Invalid username or password. Please try again.";
                PasswordBox.Clear();
                PasswordBox.Focus();
                return;
            }

            new MainWindow(user, _auth, _inventory).Show();
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            PasswordBox.SelectAll();
            PasswordBox.Focus();
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Log in";
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}

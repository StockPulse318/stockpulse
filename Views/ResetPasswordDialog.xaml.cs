using System.Windows;

namespace WarehouseInventory.Views;

public partial class ResetPasswordDialog : Window
{
    private readonly string _username;

    public ResetPasswordDialog(string username)
    {
        InitializeComponent();
        _username = username;
        HeaderTitle.Text = $"Reset Password: {username}";

        Loaded += (s, e) => NewPasswordBox.Focus();
    }

    public string ResultPassword { get; private set; } = string.Empty;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        var pwd = NewPasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 8)
        {
            ErrorText.Text = "Password must be at least 8 characters long.";
            return;
        }

        if (pwd != confirm)
        {
            ErrorText.Text = "Passwords do not match. Please re-enter.";
            return;
        }

        ResultPassword = pwd;
        DialogResult = true;
    }
}

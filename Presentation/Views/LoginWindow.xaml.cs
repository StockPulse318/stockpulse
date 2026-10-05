using System.Windows;
using WarehouseInventory.Presentation.ViewModels;

namespace WarehouseInventory.Presentation.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void PasswordInputBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordInputBox.Password;
    }
}

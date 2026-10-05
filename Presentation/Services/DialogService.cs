using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WarehouseInventory.Business.Models;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Presentation.ViewModels;
using WarehouseInventory.Presentation.Views;

namespace WarehouseInventory.Presentation.Services;

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IValidationService _validationService;

    public DialogService(IServiceProvider serviceProvider, IValidationService validationService)
    {
        _serviceProvider = serviceProvider;
        _validationService = validationService;
    }

    public void ShowInformation(string title, string message)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var active = GetActiveWindow();
            if (active != null)
                MessageBox.Show(active, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    public void ShowWarning(string title, string message)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var active = GetActiveWindow();
            if (active != null)
                MessageBox.Show(active, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    public void ShowError(string title, string message)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var active = GetActiveWindow();
            if (active != null)
                MessageBox.Show(active, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    public bool ShowConfirmation(string title, string message)
    {
        return App.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = new ConfirmDialogViewModel(title, message);
            var dialog = new ConfirmDialog(vm)
            {
                Owner = GetActiveWindow()
            };
            return dialog.ShowDialog() == true;
        }) ?? false;
    }

    public bool ShowStockDialog(Product product, bool isStockIn, out int quantity)
    {
        int resultQuantity = 0;
        bool confirmed = App.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = new StockDialogViewModel(product, isStockIn, _validationService);
            var dialog = new StockDialog(vm)
            {
                Owner = GetActiveWindow()
            };
            if (dialog.ShowDialog() == true)
            {
                resultQuantity = vm.ValidatedAmount;
                return true;
            }
            return false;
        }) ?? false;

        quantity = resultQuantity;
        return confirmed;
    }

    public bool ShowProductDialog(Product? existingProduct, IReadOnlyList<Category> categories, out Product result)
    {
        Product? resultProduct = null;
        bool confirmed = App.Current?.Dispatcher?.Invoke(() =>
        {
            var vm = new ProductDialogViewModel(existingProduct, categories, _validationService);
            var dialog = new ProductDialog(vm)
            {
                Owner = GetActiveWindow()
            };
            if (dialog.ShowDialog() == true && vm.ResultProduct != null)
            {
                resultProduct = vm.ResultProduct;
                return true;
            }
            return false;
        }) ?? false;

        result = resultProduct ?? new Product();
        return confirmed;
    }

    public void ShowMainWindow()
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var oldWindow = App.Current.MainWindow;
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainViewModel);

            App.Current.MainWindow = mainWindow;
            mainWindow.Show();
            if (oldWindow != null && oldWindow != mainWindow)
            {
                oldWindow.Close();
            }
        });
    }

    public void ShowLoginWindow(string? message = null)
    {
        App.Current?.Dispatcher?.Invoke(() =>
        {
            var oldWindow = App.Current.MainWindow;
            var loginViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
            if (!string.IsNullOrWhiteSpace(message))
            {
                loginViewModel.ErrorMessage = message;
            }

            var loginWindow = new LoginWindow(loginViewModel);
            App.Current.MainWindow = loginWindow;
            loginWindow.Show();
            if (oldWindow != null && oldWindow != loginWindow)
            {
                oldWindow.Close();
            }
        });
    }

    private static Window? GetActiveWindow()
    {
        return App.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
               ?? App.Current?.MainWindow;
    }
}

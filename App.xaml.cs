using System.IO;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WarehouseInventory.Business.Services;
using WarehouseInventory.Business.Validation;
using WarehouseInventory.Data.Http;
using WarehouseInventory.Data.State;
using WarehouseInventory.Presentation.Services;
using WarehouseInventory.Presentation.ViewModels;
using WarehouseInventory.Presentation.Views;

namespace WarehouseInventory;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureGlobalExceptionHandling();

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        _configuration = builder.Build();

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
        dialogService.ShowLoginWindow();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 1. Configuration
        var apiBaseUrl = _configuration?["ApiBaseUrl"]
            ?? _configuration?["Backend:BaseUrl"]
            ?? "https://stockpulse-backend-production-4c30.up.railway.app";

        apiBaseUrl = apiBaseUrl.Trim().TrimEnd('/');

        // 2. Data Layer
        services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();

        services.AddHttpClient<IStockPulseApiClient, StockPulseApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // 3. Business Logic Layer
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IInventoryService, InventoryService>();

        // 4. Presentation Layer
        services.AddSingleton<IDialogService, DialogService>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }

    private void ConfigureGlobalExceptionHandling()
    {
        DispatcherUnhandledException += (sender, e) =>
        {
            e.Handled = true;
            ShowErrorDialog("Application Error", e.Exception.Message);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowErrorDialog("Fatal Error", ex.Message);
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            e.SetObserved();
            ShowErrorDialog("Background Task Error", e.Exception.Message);
        };
    }

    private static void ShowErrorDialog(string title, string message)
    {
        MessageBox.Show(
            $"{message}\n\nPlease check your server connection or contact support if the issue persists.",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

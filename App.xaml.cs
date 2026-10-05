using System.Windows;
using WarehouseInventory.Services;
using WarehouseInventory.Views;

namespace WarehouseInventory;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = StockPulseConfig.Load();
        var client = new StockPulseApiClient(config.BaseUrl);

        IAuthService auth = new AuthService(client);
        IInventoryService inventory = new InventoryService(client);

        new LoginWindow(auth, inventory, client).Show();
    }
}

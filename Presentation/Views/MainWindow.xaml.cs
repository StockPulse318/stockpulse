using System.Windows;
using System.Windows.Input;
using WarehouseInventory.Presentation.ViewModels;

namespace WarehouseInventory.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
        };
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void InventoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.CanManageProducts && _viewModel.SelectedProduct != null)
        {
            _viewModel.EditProductCommand.Execute(null);
        }
    }
}

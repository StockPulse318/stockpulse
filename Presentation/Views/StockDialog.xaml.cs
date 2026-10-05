using System.Windows;
using WarehouseInventory.Presentation.ViewModels;

namespace WarehouseInventory.Presentation.Views;

public partial class StockDialog : Window
{
    public StockDialog(StockDialogViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        viewModel.CloseRequested += () =>
        {
            DialogResult = viewModel.DialogResult;
            Close();
        };
    }
}

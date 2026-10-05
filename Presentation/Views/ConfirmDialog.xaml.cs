using System.Windows;
using WarehouseInventory.Presentation.ViewModels;

namespace WarehouseInventory.Presentation.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(ConfirmDialogViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        viewModel.CloseRequested += () =>
        {
            DialogResult = viewModel.Confirmed;
            Close();
        };
    }
}

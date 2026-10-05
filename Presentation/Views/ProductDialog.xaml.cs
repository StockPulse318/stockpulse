using System.Windows;
using WarehouseInventory.Presentation.ViewModels;

namespace WarehouseInventory.Presentation.Views;

public partial class ProductDialog : Window
{
    public ProductDialog(ProductDialogViewModel viewModel)
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

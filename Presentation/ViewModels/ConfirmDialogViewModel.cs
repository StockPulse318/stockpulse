using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WarehouseInventory.Presentation.ViewModels;

public sealed partial class ConfirmDialogViewModel : ObservableObject
{
    public string Title { get; }
    public string Message { get; }
    public bool Confirmed { get; private set; }

    public event Action? CloseRequested;

    public ConfirmDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    [RelayCommand]
    private void Confirm()
    {
        Confirmed = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested?.Invoke();
    }
}

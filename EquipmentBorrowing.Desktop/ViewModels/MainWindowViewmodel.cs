namespace EquipmentBorrowing.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private object? currentPage;

    public EquipmentViewModel EquipmentViewModel { get; }
    public BorrowingsViewModel BorrowingsViewModel { get; }

    public MainWindowViewModel(EquipmentViewModel equipmentViewModel, BorrowingsViewModel borrowingsViewModel)
    {
        EquipmentViewModel = equipmentViewModel;
        BorrowingsViewModel = borrowingsViewModel;
        CurrentPage = EquipmentViewModel;
    }

    [RelayCommand]
    private void ShowEquipment()
    {
        CurrentPage = EquipmentViewModel;
        _ = EquipmentViewModel.LoadEquipmentCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowBorrowings()
    {
        CurrentPage = BorrowingsViewModel;
        _ = BorrowingsViewModel.LoadActiveBorrowingsCommand.ExecuteAsync(null);
    }
}
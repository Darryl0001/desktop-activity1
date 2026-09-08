namespace EquipmentBorrowing.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public partial class BorrowingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string statusMessage = "Active Borrowings screen — data coming next step.";
}
namespace EquipmentBorrowing.Desktop.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using System.Threading.Tasks;

public partial class BorrowingsViewModel : ObservableObject
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly ReturnEquipmentService _returnEquipmentService;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private Borrowing? selectedBorrowing;

    public ObservableCollection<Borrowing> ActiveBorrowings { get; } = new();

    public BorrowingsViewModel(IBorrowingRepository borrowingRepository, ReturnEquipmentService returnEquipmentService)
    {
        _borrowingRepository = borrowingRepository;
        _returnEquipmentService = returnEquipmentService;
        _ = LoadActiveBorrowingsAsync();
    }

    [RelayCommand]
    private async Task LoadActiveBorrowingsAsync()
    {
        var items = await _borrowingRepository.GetActiveBorrowingsAsync();
        ActiveBorrowings.Clear();
        foreach (var item in items)
        {
            ActiveBorrowings.Add(item);
        }
        StatusMessage = $"{ActiveBorrowings.Count} active borrowing(s).";
    }

    [RelayCommand]
    private async Task ReturnAsync()
    {
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Select a borrowing to return first.";
            return;
        }

        var result = await _returnEquipmentService.ExecuteAsync(SelectedBorrowing.Id);

        if (result.IsSuccess)
        {
            StatusMessage = $"Returned borrowing #{result.Borrowing!.Id}.";
            await LoadActiveBorrowingsAsync();
        }
        else
        {
            StatusMessage = $"Return failed: {result.ErrorMessage}";
        }
    }
}
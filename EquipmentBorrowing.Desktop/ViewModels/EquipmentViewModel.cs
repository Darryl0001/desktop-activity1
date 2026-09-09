namespace EquipmentBorrowing.Desktop.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

public partial class EquipmentViewModel : ObservableObject
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly BorrowEquipmentService _borrowEquipmentService;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private Equipment? selectedEquipment;

    [ObservableProperty]
    private string studentIdInput = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? selectedReturnDate = DateTimeOffset.Now.AddDays(7);

    public ObservableCollection<Equipment> Equipment { get; } = new();

    public EquipmentViewModel(IEquipmentRepository equipmentRepository, BorrowEquipmentService borrowEquipmentService)
    {
        _equipmentRepository = equipmentRepository;
        _borrowEquipmentService = borrowEquipmentService;
        _ = LoadEquipmentAsync();
    }

    [RelayCommand]
    private async Task LoadEquipmentAsync()
    {
        var items = await _equipmentRepository.GetAllAsync();
        Equipment.Clear();
        foreach (var item in items)
        {
            Equipment.Add(item);
        }
        StatusMessage = $"{Equipment.Count} equipment item(s) loaded.";
    }

    [RelayCommand]
    private async Task BorrowAsync()
    {
        // --- Presentation validation: is the form itself filled in correctly? ---
        if (SelectedEquipment is null)
        {
            StatusMessage = "Select an equipment item first.";
            return;
        }

        if (!int.TryParse(StudentIdInput, out var studentId))
        {
            StatusMessage = "Enter a valid numeric Student ID.";
            return;
        }

        if (SelectedReturnDate is null || SelectedReturnDate.Value.Date < DateTime.Today)
        {
            StatusMessage = "Please choose a valid expected return date (today or later).";
            return;
        }

        // --- Business logic lives entirely in the service, not here ---
        var result = await _borrowEquipmentService.ExecuteAsync(
            studentId,
            SelectedEquipment.Id,
            SelectedReturnDate.Value.DateTime);

        if (result.IsSuccess)
        {
            StatusMessage = $"Borrowed! Borrowing #{result.Borrowing!.Id}, due {result.Borrowing.ExpectedReturnDate:d}.";
            await LoadEquipmentAsync(); // refresh so availability reflects the change
        }
        else
        {
            StatusMessage = $"Borrow failed: {result.ErrorMessage}";
        }
    }
}
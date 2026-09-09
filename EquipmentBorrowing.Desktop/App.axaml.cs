using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure.Repositories;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var studentRepository = new InMemoryStudentRepository();
            var equipmentRepository = new InMemoryEquipmentRepository();
            var borrowingRepository = new InMemoryBorrowingRepository();

            var borrowEquipmentService = new BorrowEquipmentService(studentRepository, equipmentRepository, borrowingRepository);
            var returnEquipmentService = new ReturnEquipmentService(borrowingRepository, equipmentRepository, studentRepository);

            var equipmentViewModel = new EquipmentViewModel(equipmentRepository, borrowEquipmentService);
            var borrowingsViewModel = new BorrowingsViewModel(borrowingRepository, returnEquipmentService);
            var mainWindowViewModel = new MainWindowViewModel(equipmentViewModel, borrowingsViewModel);

            desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
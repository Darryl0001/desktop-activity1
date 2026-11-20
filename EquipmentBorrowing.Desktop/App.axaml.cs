using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop;
public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<EquipmentBorrowingDbContext>(o => o.UseNpgsql(DatabaseConfiguration.ConnectionString));
        services.AddSingleton<ILabRepository, PostgresLabRepository>();
        services.AddSingleton<ICalendar, ManilaCalendar>();
        services.AddSingleton<LabService>();
        services.AddTransient<WorkbenchViewModel>();
        var provider = services.BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = provider.GetRequiredService<WorkbenchViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.MainWindow.Opened += async (_, _) => await vm.RefreshCommand.ExecuteAsync(null);
            desktop.Exit += (_, _) => provider.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

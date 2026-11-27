using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Desktop.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop;
public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ =>
        {
            var address = Environment.GetEnvironmentVariable("LAB5_API_BASE_URL") ?? "http://127.0.0.1:5185";
            return new HttpClient
            {
                BaseAddress = Uri.TryCreate(address.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https" ? uri : null,
                Timeout = TimeSpan.FromSeconds(20)
            };
        });
        services.AddSingleton<ILabOperations, HttpLabClient>();
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

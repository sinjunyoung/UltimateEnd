using System;
using Avalonia;
using ReactiveUI.Avalonia;

namespace UltimateEnd.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .UseReactiveUI()
        .LogToTrace();
}
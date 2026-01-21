using Avalonia.Controls;
using System;
using UltimateEnd.Managers;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        this.Deactivated += OnWindowDeactivated;
        this.Activated += OnWindowActivated;
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        ScreenSaverManager.Instance.OnWindowDeactivated();

        var gameListViewModel = FindGameListViewModel();
        gameListViewModel?.StopVideo();
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        ScreenSaverManager.Instance.OnWindowActivated();
        InputManager.LoadKeyBindings();

        if (OverlayHelper.IsAnyOverlayVisible(this)) return;        

        var gameListViewModel = FindGameListViewModel();
        _ = gameListViewModel?.ResumeVideoAsync();
    }

    private GameListViewModel? FindGameListViewModel()
    {
        if (this.DataContext is MainViewModel mainViewModel) return mainViewModel.CurrentView as GameListViewModel;

        return null;
    }
}
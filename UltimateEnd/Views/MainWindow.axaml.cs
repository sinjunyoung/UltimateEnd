using Avalonia.Controls;
using Avalonia.VisualTree;
using System;
using System.Linq;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Overlays;

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
        var gameListViewModel = FindGameListViewModel();
        gameListViewModel?.StopVideo();
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (OverlayHelper.IsAnyOverlayVisible(this))
            return;

        var gameListViewModel = FindGameListViewModel();
        _ = gameListViewModel?.ResumeVideoAsync();
    }

    private GameListViewModel? FindGameListViewModel()
    {
        if (this.DataContext is MainViewModel mainViewModel)
            return mainViewModel.CurrentView as GameListViewModel;

        return null;
    }
}
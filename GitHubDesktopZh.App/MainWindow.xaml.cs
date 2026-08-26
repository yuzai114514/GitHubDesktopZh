using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using GitHubDesktopZh.App.ViewModels;

namespace GitHubDesktopZh.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    public void CheckForUpdate()
    {
        _viewModel.CheckForUpdatesCommand.Execute(null);
    }

    public void Relocalize()
    {
        _viewModel.LocalizeCommand.Execute(null);
    }

    public void OpenSettings()
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.ShowDialog();
    }
}
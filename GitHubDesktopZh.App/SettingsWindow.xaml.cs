using System.Windows;
using Wpf.Ui.Controls;
using GitHubDesktopZh.App.ViewModels;

namespace GitHubDesktopZh.App;

public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;

        Loaded += SettingsWindow_Loaded;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadSettingsAsync();
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveSettingsAsync();
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
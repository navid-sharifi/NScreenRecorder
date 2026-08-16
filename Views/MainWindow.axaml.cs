using Avalonia.Controls;
using Avalonia.Interactivity;
using ScreenRecorder.ViewModels;

namespace ScreenRecorder.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnSelectAreaClick(object? sender, RoutedEventArgs e)
    {
        var selectionWindow = new RegionSelectionWindow();
        await selectionWindow.ShowDialog(this);

        if (selectionWindow.SelectedRegion.HasValue && DataContext is MainViewModel vm)
        {
            var region = selectionWindow.SelectedRegion.Value;
            vm.Settings.RegionLeft = region.X;
            vm.Settings.RegionTop = region.Y;
            vm.Settings.RegionRight = region.X + region.Width;
            vm.Settings.RegionBottom = region.Y + region.Height;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
        base.OnClosing(e);
    }
}
using Avalonia.Controls;
using Avalonia.Interactivity;
using ScreenRecorder.ViewModels;
using System.Threading.Tasks;

namespace ScreenRecorder.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnSelectAreaClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Hide this options window to let user see the screen clearly during selection
        Hide();
        await Task.Delay(250);

        var selectionWindow = new RegionSelectionWindow();
        await selectionWindow.ShowDialog(this);

        Show();
        Activate();

        if (selectionWindow.SelectedRegion.HasValue)
        {
            var region = selectionWindow.SelectedRegion.Value;
            vm.Settings.RegionLeft = region.X;
            vm.Settings.RegionTop = region.Y;
            vm.Settings.RegionRight = region.X + region.Width;
            vm.Settings.RegionBottom = region.Y + region.Height;
        }
    }

    private async void OnTakeScreenshotClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Get this options window out of the shot, then bring it back before the editor opens.
        Hide();
        await Task.Delay(250);

        var capture = vm.CaptureScreenshotImage();

        Show();
        Activate();

        if (capture != null)
        {
            vm.OpenScreenshotEditor(capture);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
        base.OnClosing(e);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Shapes;
using System;

namespace ScreenRecorder.Views
{
    public partial class RegionSelectionWindow : Window
    {
        private Point _startPoint;
        private bool _isDrawing;
        private Rectangle _selectionRectangle;

        public Avalonia.PixelRect? SelectedRegion { get; private set; }

        public RegionSelectionWindow()
        {
            InitializeComponent();
            _selectionRectangle = this.FindControl<Rectangle>("SelectionRectangle")!;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            _isDrawing = true;
            _selectionRectangle.IsVisible = true;
            Canvas.SetLeft(_selectionRectangle, _startPoint.X);
            Canvas.SetTop(_selectionRectangle, _startPoint.Y);
            _selectionRectangle.Width = 0;
            _selectionRectangle.Height = 0;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDrawing) return;

            var currentPoint = e.GetPosition(this);
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(_selectionRectangle, x);
            Canvas.SetTop(_selectionRectangle, y);
            _selectionRectangle.Width = width;
            _selectionRectangle.Height = height;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDrawing) return;
            _isDrawing = false;

            var left = Canvas.GetLeft(_selectionRectangle);
            var top = Canvas.GetTop(_selectionRectangle);
            
            if (_selectionRectangle.Width > 10 && _selectionRectangle.Height > 10)
            {
                var scaling = this.RenderScaling;
                SelectedRegion = new Avalonia.PixelRect(
                    (int)(left * scaling), 
                    (int)(top * scaling), 
                    (int)(_selectionRectangle.Width * scaling), 
                    (int)(_selectionRectangle.Height * scaling));
            }
            
            this.Close();
        }
    }
}
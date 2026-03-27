using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace GlassManagement.SectionTool;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.PreviewOuter) or nameof(MainViewModel.Result))
        {
            DrawPreview();
        }
    }

    private async void OnPasteClicked(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        var text = await clipboard.GetTextAsync();
        _vm.PasteFromClipboard(text);
    }

    private void OnAddHolePoint(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HoleGroup hole)
            hole.Points.Add(new PointRow());
    }

    private void OnRemoveHolePoint(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HoleGroup hole && hole.Points.Count > 0)
            hole.Points.RemoveAt(hole.Points.Count - 1);
    }

    private void OnBrowseDb(object? sender, RoutedEventArgs e) => _vm.BrowseDatabase();
    private void OnCreateDb(object? sender, RoutedEventArgs e) => _vm.CreateNewDatabase();

    private void DrawPreview()
    {
        var canvas = this.FindControl<Canvas>("PreviewCanvas");
        if (canvas == null) return;
        canvas.Children.Clear();

        var outer = _vm.PreviewOuter;
        if (outer == null || outer.Count < 3) return;

        double canvasW = canvas.Bounds.Width;
        double canvasH = canvas.Bounds.Height;
        if (canvasW < 10 || canvasH < 10) return;

        // Find bounds
        double minX = outer.Min(p => p.X), maxX = outer.Max(p => p.X);
        double minY = outer.Min(p => p.Y), maxY = outer.Max(p => p.Y);

        if (_vm.PreviewHoles != null)
        {
            foreach (var hole in _vm.PreviewHoles)
            {
                foreach (var p in hole)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }
        }

        double rangeX = maxX - minX;
        double rangeY = maxY - minY;
        if (rangeX < 1e-6 || rangeY < 1e-6) return;

        double margin = 30;
        double scaleX = (canvasW - 2 * margin) / rangeX;
        double scaleY = (canvasH - 2 * margin) / rangeY;
        double scale = Math.Min(scaleX, scaleY);

        double offsetX = margin + (canvasW - 2 * margin - rangeX * scale) / 2;
        double offsetY = margin + (canvasH - 2 * margin - rangeY * scale) / 2;

        Point Transform(PointD p) => new(
            offsetX + (p.X - minX) * scale,
            canvasH - (offsetY + (p.Y - minY) * scale) // flip Y
        );

        // Draw outer
        var outerPoly = new Polygon
        {
            Points = outer.Select(Transform).ToList(),
            Stroke = new SolidColorBrush(Color.Parse("#512BD4")),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.Parse("#E8E5F8")),
        };
        canvas.Children.Add(outerPoly);

        // Draw holes
        if (_vm.PreviewHoles != null)
        {
            foreach (var hole in _vm.PreviewHoles)
            {
                if (hole.Count < 3) continue;
                var holePoly = new Polygon
                {
                    Points = hole.Select(Transform).ToList(),
                    Stroke = new SolidColorBrush(Color.Parse("#EF4444")),
                    StrokeThickness = 1.5,
                    Fill = new SolidColorBrush(Color.Parse("#F5F3FF")),
                };
                canvas.Children.Add(holePoly);
            }
        }

        // Draw centroid
        if (_vm.Result != null)
        {
            var cp = Transform(new PointD { X = _vm.Result.Cx, Y = _vm.Result.Cy });
            var cross1 = new Line { StartPoint = new Point(cp.X - 8, cp.Y), EndPoint = new Point(cp.X + 8, cp.Y), Stroke = Brushes.Red, StrokeThickness = 2 };
            var cross2 = new Line { StartPoint = new Point(cp.X, cp.Y - 8), EndPoint = new Point(cp.X, cp.Y + 8), Stroke = Brushes.Red, StrokeThickness = 2 };
            canvas.Children.Add(cross1);
            canvas.Children.Add(cross2);

            // Coordinate axes from centroid
            var axisX = new Line { StartPoint = cp, EndPoint = new Point(cp.X + 40, cp.Y), Stroke = Brushes.Blue, StrokeThickness = 1, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 } };
            var axisY = new Line { StartPoint = cp, EndPoint = new Point(cp.X, cp.Y - 40), Stroke = Brushes.Green, StrokeThickness = 1, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 } };
            canvas.Children.Add(axisX);
            canvas.Children.Add(axisY);
        }
    }
}

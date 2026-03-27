using Avalonia.Controls;
using Avalonia.Interactivity;
using GlassManagement.Desktop.ViewModels;

namespace GlassManagement.Desktop.Views;

public partial class GlassConfigWindow : Window
{
    private GlassConfigViewModel VM => (GlassConfigViewModel)DataContext!;

    public GlassConfigWindow()
    {
        InitializeComponent();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        VM.IsConfirmed = true;
        Close(true);
    }
}

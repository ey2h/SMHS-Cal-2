using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>Browse the 34 reconstructed Calc V35 forms.</summary>
public partial class CalcBrowserWindow : Window
{
    public CalcBrowserWindow()
    {
        InitializeComponent();

        var list = this.FindControl<ListBox>("FormList")!;
        var host = this.FindControl<ContentControl>("Host")!;
        var info = this.FindControl<TextBlock>("InfoText")!;

        list.ItemsSource = CalcFormCatalog.All
            .Select((e, i) => $"{i + 1:00}  {e.Title}")
            .ToList();

        list.SelectionChanged += (_, _) =>
        {
            var idx = list.SelectedIndex;
            if (idx < 0 || idx >= CalcFormCatalog.All.Count) return;
            var e = CalcFormCatalog.All[idx];
            try
            {
                host.Content = e.Create();
                info.Text = $"{e.Width}×{e.Height}px · 源控件 {e.SourceControls}";
            }
            catch (System.Exception ex)
            {
                info.Text = "加载失败: " + ex.Message;
            }
        };

        list.SelectedIndex = CalcFormCatalog.All
            .Select((e, i) => (e.Title, i))
            .Where(t => t.Title.Contains("玻璃计算"))
            .Select(t => t.i)
            .DefaultIfEmpty(0)
            .First();
    }
}
